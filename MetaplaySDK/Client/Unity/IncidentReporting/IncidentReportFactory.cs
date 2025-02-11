// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Debugging;
using Metaplay.Core.Message;
using Metaplay.Core.Network;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using Metaplay.Core.Session;
using System;
using System.Collections.Generic;

namespace Metaplay.Unity.IncidentReporting
{
    public static class IncidentReportFactory
    {
        /// <summary>
        /// Try to extract the name of the exception from Unity log message for an Exception.
        /// The input should be of the form 'InvalidOperationException: Message in the exception.'
        /// Returns 'Unknown' if cannot be matched.
        /// </summary>
        static string GetExceptionName(string message)
        {
            int offset = message.IndexOf(':');
            if (offset != -1 && offset < 100) // assume all exception names are shorter than 100 chars
                return message.Substring(0, offset);
            else
                return "Unknown";
        }

        /// <summary>
        /// Create an incident id based on the time of its occurrence. First 8 bytes are the milliseconds-since-epoch, followed by 8 bytes of random.
        /// </summary>
        public static string CreateIncidentId(MetaTime occurredAt) =>
            PlayerIncidentUtil.EncodeIncidentId(occurredAt, RandomPCG.CreateNew().NextULong());

        public static PlayerIncidentReport.UnhandledExceptionError CreateUnhandledException(string exceptionMessage, string stackTrace, PlayerIncidentReport.SharedIncidentInfo info)
        {
            // Limit very long stack traces to 8kB
            stackTrace = Util.ShortenString(stackTrace, 8 * 1024);
            
            return new PlayerIncidentReport.UnhandledExceptionError(
                sharedIncidentInfo:     info,
                exceptionName:          GetExceptionName(exceptionMessage),
                exceptionMessage:       exceptionMessage,
                stackTrace:             stackTrace);
        }

        public static PlayerIncidentReport.TerminalNetworkError CreateReportForBrokenReport(string incidentId, Exception error)
        {
            // \note: "network"error instead of unhandled exception since this is targeted for metaplay people
            return new PlayerIncidentReport.TerminalNetworkError(
                id:                     incidentId,
                occurredAt:             PlayerIncidentUtil.GetOccurredAtFromIncidentId(incidentId),
                logEntries:             new List<ClientLogEntry>() { new ClientLogEntry(MetaTime.Now, ClientLogEntryType.Exception, "Failed to parse incident", error.ToString() )},
                systemInfo:             null, // \note not available in broken-report reports
                platformInfo:           null,
                gameConfigInfo:         null,
                applicationInfo:        null,
                errorType:              "Metaplay.InternalReportBroken",
                networkError:           "Metaplay.InternalReportBroken",
                reasonOverride:         null,
                endpoint:               null,
                networkReachability:    null,
                networkReport:          null,
                tlsPeerDescription:     null);
        }

        public static PlayerIncidentReport.TerminalNetworkError CreateReportForWatchdogDeadlineExceededError(PlayerIncidentReport.SharedIncidentInfo info, string latestTlsPeerDescription)
        {
            return new PlayerIncidentReport.TerminalNetworkError(
                sharedIncidentInfo:     info,
                errorType:              "Metaplay.InternalWatchdogDeadlineExceeded",
                networkError:           "Metaplay.InternalWatchdogDeadlineExceeded",
                reasonOverride:         null,
                endpoint:               null,
                networkReachability:    info.ClientPlatformInfo?.InternetReachability,
                networkReport:          null,
                tlsPeerDescription:     latestTlsPeerDescription);
        }

        public static PlayerIncidentReport.TerminalNetworkError CreateReportForServerStatusHintCorrupt(byte[] bytes, int length, List<ClientLogEntry> logHistory, UnitySystemInfo unitySystemInfo, UnityPlatformInfo unityPlatformInfo, IncidentGameConfigInfo latestGameConfigInfo, IncidentApplicationInfo applicationInfo, string latestTlsPeerDescription)
        {
            // \note: "network"error instead of unhandled exception since this is targeted for metaplay people
            MetaTime occurredAt = MetaTime.Now;
            List<ClientLogEntry> augmentedLogs = new List<ClientLogEntry>();
            augmentedLogs.Add(new ClientLogEntry(occurredAt, ClientLogEntryType.Warning, "bytes: " + Convert.ToBase64String(bytes, 0, length), null));
            augmentedLogs.AddRange(logHistory);
            return new PlayerIncidentReport.TerminalNetworkError(
                id:                     CreateIncidentId(occurredAt),
                occurredAt:             occurredAt,
                logEntries:             augmentedLogs,
                systemInfo:             unitySystemInfo,
                platformInfo:           unityPlatformInfo,
                gameConfigInfo:         latestGameConfigInfo,
                applicationInfo:        applicationInfo,
                errorType:              "Metaplay.InternalServerStatusHintCorrupt",
                networkError:           "Metaplay.InternalServerStatusHintCorrupt",
                reasonOverride:         null,
                endpoint:               null,
                networkReachability:    unityPlatformInfo?.InternetReachability,
                networkReport:          null,
                tlsPeerDescription:     latestTlsPeerDescription);
        }

