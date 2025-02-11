// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Client.Messages;
using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.Debugging;
using Metaplay.Core.Message;
using Metaplay.Core.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Metaplay.Unity.IncidentReporting
{
    /// <summary>
    /// Manages syncing new incidents from repository to server and deleting them after upload is complete.
    /// </summary>
    public class IncidentUploader
    {
        enum State
        {
            /// <summary>
            /// Waiting for incidents to become available.
            /// </summary>
            WaitingForIncidents,

            /// <summary>
            /// Waiting for server to reply to upload proposal.
            /// </summary>
            ProposingUploads,

            /// <summary>
            /// Starting uploading of next requested incidents.
            /// </summary>
            NextUpload,

            /// <summary>
            /// Upload logic is running in background.
            /// We are waiting for server to acknowledge upload.
            /// </summary>
            Uploading,

            /// <summary>
            /// No connection, nothing to do.
            /// </summary>
            NoConnection
        }

        class IncidentAlreadyUploadedException : Exception
        {
        }

        TimeSpan StartDelayAfterSessionStart => TimeSpan.FromSeconds(5); // Minimum interval to avoid spamming the server
        TimeSpan UploadCooldown => TimeSpan.FromSeconds(10); // Minimum interval to avoid spamming the server. \note: If you change this, modify the server-side throttle too.
        TimeSpan UploadFailureCooldown => TimeSpan.FromSeconds(1); // Minimum interval avoid IO spam.

        readonly LogChannel _log;
        readonly MessageDispatcher _messageDispatcher;

        object _lock;
        State _state;
        DateTime _nextProposalAt;
        IncidentRepository.PendingIncident[] _proposedUploads;
        DateTime _nextUploadAt;
        Queue<string> _incidentsToUpload;
        string _ongoingUploadIncident;
        LoginIncidentReportDebugDiagnostics _debugDiagnostics;

        int _nextUploadUrlRequestId = 0;
        Dictionary<int, TaskCompletionSource<PlayerIncidentUploadUrlResponse>> _pendingUploadUrlResponse = new();

        public IncidentUploader()
        {
            _log = MetaplaySDK.Logs.Incidents;
            _messageDispatcher = MetaplaySDK.MessageDispatcher;

            _state = State.NoConnection;
            _lock = new object();
            _debugDiagnostics = new LoginIncidentReportDebugDiagnostics();

            // Register message listeners
            _messageDispatcher.AddListener<SessionProtocol.SessionStartSuccess>(OnSessionStartSuccess);
            _messageDispatcher.AddListener<DisconnectedFromServer>(OnDisconnectedFromServer);
            _messageDispatcher.AddListener<PlayerRequestIncidentReportUploads>(OnPlayerRequestIncidentReportUploads);
            _messageDispatcher.AddListener<PlayerAckIncidentReportUpload>(OnPlayerAckIncidentReportUpload);
            _messageDispatcher.AddListener<PlayerIncidentUploadUrlResponse>(OnPlayerIncidentUploadUrlResponse);
        }

        public void LateUpdate(DateTime timeNow)
        {
            // Complete state transition in locked scope, side-effects in unlocked scope.

            IncidentRepository.PendingIncident[] incidents = MetaplaySDK.IncidentRepository.GetAll();
            string nextIncident;
            lock (_lock)
            {
                _debugDiagnostics.CurrentPendingIncidents = incidents.Length;
                _debugDiagnostics.CurrentRequestedIncidents = _state == State.NextUpload ? _incidentsToUpload.Count : 0;

                switch (_state)
                {
                    case State.WaitingForIncidents:
                    {
                        if (incidents.Length == 0)
                            return;
                        if (timeNow < _nextProposalAt)
                            return;

                        // Check that the pending incidents are for the current environment
                        string envId = MetaplaySDK.CurrentEnvironmentConfig.Id ?? "";

                        List<IncidentRepository.PendingIncident> pendingIncidents = new List<IncidentRepository.PendingIncident>();
                        foreach (IncidentRepository.PendingIncident pendingIncident in incidents)
                        {
                            // Ignoring reports that don't match current environment (also tolerating failure to get current environment id)
                            if (pendingIncident.EnvironmentId == envId || pendingIncident.EnvironmentId == "" || envId == "")
                            {
                                pendingIncidents.Add(pendingIncident);
                            }
                        }

                        if (pendingIncidents.Count == 0)
                            return;

                        _proposedUploads = pendingIncidents.ToArray();
                        _state = State.ProposingUploads;

                        goto complete_waiting_for_incidents;
                    }

                    case State.ProposingUploads:
                    {
                        // nada, waiting for PlayerRequestIncidentReportUploads.
                        return;
                    }

                    case State.NextUpload:
                    {
                        if (timeNow < _nextUploadAt)
                        {
                            // \note: we first wait the cooldown, and only then check if there
                            //        was more work. This allows cooldown to work after last upload.
                            return;
                        }
                        if (_incidentsToUpload.Count == 0)
                        {
                            _state = State.WaitingForIncidents;
                            _nextProposalAt = DateTime.UtcNow;
                            return;
                        }

                        nextIncident = _incidentsToUpload.Dequeue();

                        _ongoingUploadIncident = nextIncident;
                        _state = State.Uploading;
                        _debugDiagnostics.UploadsAttempted++;

                        goto complete_next_upload;
                    }

                    case State.Uploading:
                    {
                        // nada, waiting for background task to complete
                        // and then for PlayerAckIncidentReportUpload
                        return;
                    }

                    case State.NoConnection:
                    {
                        // nada, waiting for ConnectedToServer
                        return;
                    }

                    default:
                        return;
                }
            }

            // unreachable

complete_waiting_for_incidents:
            _log.Debug("Informing the server of {NumPendingIncidents} pending incident reports", incidents.Length);
            _messageDispatcher.SendMessage(new PlayerAvailableIncidentReports(_proposedUploads.Select(proposed => proposed.ToClientUploadProposal()).ToArray()));
            return;

complete_next_upload:
            _log.Debug("Uploading incident report {IncidentReport}. ({NumReportsPending} more request(s) pending)", nextIncident, _incidentsToUpload.Count);
            TryUploadIncidentOnBackground(nextIncident);
            return;
        }

        public LoginIncidentReportDebugDiagnostics GetLoginIncidentReportDebugDiagnostics()
        {
            lock (_lock)
            {
                return _debugDiagnostics.Clone();
            }
        }

        void OnSessionStartSuccess(SessionProtocol.SessionStartSuccess success)
        {
            // When connection is established, start uploading. In Offline mode there is no
            // point in uploading, and we can pretend no connection was made.
            if (MetaplaySDK.Connection.Endpoint.IsOfflineMode)
                return;

            lock (_lock)
            {
                _state = State.WaitingForIncidents;
                _nextProposalAt = DateTime.UtcNow + StartDelayAfterSessionStart;
            }
        }

        void OnDisconnectedFromServer(DisconnectedFromServer _)
        {
            lock (_lock)
            {
                _state = State.NoConnection;
            }

            // Cancel all pending uploads.
            foreach (TaskCompletionSource<PlayerIncidentUploadUrlResponse> tcs in _pendingUploadUrlResponse.Values)
                tcs.SetCanceled();
            _pendingUploadUrlResponse.Clear();
        }

        void OnPlayerRequestIncidentReportUploads(PlayerRequestIncidentReportUploads uploadRequest)
        {
            HashSet<string> reportsToDelete = new HashSet<string>();
            lock (_lock)
            {
                _debugDiagnostics.TotalUploadRequestMessages++;
                _debugDiagnostics.TotalRequestedIncidents += uploadRequest.IncidentIds?.Count ?? 0;

                if (_state != State.ProposingUploads)
                    return;

                // Server replied which incidents it is interested in. This means the incidents not on the
                // list are not interesting and should be removed.

                HashSet<string> interestingReports = new HashSet<string>(uploadRequest.IncidentIds);
                foreach (IncidentRepository.PendingIncident proposedUpload in _proposedUploads)
                {
                    if (!interestingReports.Contains(proposedUpload.IncidentId))
                        reportsToDelete.Add(proposedUpload.IncidentId);
                }

                _state = State.NextUpload;
                _proposedUploads = null;
                _nextUploadAt = DateTime.UtcNow;
                _incidentsToUpload = new Queue<string>(uploadRequest.IncidentIds);
            }

            foreach (string incidentId in reportsToDelete)
                MetaplaySDK.IncidentRepository.Remove(incidentId);
        }

        void OnPlayerAckIncidentReportUpload(PlayerAckIncidentReportUpload ackUpload)
        {
            _log.Info("Acknowledge upload of incident report {IncidentId}, deleting it..", ackUpload.IncidentId);
            MetaplaySDK.IncidentRepository.Remove(ackUpload.IncidentId);

            lock (_lock)
            {
                if (_state != State.Uploading)
                    return;
                if (_ongoingUploadIncident != ackUpload.IncidentId)
                    return;

                _debugDiagnostics.AcknowledgedIncidents++;

                _state = State.NextUpload;
                _ongoingUploadIncident = null;
                _nextUploadAt = DateTime.UtcNow + UploadCooldown;
            }
        }

        void TryUploadIncidentOnBackground(string incidentId)
        {
            _ = MetaTask.Run(async () =>
            {
                PlayerIncidentReport incident = await MetaplaySDK.IncidentRepository.TryGetReportAsync(incidentId);
                if (incident == null)
                {
                    _log.Warning("Server requested incident report {IncidentId} is no longer available, ignored.", incidentId);

                    // Move to next upload
                    lock (_lock)
                    {
                        _debugDiagnostics.UploadUnavailable++;

                        if (_state == State.Uploading)
                            _state = State.NextUpload;
                        _nextUploadAt = DateTime.UtcNow + UploadFailureCooldown;
                    }
                    return;
                }

                try
                {
                    byte[] compressedPayload = PlayerIncidentUtil.CompressIncidentForNetworkDelivery(incident);

                    // Send report. Server replies with ack.
                    await MetaTask.Run(async () => await UploadPayloadOnUnityThreadAsync(incidentId, compressedPayload), MetaTask.UnityMainScheduler);

                    // Wait for the ack
                    lock (_lock)
                    {
                        _debugDiagnostics.UploadsSent++;
                    }
                }
                catch (PlayerIncidentUtil.IncidentReportTooLargeException tooLarge)
                {
                    _log.Warning("Failed to compress report: {Error}", tooLarge);
                    _debugDiagnostics.UploadTooLarge++;

                    // Too large incident report. Remove report now...
                    MetaplaySDK.IncidentRepository.Remove(incidentId);

                    // Move to next upload
                    lock (_lock)
                    {
                        if (_state == State.Uploading)
                            _state = State.NextUpload;
                        _nextUploadAt = DateTime.UtcNow + UploadFailureCooldown;
                    }

                    // ... and create a new incident for the too large incident.
                    MetaplaySDK.IncidentTracker.ReportIncidentReportTooLarge(incident);
                }
                catch (IncidentAlreadyUploadedException)
                {
                    // Already completed. Remove report.
                    MetaplaySDK.IncidentRepository.Remove(incidentId);

                    // Move to next upload as if this was successful
                    lock (_lock)
                    {
                        if (_state == State.Uploading)
                            _state = State.NextUpload;
                        _nextUploadAt = DateTime.UtcNow + UploadCooldown;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        void OnPlayerIncidentUploadUrlResponse(PlayerIncidentUploadUrlResponse response)
        {
            if (_pendingUploadUrlResponse.Remove(response.QueryId, out TaskCompletionSource<PlayerIncidentUploadUrlResponse> tcs))
            {
                tcs.SetResult(response);
            }
        }

        async Task UploadPayloadOnUnityThreadAsync(string incidentId, byte[] compressedPayload)
        {
            string uploadUrl = null;

            // In WebGL, HTTP upload requires special CORS rules that arent implemented
            // yet in the server. Ignore for now. On other platforms, we request a upload
            // slot.
            bool maySupportIncidentHttpUpload =
            #if UNITY_WEBGL
                false
            #else
                true
            #endif
                ;

            if (maySupportIncidentHttpUpload)
            {
                int responseId = _nextUploadUrlRequestId++;
                _pendingUploadUrlResponse[responseId] = new TaskCompletionSource<PlayerIncidentUploadUrlResponse>();
                _messageDispatcher.SendMessage(new PlayerIncidentUploadUrlRequest(responseId, incidentId, compressedPayload.Length));

                // \note: If connection is lost, this will be cancelled.
                PlayerIncidentUploadUrlResponse response = await _pendingUploadUrlResponse[responseId].Task;

                if (response.AlreadyUploaded)
                    throw new IncidentAlreadyUploadedException();

                uploadUrl = response.Url;
            }

            if (uploadUrl != null)
            {
                try
                {
                    UnityWebRequest uploadRequest = UnityWebRequest.Put(uploadUrl, compressedPayload);
                    await uploadRequest.SendWebRequest();
                    if (uploadRequest.result == UnityWebRequest.Result.Success)
                    {
                        _messageDispatcher.SendMessage(new PlayerUploadedIncidentReportToUrl());
                        return;
                    }

                    _log.Warning("Failed to upload incident to S3: {Error}", uploadRequest.result);
                }
                catch (Exception ex)
                {
                    _log.Warning("Failed to upload incident to S3: {Error}", ex);
                }
            }

            _messageDispatcher.SendMessage(new PlayerUploadIncidentReport(incidentId, compressedPayload));
        }
    }
}
