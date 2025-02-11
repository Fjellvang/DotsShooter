// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Client.Messages;
using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.Debugging;
using Metaplay.Core.Message;
using Metaplay.Core.Network;
using Metaplay.Core.Player;
using Metaplay.Core.Session;
using Metaplay.Unity.ConnectionStates;
using Metaplay.Unity.DefaultIntegration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Metaplay.Unity.IncidentReporting
{
    /// <summary>
    /// Client-side incident reporting service. Keeps rotating buffer of latest Unity logs.
    /// </summary>
    public class IncidentTracker
    {
        LogChannel              _log;
        PeriodThrottle          _incidentThrottle;          // Throttle to avoid incident report spam

        UnitySystemInfo         _unitySystemInfo;
        UnityPlatformInfo       _unityPlatformInfo;
        IncidentGameConfigInfo  _latestGameConfigInfo;
        string                  _latestTlsPeerDescription;
        SessionToken            _sessionToken;
        bool                    _connectionErrorReportedForCurrentConnectionAttempt;
        DateTime                _nextDroppedUnhandledExceptionsLogPrintAt;
        int                     _numDroppedUnhandledExceptionsTotal;
        int                     _numDroppedUnhandledExceptionsLastTime;

        UnityLogHistoryTracker  _logHistoryTracker;

        List<PlayerEventIncidentRecorded> _pendingAnalyticsEvents = new List<PlayerEventIncidentRecorded>();
        object _pendingAnalyticsEventsLock = new object();

        public IncidentTracker()
        {
            _log = MetaplaySDK.Logs.Incidents;
            _incidentThrottle = new PeriodThrottle(TimeSpan.FromSeconds(60), maxEventsPerDuration: 5, DateTime.UtcNow); // 5 events per minute
            _unitySystemInfo = UnitySystemInfo.Collect();
            _unityPlatformInfo = UnityPlatformInfo.Collect();

            _logHistoryTracker = new UnityLogHistoryTracker();
            _logHistoryTracker.OnExceptionLogEntry += OnExceptionLogEntry;
            _logHistoryTracker.Start();

            MetaplaySDK.MessageDispatcher.AddListener<ConnectedToServer>(OnConnectedToServer);
            MetaplaySDK.MessageDispatcher.AddListener<SessionProtocol.SessionStartSuccess>(OnSessionStartSuccess);
        }

        public void UpdateEarly()
        {
            // Update dynamically changing values of unitySystemInfo and unityPlatformInfo
            // \todo [petri] These can in theory be used while being updated, so should protect them with a lock to be extra-safe
            DateTime now = DateTime.UtcNow;
            _unitySystemInfo.UpdateDynamic(now);
            _unityPlatformInfo.UpdateDynamic(now);
        }

        public void UpdateAfterConnection()
        {
            // Create an incident report for the issue
            if (MetaplaySDK.Connection.State is ConnectionStates.ErrorState errorState && IsReportableErrorState(errorState))
            {
                if (!_connectionErrorReportedForCurrentConnectionAttempt)
                {
                    _connectionErrorReportedForCurrentConnectionAttempt = true;

                    BeginReportNetworkError(errorState, MetaplaySDK.Connection.Endpoint, MetaplaySDK.Connection.LatestTlsPeerDescription);
                }
            }
            else
            {
                _connectionErrorReportedForCurrentConnectionAttempt = false;
            }

            // Log dropped incidents once a second.
            DateTime now = DateTime.UtcNow;
            if (now >= _nextDroppedUnhandledExceptionsLogPrintAt)
            {
                int numIncidentsDropped = _numDroppedUnhandledExceptionsTotal;
                int newDrops = numIncidentsDropped - _numDroppedUnhandledExceptionsLastTime;
                _numDroppedUnhandledExceptionsLastTime = numIncidentsDropped;
                _nextDroppedUnhandledExceptionsLogPrintAt = now + TimeSpan.FromSeconds(1);

                if (newDrops > 0)
                {
                    _log.Warning("Too many exceptions. Dropped {NumIncidents} UnhandledException incident report(s) due to throttling", newDrops);
                }
            }
        }

        public IEnumerable<PlayerEventIncidentRecorded> GetAndClearPendingAnalyticsEvents()
        {
            lock (_pendingAnalyticsEventsLock)
            {
                if (_pendingAnalyticsEvents.Count == 0)
                {
                    // Avoid allocation in common case.
                    return Enumerable.Empty<PlayerEventIncidentRecorded>();
                }
                else
                {
                    List<PlayerEventIncidentRecorded> result = _pendingAnalyticsEvents;
                    _pendingAnalyticsEvents = new List<PlayerEventIncidentRecorded>();
                    return result;
                }
            }
        }

        public void Dispose()
        {
            _logHistoryTracker?.Dispose();
            _logHistoryTracker = null;
        }

        /// <summary>
        /// Issues an incident report for the unhandled exception. Thread safe.
        /// </summary>
        public void ReportUnhandledException(Exception ex)
        {
            DoReportUnhandledException(exceptionMessage: ex.Message, stackTrace: ex.StackTrace, _logHistoryTracker.GetLogHistory());
        }

        /// <summary>
        /// Issues an incident report for WatchdogDeadlineExceededError. Thread safe.
        /// </summary>
        public void ReportWatchdogDeadlineExceededError()
        {
            // Throttle errors
            if (!_incidentThrottle.TryTrigger(DateTime.UtcNow))
            {
                _log.Warning("Dropping WatchdogDeadlineExceeded incident report due to throttling");
                return;
            }

            AddIncidentAndAnalyticsEvent(IncidentReportFactory.CreateReportForWatchdogDeadlineExceededError(
                GetSharedIncidentInfo(),
                _latestTlsPeerDescription),
                useThrottle: false);
        }

        public void ReportServerStatusHintCorrupt(byte[] bytes, int length)
        {
            if (!_incidentThrottle.TryTrigger(DateTime.UtcNow))
            {
                _log.Warning("Dropping ServerStatusHintCorrupt incident report due to throttling");
                return;
            }

            AddIncidentAndAnalyticsEvent(IncidentReportFactory.CreateReportForServerStatusHintCorrupt(
                bytes,
                length,
                _logHistoryTracker.GetLogHistory(),
                _unitySystemInfo,
                _unityPlatformInfo,
                _latestGameConfigInfo,
                CollectApplicationIncidentInfo(),
                _latestTlsPeerDescription),
                useThrottle: false);
        }

        public void ReportSessionPingPongDurationThresholdExceeded(LoginDebugDiagnostics debugDiagnostics, TimeSpan roundtripEstimate, ServerGateway serverGateway, int pingId, TimeSpan timeSincePing)
        {
            if (!_incidentThrottle.TryTrigger(DateTime.UtcNow))
            {
                _log.Warning("Dropping SessionPingPongDurationThresholdExceeded incident report due to throttling");
                return;
            }

            AddIncidentWithNetworkReportAndAnalyticsEvent(IncidentReportFactory.CreateReportForSessionPingPongDurationThresholdExceeded(
                GetSharedIncidentInfo(),
                debugDiagnostics,
                roundtripEstimate,
                serverGateway,
                _latestTlsPeerDescription,
                _sessionToken,
                pingId,
                timeSincePing),
                useThrottle: false);
        }

        /// <summary>
        /// Issues a session start failure error report. If reports are throttled, no report is created and this method returns <c>null</c>.
        /// </summary>
        public PlayerIncidentReport.SessionStartFailed ReportSessionStartFailure(ConnectionStates.ErrorState error, ServerEndpoint endpoint, string tlsPeerDescription, NetworkDiagnosticReport networkReport)
        {
            // Suppress next connection error as it will be reported already
            _connectionErrorReportedForCurrentConnectionAttempt = true;

            // Throttle errors
            if (!_incidentThrottle.TryTrigger(DateTime.UtcNow))
            {
                _log.Warning("Dropping SessionStartFailed incident report due to throttling");
                return null;
            }

            PlayerIncidentReport.SessionStartFailed report = IncidentReportFactory.CreateReportForSessionStartFailed(
                GetSharedIncidentInfo(),
                networkReport,
                error,
                endpoint,
                _latestTlsPeerDescription);
            AddIncidentAndAnalyticsEvent(report, useThrottle: false);
            return report;
        }

        public void ReportIncidentReportTooLarge(PlayerIncidentReport originalReport)
        {
            // Throttle errors
            if (!_incidentThrottle.TryTrigger(DateTime.UtcNow))
            {
                _log.Warning("Dropping IncidentReportTooLarge incident report due to throttling");
                return;
            }

            AddIncidentAndAnalyticsEvent(IncidentReportFactory.CreateReportForIncidentReportTooLarge(
                _unitySystemInfo,
                _unityPlatformInfo,
                _latestGameConfigInfo,
                CollectApplicationIncidentInfo(),
                originalReport),
                useThrottle: false);
        }

        public void ReportCompanyIdLoginError(string phase, string message, Exception error = null)
        {
            if (!_incidentThrottle.TryTrigger(DateTime.UtcNow))
            {
                _log.Warning("Dropping CompanyIdLoginError incident report due to throttling");
                return;
            }

            AddIncidentAndAnalyticsEvent(IncidentReportFactory.CreateReportForCompanyIdLoginError(
                GetSharedIncidentInfo(),
                phase,
                message,
                error),
                useThrottle: false);
        }

        public void ReportPlayerChecksumMismatch(
            int actionNdx,
            long conflictTick,
            string playerModelDiff,
            List<string> vagueDifferencePathsMaybe,
            PlayerActionBase playerAction)
        {
            if (!_incidentThrottle.TryTrigger(DateTime.UtcNow))
            {
                _log.Warning("Dropping PlayerChecksumMismatch incident report due to throttling");
                return;
            }

            AddIncidentAndAnalyticsEvent(new PlayerIncidentReport.PlayerChecksumMismatch(
                GetSharedIncidentInfo(),
                actionNdx,
                conflictTick,
                playerModelDiff,
                vagueDifferencePathsMaybe,
                playerAction),
                useThrottle: false);
        }

        public PlayerIncidentReport.SharedIncidentInfo GetSharedIncidentInfo()
        {
            MetaTime occurredAt = MetaTime.Now;
            return new PlayerIncidentReport.SharedIncidentInfo()
            {
                OccurredAt         = occurredAt,
                ApplicationInfo    = CollectApplicationIncidentInfo(),
                ClientLogEntries   = _logHistoryTracker.GetLogHistory(),
                ClientPlatformInfo = _unityPlatformInfo,
                ClientSystemInfo   = _unitySystemInfo,
                GameConfigInfo     = _latestGameConfigInfo,
            };
        }

        void BeginReportNetworkError(ConnectionStates.ErrorState error, ServerEndpoint endpoint, string tlsPeerDescription)
        {
            // Throttle errors
            if (!_incidentThrottle.TryTrigger(DateTime.UtcNow))
            {
                _log.Warning("Dropping NetworkError incident report due to throttling");
                return;
            }

            // Try to get network diagnostic report
            NetworkDiagnosticReport networkReport;
            bool                    doAsyncNetworkDiagReport;

            if (error is IHasNetworkDiagnosticReport errorWithReport)
            {
                if (errorWithReport.NetworkDiagnosticReport == null)
                {
                    // This error should have a diagnostics report but it was not attached.
                    // This means the diagnostics report could not be computed when the
                    // error was raised.
                    // We want to compute the diagnostics report, but it takes time and we
                    // don't want to delay writing the incident in case app is closed. So, write
                    // the incident without the report, and then when/if the network diagnostics
                    // complete, update it.
                    networkReport = null;
                    doAsyncNetworkDiagReport = true;
                }
                else
                {
                    networkReport = errorWithReport.NetworkDiagnosticReport;
                    doAsyncNetworkDiagReport = false;
                }
            }
            else
            {
                networkReport = null;
                doAsyncNetworkDiagReport = false;
            }

            MetaTime now = MetaTime.Now;
            PlayerIncidentReport.TerminalNetworkError incident = new PlayerIncidentReport.TerminalNetworkError(
                id:                     IncidentReportFactory.CreateIncidentId(now),
                occurredAt:             now,
                logEntries:             _logHistoryTracker.GetLogHistory(),
                systemInfo:             _unitySystemInfo,
                platformInfo:           _unityPlatformInfo,
                gameConfigInfo:         _latestGameConfigInfo,
                applicationInfo:        CollectApplicationIncidentInfo(),
                errorType:              error.GetType().Name,
                networkError:           PrettyPrint.Verbose(error).ToString(),
                reasonOverride:         error.TryGetReasonOverrideForIncidentReport(),
                endpoint:               endpoint,
                networkReachability:    _unityPlatformInfo?.InternetReachability,
                networkReport:          networkReport,
                tlsPeerDescription:     tlsPeerDescription);

            if (!doAsyncNetworkDiagReport)
            {
                // Complete report (with network diagnostics). Write and we are done
                AddIncidentAndAnalyticsEvent(incident, useThrottle: false);
            }
            else
            {
                // Partial report (missing network diagnostics). Write partial, wait for diagnostics, rewrite and report
                MetaplaySDK.IncidentRepository.AddOrUpdate(incident, isVisible: false);
                AddAnalyticsEvent(incident);

                MetaplaySDK.StartNewNetworkDiagnosticsReport((newNetworkReport) =>
                {
                    PlayerIncidentReport.TerminalNetworkError completeIncident = new PlayerIncidentReport.TerminalNetworkError(
                        id:                     incident.IncidentId,
                        occurredAt:             incident.OccurredAt,
                        logEntries:             incident.ClientLogEntries,
                        systemInfo:             incident.ClientSystemInfo,
                        platformInfo:           incident.ClientPlatformInfo,
                        gameConfigInfo:         incident.GameConfigInfo,
                        applicationInfo:        incident.ApplicationInfo,
                        errorType:              incident.ErrorType,
                        networkError:           incident.NetworkError,
                        reasonOverride:         incident.ReasonOverride,
                        endpoint:               incident.Endpoint,
                        networkReachability:    incident.NetworkReachability,
                        networkReport:          newNetworkReport,
                        tlsPeerDescription:     incident.TlsPeerDescription);

                    MetaplaySDK.IncidentRepository.AddOrUpdate(completeIncident, isVisible: true);
                });
            }
        }

        void OnConnectedToServer(ConnectedToServer _)
        {
            _latestGameConfigInfo = MetaplaySDK.Connection.LatestGameConfigInfo.ToIncidentInfo();
            _latestTlsPeerDescription = MetaplaySDK.Connection.LatestTlsPeerDescription;
        }

        void OnSessionStartSuccess(SessionProtocol.SessionStartSuccess loginSuccess)
        {
            _sessionToken = loginSuccess.SessionToken;
        }

        void OnExceptionLogEntry(string logString, string stackTrace, List<ClientLogEntry> logHistory)
        {
            DoReportUnhandledException(exceptionMessage: logString, stackTrace: stackTrace, logHistory);
        }

        void DoReportUnhandledException(string exceptionMessage, string stackTrace, List<ClientLogEntry> logHistory)
        {
            if (!_incidentThrottle.TryTrigger(DateTime.UtcNow))
            {
                // Unhandled exceptions are commonly spammy. Avoid contributing to spam and delay logging.
                _numDroppedUnhandledExceptionsTotal++;
                return;
            }

            AddIncidentAndAnalyticsEvent(IncidentReportFactory.CreateUnhandledException(
                exceptionMessage,
                stackTrace,
                GetSharedIncidentInfo()));
        }

        /// <summary>
        /// Add an incident report with an included network diagnostic report and an associated analytics event.
        /// Generating the network report can take up to 5 seconds. The incident is still persisted as a partial report
        /// immediately, and rewritten when the network report is completed (or failed).
        /// By default, incidents can be throttled if they are happening too frequently.
        /// Set <see cref="useThrottle"/> to false to bypass throttling. See also: <seealso cref="AddIncidentAndAnalyticsEvent"/>.
        /// </summary>
        /// <param name="report">The incident report to add.</param>
        /// <param name="useThrottle">If true, incident can be throttled if incidents are too frequent. If false, throttling is bypassed.</param>
        public void AddIncidentWithNetworkReportAndAnalyticsEvent(PlayerIncidentReport report, bool useThrottle = true)
        {
            if (useThrottle && !_incidentThrottle.TryTrigger(DateTime.UtcNow))
            {
                _log.Warning("Dropping {ReportType} incident report due to throttling", report.GetType().Name);
                return;
            }

            // Partial report (missing network diagnostics). Write partial, wait for diagnostics, rewrite and report
            MetaplaySDK.IncidentRepository.AddOrUpdate(report, isVisible: false);

            AddAnalyticsEvent(report);

            MetaplaySDK.StartNewNetworkDiagnosticsReport((newNetworkReport) =>
            {
                report.NetworkReport = newNetworkReport;
                MetaplaySDK.IncidentRepository.AddOrUpdate(report, isVisible: true);
            });
        }
        
        /// <summary>
        /// Add an incident report and an associated analytics event. By default, incidents can be throttled if they are happening too frequently.
        /// Set <see cref="useThrottle"/> to false to bypass throttling. Alternatively, use <see cref="IncidentRepository.AddOrUpdate"/>
        /// to only add an incident report without the associated analytics event. See also: <seealso cref="AddIncidentWithNetworkReportAndAnalyticsEvent"/>.
        /// </summary>
        /// <param name="report">The incident report to add.</param>
        /// <param name="useThrottle">If true, incident can be throttled if incidents are too frequent. If false, throttling is bypassed.</param>
        public void AddIncidentAndAnalyticsEvent(PlayerIncidentReport report, bool useThrottle = true)
        {
            if (useThrottle && !_incidentThrottle.TryTrigger(DateTime.UtcNow))
            {
                _log.Warning("Dropping {ReportType} incident report due to throttling", report.GetType().Name);
                return;
            }

            MetaplaySDK.IncidentRepository.AddOrUpdate(report);
            AddAnalyticsEvent(report);
        }

        void AddAnalyticsEvent(PlayerIncidentReport report)
        {
            PlayerEventIncidentRecorded ev = CreateIncidentAnalyticsEvent(report);
            lock (_pendingAnalyticsEventsLock)
                _pendingAnalyticsEvents.Add(ev);
        }

        PlayerEventIncidentRecorded CreateIncidentAnalyticsEvent(PlayerIncidentReport report)
        {
            string reason = PlayerIncidentUtil.TruncateReason(report.GetReason());
            string fingerprint = PlayerIncidentUtil.ComputeFingerprint(report.Type, report.SubType, reason);
            return new PlayerEventIncidentRecorded(report.IncidentId, report.OccurredAt, report.Type, report.SubType, reason, fingerprint);
        }

        static bool IsReportableErrorState(ConnectionStates.ErrorState state)
        {
            switch (state)
            {
                case ConnectionStates.TerminalError.InMaintenance _:
                    // no point to send reports if in Maintenance
                    return false;

                case ConnectionStates.TerminalError.LogicVersionMismatch _:
                case ConnectionStates.TerminalError.ClientPatchVersionTooOld _:
                    // These are usually not incidents. They are often expected, especially after a game update.
                    return false;

                case ConnectionStates.TransientError.SessionLostInBackground _:
                    // not really an error. App was just brought back to foreground.
                    return false;

                case ConnectionStates.TransientError.GameConfigUpdateRequired _:
                    // Not an error: client needs to reconnect for downloading updated config.
                    return false;

                case ClientTerminatedConnectionConnectionError _:
                    // not an error, client decided to terminate the connection
                    return false;

                case PlayerChecksumMismatchConnectionError _:
                    // Checksum mismatches create their own separate incident report
                    return false;

                default:
                    return true;
            }
        }

        IncidentApplicationInfo CollectApplicationIncidentInfo()
        {
            return new IncidentApplicationInfo(
                buildVersion:                   MetaplaySDK.BuildVersion,
                deviceGuid:                     MetaplaySDK.DeviceGuid,
                activeLanguage:                 MetaplaySDK.ActiveLanguage?.LanguageId.Value,
                highestSupportedLogicVersion:   MetaplayCore.Options.ClientLogicVersion,
                environmentId:                  MetaplaySDK.CurrentEnvironmentConfig.Id ?? ""
                );
        }
    }
}
