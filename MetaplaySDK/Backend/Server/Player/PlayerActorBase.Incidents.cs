// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Utility;
using Metaplay.Core.Debugging;
using Metaplay.Core.Player;
using Metaplay.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Metaplay.Server
{
    public partial class PlayerActorBase<TModel, TPersisted>
    {
        /// <summary>
        /// Short-term memory (actor lifetime) to avoid re-requesting already uploaded data.
        /// </summary>
        HashSet<string> _recentlyUploadedIncidents = new HashSet<string>();

        /// <summary>
        /// (Unfinished) upload url allocated for the client. Reset on session start.
        /// </summary>
        string _pendingIncidentUploadDownloadBucketKey;
        string _pendingIncidentUploadIncidentId;
        void ResetIncidentUploadStateOnSessionEnd()
        {
            _pendingIncidentUploadDownloadBucketKey = null;
            _pendingIncidentUploadIncidentId = null;
        }

        // \note: Client limits uploads to 1 per 10 second. Due to clocks not being synchronized, we may get 0..2 events per cycle.
        PeriodThrottle _incidentUploadUrlThrottle = new PeriodThrottle(TimeSpan.FromSeconds(10), maxEventsPerDuration: 2, DateTime.UtcNow);

        /// <summary>
        /// Client has some player incident reports available. Figure out which ones are of interest and
        /// should be uploaded to server.
        /// </summary>
        [PubSubMessageHandler]
        void HandlePlayerAvailableIncidentReports(EntitySubscriber session, PlayerAvailableIncidentReports msg)
        {
            static string IncidentInfoForLog(ClientAvailableIncidentReport info)
            {
                return $"{{ Id={info.IncidentId}, Type={info.Type}, SubType={info.SubType}, Reason={PlayerIncidentUtil.TruncateReason(info.Reason)} }}";
            }

            PlayerDebugIncidentUploadMode debugMode = Model.SessionDebugModeOverride?.IncidentUploadMode ?? PlayerDebugIncidentUploadMode.Normal;

            if (debugMode == PlayerDebugIncidentUploadMode.SilentlyOmitUploads)
            {
                // Debug-overridden to not even respond to the client.
                _log.Info($"Using {nameof(PlayerDebugIncidentUploadMode)}.{debugMode}; not responding to {nameof(PlayerAvailableIncidentReports)}: {string.Join(", ", msg.IncidentHeaders.Select(IncidentInfoForLog))}");
                return;
            }
            else if (debugMode == PlayerDebugIncidentUploadMode.RejectIncidents)
            {
                // Debug-overridden to reject all incidents.
                _log.Info($"Using {nameof(PlayerDebugIncidentUploadMode)}.{debugMode}; rejecting all incidents in {nameof(PlayerAvailableIncidentReports)}: {string.Join(", ", msg.IncidentHeaders.Select(IncidentInfoForLog))}");
                SendMessage(session, new PlayerRequestIncidentReportUploads(new List<string>()));
                return;
            }

            // Resolve all incidentIds that we are interested in
            // \todo [petri] collect statistics about all incidents (even the ones we don't want uploaded)
            List<string> uploadIncidentIds = new List<string>();
            foreach (ClientAvailableIncidentReport header in msg.IncidentHeaders)
            {
                // If already handled, ack (so client removes it)
                if (_recentlyUploadedIncidents.Contains(header.IncidentId))
                {
                    SendMessage(session, new PlayerAckIncidentReportUpload(header.IncidentId));
                    continue;
                }

                // Compute fingerprint (not used yet)
                // \todo [petri] if already have enough of this incident type, just accumulate count (further reports likely not useful)
                //string reason = PlayerIncidentUtil.TruncateReason(header.Reason);
                //string fingerprint = PlayerIncidentUtil.ComputeFingerprint(header.Type, header.SubType, reason);

                // Check if the report is of interest, and we should ask client to upload it
                int uploadPercentage = PlayerIncidentStorage.GetUploadPercentage(_log, header);
                int roll = (int)((ulong)MiniMD5.ComputeMiniMD5(header.IncidentId) * 100 >> 32);
                bool shouldUpload = roll < uploadPercentage;

                if (shouldUpload)
                    uploadIncidentIds.Add(header.IncidentId);
            }

            // Request client to upload incidents of interest, even if there were none.
            SendMessage(session, new PlayerRequestIncidentReportUploads(uploadIncidentIds));
        }

        /// <summary>
        /// Client uploads an incident with session protocol.
        /// </summary>
        [PubSubMessageHandler]
        async Task HandlePlayerUploadIncidentReport(EntitySubscriber session, PlayerUploadIncidentReport upload)
        {
            await AddIncidentReportAsync(upload.Payload, PlayerIncidentStorage.IncidentSource.GameProtocol);

            // Ack report, so client can delete it
            // \note Report is acked even if it wasn't accepted, so client won't keep sending it again
            SendMessage(session, new PlayerAckIncidentReportUpload(upload.IncidentId));
        }

        /// <summary>
        /// Some server component wants to add an incident to player.
        /// </summary>
        [MessageHandler]
        async Task HandleInternalPlayerAddIncidentReport(InternalPlayerAddIncidentReport addIncident)
        {
            await AddIncidentReportAsync(addIncident.Payload, PlayerIncidentStorage.IncidentSource.ServerGenerated);
        }

        /// <summary>
        /// Some server component has already added an incident and wants to inform player.
        /// </summary>
        [EntityAskHandler]
        EntityAskOk HandleNewPlayerIncidentRecorded(InternalNewPlayerIncidentRecorded incident)
        {
            // Player incident report has been persisted outside of PlayerActor

            string fingerprint = PlayerIncidentUtil.ComputeFingerprint(incident.Type, incident.SubType, incident.Reason);

            // Analytics event
            Model.EventStream.Event(new PlayerEventIncidentRecorded(incident.IncidentId, incident.OccurredAt, incident.Type, incident.SubType, incident.Reason, fingerprint));

            // Inform integration
            OnPlayerIncidentRecorded(incident.IncidentId, incident.Type, incident.SubType, incident.Reason);

            _recentlyUploadedIncidents.Add(incident.IncidentId);

            return EntityAskOk.Instance;
        }

        /// <summary>
        /// An incident for this player has been found in S3.
        /// </summary>
        [MessageHandler]
        async Task HandleInternalPlayerIncidentReportFoundInS3Bucket(InternalPlayerIncidentReportFoundInS3Bucket found)
        {
            await TryHandleAndAckS3UploadedIncident(found.IncidentId, found.BucketKey, PlayerIncidentStorage.IncidentSource.OrphanHttpUpload);
        }

        /// <summary>
        /// Client requests for a http upload url.
        /// </summary>
        [PubSubMessageHandler]
        void HandlePlayerIncidentUploadUrlRequest(EntitySubscriber session, PlayerIncidentUploadUrlRequest request)
        {
            if (_recentlyUploadedIncidents.Contains(request.IncidentId))
            {
                SendMessage(session, new PlayerIncidentUploadUrlResponse(queryId: request.QueryId, alreadyUploaded: true, url: null));
                return;
            }
            else if (!PlayerIncidentUtil.IsValidIncidentId(request.IncidentId))
            {
                _log.Warning("Client supplied invalid incident id: {IncidentId}", request.IncidentId);
                // fallthrough
            }
            else if (request.ContentLength <= 0 || request.ContentLength > PlayerUploadIncidentReport.MaxCompressedPayloadSize)
            {
                _log.Warning("Client supplied incident upload request with bad content length: {ContentLength}", request.ContentLength);
                // fallthrough
            }
            else if (!_incidentUploadUrlThrottle.TryTrigger(DateTime.UtcNow))
            {
                _log.Warning("Client request for a new incident upload url was throttled");
                // fallthrough
            }
            else
            {
                // \note: _pendingIncidentUploadDownloadBucketKey and _pendingIncidentUploadIncidentId may be non-null here
                //        if client failed to upload to the URL. We allow client to create a new url, subject to the above
                //        throttling.

                (string incidentDownloadBucketKey, string incidentUploadUrl) = PlayerIncidentUploadStorageService.GetInstance().TryCreateIncidentUploadUrl(_entityId, request.IncidentId, request.ContentLength);

                if (incidentUploadUrl != null)
                {
                    _pendingIncidentUploadDownloadBucketKey = incidentDownloadBucketKey;
                    _pendingIncidentUploadIncidentId = request.IncidentId;
                    SendMessage(session, new PlayerIncidentUploadUrlResponse(queryId: request.QueryId, alreadyUploaded: false, url: incidentUploadUrl));
                    return;
                }

                // fallthrough
            }

            SendMessage(session, new PlayerIncidentUploadUrlResponse(queryId: request.QueryId, alreadyUploaded: false, url: null));
        }

        /// <summary>
        /// Client has uploaded an incident to S3.
        /// </summary>
        [MessageHandler]
        async Task HandlePlayerUploadedIncidentReportToUrl(PlayerUploadedIncidentReportToUrl uploaded)
        {
            if (_pendingIncidentUploadDownloadBucketKey == null)
            {
                // \note: not on Warning level. Client uploads on background and this might leak. It is harmless.
                _log.Info("Client reported an incident upload was complete but there was no incident upload ongoing");
                return;
            }

            await TryHandleAndAckS3UploadedIncident(_pendingIncidentUploadIncidentId, _pendingIncidentUploadDownloadBucketKey, PlayerIncidentStorage.IncidentSource.HttpUpload);
            _pendingIncidentUploadIncidentId = null;
            _pendingIncidentUploadDownloadBucketKey = null;
        }

        async Task TryHandleAndAckS3UploadedIncident(string incidentId, string downloadBucketKey, PlayerIncidentStorage.IncidentSource source)
        {
            string ackedIncidentId;

            // Check incident hasnt been uploaded already. Scanner and client uploads race and we want to
            // avoid double work.
            if (_recentlyUploadedIncidents.Contains(incidentId))
            {
                await PlayerIncidentUploadStorageService.GetInstance().DeleteIncidentAsync(_log, downloadBucketKey);
                ackedIncidentId = incidentId;
            }
            else
            {
                byte[] payload = await PlayerIncidentUploadStorageService.GetInstance().TryDownloadAndDeleteIncidentAsync(_log, downloadBucketKey);
                if (payload != null)
                    ackedIncidentId = await AddIncidentReportAsync(payload, source);
                else
                    ackedIncidentId = null;
            }

            // Ack report, so client can delete it
            // \note Report is acked even if client didn't send this request
            // \note: If we fail to parse or download the report, we cannot ack. We'll try
            //        again next session.
            EntitySubscriber session = TryGetOwnerSession();
            if (session != null && ackedIncidentId != null)
                SendMessage(session, new PlayerAckIncidentReportUpload(ackedIncidentId));
        }

        async Task<string> AddIncidentReportAsync(byte[] reportPayload, PlayerIncidentStorage.IncidentSource source)
        {
            string incidentId = null;

            try
            {
                // Uncompress & decode the payload
                PlayerIncidentReport report = PlayerIncidentUtil.DecompressNetworkDeliveredIncident(reportPayload, out int uncompressedPayloadSize);
                incidentId = report.IncidentId;
                _log.Debug("Received incident report: {IncidentReport} ({CompressedSize} bytes compressed, {UncompressedSize} bytes uncompressed)", report.GetType().ToGenericTypeString(), reportPayload.Length, uncompressedPayloadSize);

                // Persist
                await PlayerIncidentStorage.PersistIncidentAsync(_entityId, report, reportPayload, source);

                string reason = PlayerIncidentUtil.TruncateReason(report.GetReason());
                string fingerprint = PlayerIncidentUtil.ComputeFingerprint(report.Type, report.SubType, reason);

                // Analytics event
                Model.EventStream.Event(new PlayerEventIncidentRecorded(report.IncidentId, report.OccurredAt, report.Type, report.SubType, reason, fingerprint));

                // Inform integration
                OnPlayerIncidentRecorded(report.IncidentId, report.Type, report.SubType, reason);

                // Mark incident as uploaded
                _recentlyUploadedIncidents.Add(incidentId);
            }
            catch (Exception ex)
            {
                _log.Error("Failed to persist PlayerIncidentReport: {Exception}", ex);
            }

            return incidentId;
        }

        /// <summary>
        /// PlayerActor hook for when a new player incident report has been recorded. Will also be called when the incident
        /// report is received as part of a rejected session start.
        /// </summary>
        protected virtual void OnPlayerIncidentRecorded(string incidentId, string type, string subType, string reason) { }
    }
}
