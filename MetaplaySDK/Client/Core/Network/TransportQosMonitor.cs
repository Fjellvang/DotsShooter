// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Client.Messages;
using Metaplay.Core.Message;
using System;
using System.Collections.Generic;

namespace Metaplay.Core.Network
{
    /// <summary>
    /// Provides a simple Healthy/Not-healthy assesment by reading MessageTransport Info stream.
    /// </summary>
    public class TransportQosMonitor
    {
        public interface IWarningSource
        {
            /// <summary>
            /// Returns developer-readable description of why connection is unhealthy. If connection
            /// is healthy, returns <c>null</c>
            /// </summary>
            public string GetUnhealthyReason();
        }

        readonly LogChannel _log;

        bool _isWriteHealthy;
        bool _isReadHealthy;
        bool _hasHandshakedConnection;
        bool _hasTransport;
        int _lastSentSessionResumptionPingId;
        int _lastReceivedSessionResumptionPongId;
        int _sessionResumptionPingIdCounter;
        DateTime _lastSessionResumptionPingSentAt;
        bool _hasBeenHealthy;
        HashSet<IWarningSource> _externalSources;

        public bool IsHealthy { get; private set; }

        /// <summary>
        /// True, if the ping-pong that is started after session resume is ongoing.
        /// False, if the ping-pong has completed or if there has been no session resume.
        /// </summary>
        public bool IsSessionResumptionPingPongOngoing => _lastReceivedSessionResumptionPongId != _lastSentSessionResumptionPingId;

        public TransportQosMonitor(LogChannel log)
        {
            _log = log;
            _externalSources = new HashSet<IWarningSource>();
            Reset();
        }

        public void Reset()
        {
            IsHealthy = false;
            _isWriteHealthy = true;
            _isReadHealthy = true;
            _hasHandshakedConnection = false;
            _hasTransport = false;
            _lastSentSessionResumptionPingId = 0;
            _lastReceivedSessionResumptionPongId = 0;
            _sessionResumptionPingIdCounter = 0;
            _lastSessionResumptionPingSentAt = DateTime.UnixEpoch;
            _hasBeenHealthy = false;
            _externalSources.Clear();
        }

        public int GetOngoingSessionResumptionPingId() => _lastSentSessionResumptionPingId;

        public DateTime GetOngoingSessionResumptionPingSentAt() => _lastSessionResumptionPingSentAt;

        /// <summary>
        /// Updates internal model from messages and current timestamp. Returns <c>true</c> if QoS assesment changed
        /// during this call.
        /// </summary>
        public bool ProcessMessages(List<MetaMessage> messages, ServerConnection serverConnection)
        {
            foreach (MetaMessage message in messages)
            {
                switch (message)
                {
                    case MessageTransportInfoWrapperMessage wrapper:
                    {
                        switch (wrapper.Info)
                        {
                            case StreamMessageTransport.ReadDurationWarningInfo readDurationWarning:
                                _isReadHealthy = readDurationWarning.IsEnd;
                                break;

                            case StreamMessageTransport.WriteDurationWarningInfo writeDurationWarning:
                                _isWriteHealthy = writeDurationWarning.IsEnd;
                                break;

                            case ServerConnection.TransportLifecycleInfo transportEvent:
                            {
                                // reset transport's read/write healths when transport dies/spawns.
                                _isReadHealthy = true;
                                _isWriteHealthy = true;
                                _hasTransport = transportEvent.IsTransportAttached;
                                break;
                            }

                            case ServerConnection.SessionConnectionErrorLostInfo _:
                            {
                                _hasHandshakedConnection = false;
                                break;
                            }
                        }
                        break;
                    }

                    case SessionProtocol.SessionStartSuccess _:
                    {
                        _hasHandshakedConnection = true;
                        break;
                    }

                    case SessionProtocol.SessionResumeSuccess _:
                    {
                        _hasHandshakedConnection = true;

                        // Send ping. The connection will remain considered "unhealthy" until we get the pong.
                        //
                        // The purpose of this session resumption ping-ponging is to improve UI upon certain
                        // connectivity issues where session messages are not received by the server, yet logins
                        // and session resumptions succeed. In that case, we wish to indicate connection unhealthiness
                        // despite the apparent success in connection (i.e. successful login and resumption).
                        //
                        // Since the ping-pong happens on the session messaging level, receiving the pong
                        // also implies that the server has received all of our session messages preceding the ping.
                        //
                        int pingId = StartNewSessionResumePing();
                        serverConnection.EnqueueSendMessage(new SessionPing(pingId));
                        _lastSessionResumptionPingSentAt = DateTime.UtcNow;
                        break;
                    }

                    case SessionPong pong:
                    {
                        _lastReceivedSessionResumptionPongId = pong.Id;
                        break;
                    }
                }
            }

            bool oldIsHealthy = IsHealthy;
            IsHealthy = _isWriteHealthy && _isReadHealthy && _hasHandshakedConnection && _hasTransport && !IsSessionResumptionPingPongOngoing;

            // If main connection is fine, poll the external sources
            string externalReason = null;
            if (IsHealthy)
            {
                foreach (IWarningSource source in _externalSources)
                {
                    externalReason = source.GetUnhealthyReason();
                    if (externalReason != null)
                    {
                        IsHealthy = false;
                        break;
                    }
                }
            }

            // Nothing changed?
            if (IsHealthy == oldIsHealthy)
                return false;

            // Healthiness changed.
            if (IsHealthy)
            {
                // Don't print unless we have been healthy. This suppresses the initial (no connection: no healthy) -> (initial connection: healthy)
                // transition. It's the session handshake completing, and that is not noteworthy.
                if (_hasBeenHealthy)
                    _log.Warning("QoS: Connection is now healthy.");
                _hasBeenHealthy = true;
            }
            else
            {
                // Check from most general to most specific
                string reason = "unknown";
                if (!_hasHandshakedConnection)                  reason = "The connection (TCP/TLS/WSS) to the backend has been lost";
                else if (!_hasTransport)                        reason = "The low-level connection (TCP/TLS/WSS) to the backend has been lost";
                else if (!_isWriteHealthy)                      reason = "Writing data to the socket is taking too long";
                else if (!_isReadHealthy)                       reason = "Socket has not received any data in a while";
                else if (IsSessionResumptionPingPongOngoing)    reason = "Session resume pending messages haven't been ack'd by the backend yet";
                else if (externalReason != null)                reason = externalReason;

                _log.Warning("QoS: Connection has become unhealthy. Reason: {reason}.", reason);
            }

            return true;
        }

        /// <summary>
        /// Returns the PingId for the resumption.
        /// </summary>
        int StartNewSessionResumePing()
        {
            int pingId = ++_sessionResumptionPingIdCounter;
            _lastSentSessionResumptionPingId = pingId;
            return pingId;
        }

        /// <summary>
        /// Register an external warning source. External sources are automatically unregistered when a new session is started.
        /// </summary>
        public void RegisterSource(IWarningSource source)
        {
            _externalSources.Add(source);
        }

        public void UnregisterSource(IWarningSource source)
        {
            _externalSources.Remove(source);
        }
    }
}