        public static PlayerIncidentReport.SessionCommunicationHanged CreateReportForSessionPingPongDurationThresholdExceeded(PlayerIncidentReport.SharedIncidentInfo info, LoginDebugDiagnostics debugDiagnostics, TimeSpan roundtripEstimate, ServerGateway serverGateway, string tlsPeerDescription, SessionToken sessionToken, int pingId, TimeSpan elapsedSinceCommunication)
        {
            return new PlayerIncidentReport.SessionCommunicationHanged(
                sharedIncidentInfo:         info,
                issueType:                  "Metaplay.SessionPingPongDurationThresholdExceeded",
                issueInfo:                  $"Metaplay.SessionPingPongDurationThresholdExceeded(pingId: {pingId})",
                debugDiagnostics:           debugDiagnostics,
                roundtripEstimate:          roundtripEstimate,
                serverGateway:              serverGateway,
                networkReachability:        info.ClientPlatformInfo?.InternetReachability,
                tlsPeerDescription:         tlsPeerDescription,
                pingId:                     pingId,
                sessionToken:               sessionToken,
                elapsedSinceCommunication:  elapsedSinceCommunication);
        }

        public static PlayerIncidentReport.SessionStartFailed CreateReportForSessionStartFailed(PlayerIncidentReport.SharedIncidentInfo info, NetworkDiagnosticReport networkReport, ConnectionStates.ErrorState error, ServerEndpoint endpoint, string tlsPeerDescription)
        {
            return new PlayerIncidentReport.SessionStartFailed(
                sharedIncidentInfo:     info,
                errorType:              error.GetType().Name,
                networkError:           PrettyPrint.Verbose(error).ToString(),
                reasonOverride:         error.TryGetReasonOverrideForIncidentReport(),
                endpoint:               endpoint,
                networkReachability:    info.ClientPlatformInfo?.InternetReachability,
                networkReport:          networkReport,
                tlsPeerDescription:     tlsPeerDescription);
        }

        public static PlayerIncidentReport.UnhandledExceptionError CreateReportForIncidentReportTooLarge(UnitySystemInfo unitySystemInfo, UnityPlatformInfo unityPlatformInfo, IncidentGameConfigInfo latestGameConfigInfo, IncidentApplicationInfo applicationInfo, PlayerIncidentReport originalReport)
        {
            // Try to gather info from original error
            string fakeStackTrace;
            try
            {
                byte[] reportAsBytes = MetaSerialization.SerializeTagged<PlayerIncidentReport>(originalReport, MetaSerializationFlags.IncludeAll, logicVersion: null);
                fakeStackTrace = "Metaplay.IncidentReportTooLarge\n"
                               + $"Type: {originalReport.Type}\n"
                               + $"SubType: {originalReport.SubType}\n"
                               + $"Serialized size: {reportAsBytes.Length}\n"
                               + $"Log entries: {originalReport.ClientLogEntries?.Count ?? 0}\n";
            }
            catch
            {
                fakeStackTrace = "Metaplay.IncidentReportTooLarge\n";
            }

            MetaTime occuredAt = MetaTime.Now;
            return new PlayerIncidentReport.UnhandledExceptionError(
                id:                     CreateIncidentId(occuredAt),
                occurredAt:             occuredAt,
                logEntries:             new List<ClientLogEntry>(),
                systemInfo:             unitySystemInfo,
                platformInfo:           unityPlatformInfo,
                gameConfigInfo:         latestGameConfigInfo,
                applicationInfo:        applicationInfo,
                exceptionName:          "Metaplay.IncidentReportTooLarge",
                exceptionMessage:       "",
                stackTrace:             fakeStackTrace);
        }

        public static PlayerIncidentReport.CompanyIdLoginError CreateReportForCompanyIdLoginError(PlayerIncidentReport.SharedIncidentInfo info, string phase, string message, Exception error)
        {
            string exceptionString;
            if (error != null)
                exceptionString = error.ToString();
            else
                exceptionString = "";

            MetaTime occurredAt = MetaTime.Now;
            return new PlayerIncidentReport.CompanyIdLoginError(
                sharedIncidentInfo:     info,
                phase:                  phase,
                message:                message,
                exception:              exceptionString);
        }
    }
}
