// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Localization;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using Metaplay.Core.Session;
using Metaplay.Core.TypeCodes;
using System;
using System.Collections.Generic;
using Metaplay.Core.MultiplayerEntity.Messages;
using Metaplay.Core.Client;
using System.Text;

namespace Metaplay.Core.Message
{
    /// <summary>
    /// Session protocol messages.
    /// </summary>
    public static class SessionProtocol
    {
        /// <summary>
        /// The client's proposal for session resources. Server may accept these
        /// or issue a correction (e.g. <see cref="SessionResourceCorrection"/>).
        /// </summary>
        [MetaSerializable]
        public struct SessionResourceProposal
        {
            [MetaMember(1)] public OrderedSet<ContentHash>   ConfigVersions              { get; private set; }
            [MetaMember(2)] public OrderedSet<ContentHash>   PatchVersions               { get; private set; }
            [MetaMember(10)] public LanguageId               ClientActiveLanguage        { get; private set; }
            [MetaMember(11)] public ContentHash              ClientLocalizationVersion   { get; private set; }

            public SessionResourceProposal(OrderedSet<ContentHash> configVersions, OrderedSet<ContentHash> patchVersions, LanguageId clientActiveLanguage, ContentHash clientLocalizationVersion)
            {
                ConfigVersions = configVersions;
                PatchVersions = patchVersions;
                ClientActiveLanguage = clientActiveLanguage;
                ClientLocalizationVersion = clientLocalizationVersion;
            }
        }

        /// <summary>
        /// The set of corrections for session resources. Issued by server if the proposed
        /// resources (in <see cref="SessionResourceProposal"/>) are not satisfactory.
        /// </summary>
        [MetaSerializable]
        public class SessionResourceCorrection
        {
            [MetaSerializable]
            public struct ConfigArchiveUpdateInfo
            {
                /// <summary>
                /// The config version the client should update to
                /// </summary>
                [MetaMember(1)] public ContentHash      SharedGameConfigVersion;

                /// <summary>
                /// The URL suffix for the download resource. May be null which means empty or no suffix.
                /// </summary>
                [MetaMember(2)] public string           UrlSuffix;

                public ConfigArchiveUpdateInfo(ContentHash sharedGameConfigVersion, string urlSuffix)
                {
                    if (sharedGameConfigVersion == ContentHash.None)
                        throw new ArgumentException("version must be set", nameof(sharedGameConfigVersion));

                    SharedGameConfigVersion = sharedGameConfigVersion;
                    UrlSuffix = urlSuffix;
                }

                public override string ToString()
                {
                    return SharedGameConfigVersion + UrlSuffix;
                }
            }

            [MetaSerializable(MetaSerializableFlags.ImplicitMembers)]
            [MetaImplicitMembersRange(1, 100)]
            public struct LanguageUpdateInfo
            {
                /// <summary>
                /// The language the client should use.
                /// </summary>
                public LanguageId       ActiveLanguage;

                /// <summary>
                /// The localization version the client should use.
                /// </summary>
                public ContentHash      LocalizationVersion;

                public LanguageUpdateInfo(LanguageId activeLanguage, ContentHash localizationVersion)
                {
                    ActiveLanguage = activeLanguage;
                    LocalizationVersion = localizationVersion;
                }

                public override string ToString()
                {
                    return ActiveLanguage + ":" + LocalizationVersion.ToString();
                }
            }

            [MetaSerializable]
            public struct ConfigPatchesUpdateInfo
            {
                /// <summary>
                /// The patch set version the client should update to.
                /// </summary>
                [MetaMember(1)] public ContentHash PatchesVersion;

                public ConfigPatchesUpdateInfo(ContentHash patchesVersion)
                {
                    PatchesVersion = patchesVersion;
                }

                public override string ToString()
                {
                    return PatchesVersion.ToString();
                }
            }

            /// <summary>
            /// If set, the configs the client should use.
            /// </summary>
            [MetaMember(1)] public List<ConfigArchiveUpdateInfo> ConfigUpdates;

            /// <summary>
            /// If set, the config patches each client should use
            /// </summary>
            [MetaMember(2)] public List<ConfigPatchesUpdateInfo> PatchUpdates;

            /// <summary>
            /// If set, the language the client should use.
            /// </summary>
            [MetaMember(3)] public LanguageUpdateInfo? LanguageUpdate;

            public SessionResourceCorrection()
            {
                ConfigUpdates = new List<ConfigArchiveUpdateInfo>();
                PatchUpdates = new List<ConfigPatchesUpdateInfo>();
                LanguageUpdate = null;
            }

            public bool HasAnyCorrection()
            {
                if (ConfigUpdates.Count > 0)
                    return true;
                if (PatchUpdates.Count > 0)
                    return true;
                if (LanguageUpdate.HasValue)
                    return true;
                return false;
            }

            static void AddGameConfig(SessionResourceCorrection correction, ConfigArchiveUpdateInfo info)
            {
                // Only one record in the list. Fetch urls do not matter.
                foreach (ConfigArchiveUpdateInfo config in correction.ConfigUpdates)
                {
                    if (config.SharedGameConfigVersion == info.SharedGameConfigVersion)
                        return;
                }
                correction.ConfigUpdates.Add(info);
            }

            static void AddPatch(SessionResourceCorrection correction, ConfigPatchesUpdateInfo info)
            {
                // Only one record in the list.
                foreach (ConfigPatchesUpdateInfo patch in correction.PatchUpdates)
                {
                    if (patch.PatchesVersion == info.PatchesVersion)
                        return;
                }
                correction.PatchUpdates.Add(info);
            }

            public static SessionResourceCorrection Combine(SessionResourceCorrection a, SessionResourceCorrection b)
            {
                SessionResourceCorrection combined = new SessionResourceCorrection();

                foreach (ConfigArchiveUpdateInfo update in a.ConfigUpdates)
                    AddGameConfig(combined, update);
                foreach (ConfigArchiveUpdateInfo update in b.ConfigUpdates)
                    AddGameConfig(combined, update);

                foreach (ConfigPatchesUpdateInfo update in a.PatchUpdates)
                    AddPatch(combined, update);
                foreach (ConfigPatchesUpdateInfo update in b.PatchUpdates)
                    AddPatch(combined, update);

                combined.LanguageUpdate = a.LanguageUpdate ?? b.LanguageUpdate;
                return combined;
            }

            public override string ToString()
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("ResourceCorrection{");

                foreach (ConfigArchiveUpdateInfo update in ConfigUpdates)
                {
                    builder.Append(" config:");
                    builder.Append(update.SharedGameConfigVersion);
                }

                foreach (ConfigPatchesUpdateInfo update in PatchUpdates)
                {
                    builder.Append(" patch:");
                    builder.Append(update.PatchesVersion);
                }

                if (LanguageUpdate != null)
                {
                    builder.Append(" lang:");
                    builder.Append(LanguageUpdate.Value.ActiveLanguage);
                    builder.Append("->");
                    builder.Append(LanguageUpdate.Value.LocalizationVersion);
                }

                builder.Append(" }");
                return builder.ToString();
            }
        }

        /// <summary>
        /// The set of information about a client device and/or environment collected at session start.
        /// </summary>
        [MetaSerializable]
        public struct ClientDeviceInfo
        {
            [MetaMember(1)] public ClientPlatform     ClientPlatform  { get; set; }
            // Human-readable device model (SystemInfo.deviceModel). Clamped to 256 characters on receive.
            [MetaMember(2)] public string             DeviceModel     { get; set; }
            [MetaMember(3)] public string             OperatingSystem { get; set; }
        }

        /// <summary>
        /// Request to create a new session.
        /// </summary>
        [MetaMessage(MessageCodesCore.SessionStartRequest, MessageDirection.ClientToServer, hasExplicitMembers: true), MessageRoutingRuleProtocol]
        public class SessionStartRequest : MetaMessage
        {
            [MetaMember(1)] public int                             QueryId                      { get; private set; }
            // Device unique identifier (MetaplaySDK.DeviceGuid). If not given or rejected by server due to not
            // matching existing per-device info a new device guid is generated and communicated to client via
            // `SessionStartSuccess.CorrectedDeviceGuid`.
            [MetaMember(2)] public string                          DeviceGuid                   { get; private set; }
            [MetaMember(3)] public ClientDeviceInfo                DeviceInfo                   { get; private set; }
            [MetaMember(4)] public PlayerTimeZoneInfo              TimeZoneInfo                 { get; private set; }
            [MetaMember(5)] public SessionResourceProposal         ResourceProposal             { get; private set; }
            [MetaMember(7)] public ISessionStartRequestGamePayload GamePayload                  { get; private set; }
            [MetaMember(8)] public CompressionAlgorithmSet         SupportedArchiveCompressions { get; private set; } // Supported archive compression modes
            [MetaMember(9)] public ClientAppPauseStatus            ClientAppPauseStatus         { get; private set; }
            [MetaMember(10)] public PlayerLocation?                OverrideLocation             { get; private set; }

            SessionStartRequest() { }
            public SessionStartRequest(int queryId, string deviceGuid, ClientDeviceInfo deviceInfo, PlayerTimeZoneInfo timeZoneInfo, SessionResourceProposal resourceProposal,
                ISessionStartRequestGamePayload gamePayload, CompressionAlgorithmSet supportedArchiveCompressions, ClientAppPauseStatus clientAppPauseStatus, PlayerLocation? overrideLocation)
            {
                QueryId = queryId;
                DeviceGuid = deviceGuid;
                DeviceInfo = deviceInfo;
                TimeZoneInfo = timeZoneInfo ?? throw new ArgumentNullException(nameof(timeZoneInfo));
                ResourceProposal = resourceProposal;
                GamePayload = gamePayload;
                SupportedArchiveCompressions = supportedArchiveCompressions;
                ClientAppPauseStatus = clientAppPauseStatus;
                OverrideLocation = overrideLocation;
            }
        }

        /// <summary>
        /// Request to abort session handshake. This may occur either before or just after SessionStartSuccess. The message
        /// leads into termination of the connection, except if HasReasonTrailer is set in which case SessionStartAbortReasonTrailer
        /// is the last message.
        /// </summary>
        [MetaMessage(MessageCodesCore.SessionStartAbort, MessageDirection.ClientToServer, hasExplicitMembers: true), MessageRoutingRuleProtocol]
        public class SessionStartAbort : MetaMessage
        {
            [MetaMember(1)] public bool HasReasonTrailer { get; private set; }

            SessionStartAbort() { }
            public SessionStartAbort(bool hasReasonTrailer)
            {
                HasReasonTrailer = hasReasonTrailer;
            }
        }

        /// <summary>
        /// Optional trailer to SessionStartAbort. This is separated from header message to better handle poor network conditions. This message can become
        /// large which increases the risk of failure. We don't want to lose the header which is critical information in order to differentiate network issues
        /// from client issues. But at the same time, this trailer is only opportunistically delivered data and failure is acceptable since normal incident
        /// delivery system will retry the delivery later. Hence, we deliver the message in two parts where the non-critical data is in the trailer.
        /// </summary>
        [MetaMessage(MessageCodesCore.SessionStartAbortReasonTrailer, MessageDirection.ClientToServer, hasExplicitMembers: true), MessageRoutingRuleProtocol]
        public class SessionStartAbortReasonTrailer : MetaMessage
        {
            [MetaMember(1)] public string   IncidentId  { get; private set; }
            [MetaMember(2)] public byte[]   Incident    { get; private set; } // Deflate-compressed, TaggedSerialized<PlayerIncidentReport>

            SessionStartAbortReasonTrailer() { }
            public SessionStartAbortReasonTrailer(string incidentId, byte[] incident)
            {
                IncidentId = incidentId;
                Incident = incident;
            }
        }

        [MetaSerializable]
        public class InitialPlayerState
        {
            [MetaMember(1)] public MetaSerialized<IPlayerModelBase> PlayerModel                 { get; private set; }
            [MetaMember(2)] public int                              CurrentOperation            { get; private set; } // timeline position is (Model.CurrentTick, CurrentOperation, 0)
            [MetaMember(3)] public EntityDebugConfig                DebugConfig                 { get; private set; }
            [MetaMember(4)] public ContentHash                      SharedGameConfigVersion     { get; private set; }
            [MetaMember(5)] public ContentHash                      SharedConfigPatchesVersion  { get; private set; }
            [MetaMember(6)] public EntityActiveExperiment[]         ActiveExperiments           { get; private set; }

            InitialPlayerState() { }
            public InitialPlayerState(MetaSerialized<IPlayerModelBase> playerModel, int currentOperation, EntityDebugConfig debugConfig, ContentHash sharedGameConfigVersion, ContentHash sharedConfigPatchesVersion, EntityActiveExperiment[] activeExperiments)
            {
                PlayerModel                 = playerModel;
                CurrentOperation            = currentOperation;
                DebugConfig                 = debugConfig;
                SharedGameConfigVersion     = sharedGameConfigVersion;
                SharedConfigPatchesVersion  = sharedConfigPatchesVersion;
                ActiveExperiments           = activeExperiments;
            }
        }

        [MetaMessage(MessageCodesCore.SessionStartSuccess, MessageDirection.ServerToClient, hasExplicitMembers: true)]
        public class SessionStartSuccess : MetaMessage
        {
            [MetaMember(1)] public int                                          QueryId                         { get; private set; }
            [MetaMember(2)] public int                                          LogicVersion                    { get; private set; }   // LogicVersion to use for the session.
            [MetaMember(3)] public SessionToken                                 SessionToken                    { get; private set; }
            [MetaMember(4)] public ScheduledMaintenanceModeForClient            ScheduledMaintenanceMode        { get; private set; }
            [MetaMember(5)] public EntityId                                     PlayerId                        { get; private set; }
            [MetaMember(6)] public InitialPlayerState                           PlayerState                     { get; private set; }
            [MetaMember(8)] public MetaDictionary<LanguageId, ContentHash>   LocalizationVersions            { get; private set; }
            [MetaMember(10)] public bool                                        DeveloperMaintenanceBypass      { get; private set; }
            [MetaMember(11)] public List<EntityInitialState>                    EntityStates                    { get; private set; }
            [MetaMember(12)] public ISessionStartSuccessGamePayload             GamePayload                     { get; private set; }
            // When the DeviceGuid in SessionStartRequest is not accepted by server (or none was given) a new DeviceGuid is generated and
            // communicated back here, null otherwise.
            [MetaMember(14)] public string                                      CorrectedDeviceGuid             { get; private set; }
            [Sensitive]
            [MetaMember(15)] public byte[]                                      ResumptionToken                 { get; private set; }

            SessionStartSuccess() { }
            public SessionStartSuccess(
                int queryId,
                int logicVersion,
                SessionToken sessionToken,
                ScheduledMaintenanceModeForClient scheduledMaintenanceMode,
                EntityId playerId,
                InitialPlayerState playerState,
                MetaDictionary<LanguageId, ContentHash> localizationVersions,
                bool developerMaintenanceBypass,
                List<EntityInitialState> entityStates,
                ISessionStartSuccessGamePayload gamePayload,
                string correctedDeviceGuid,
                byte[] resumptionToken)
            {
                QueryId = queryId;
                LogicVersion = logicVersion;
                SessionToken = sessionToken;
                ScheduledMaintenanceMode = scheduledMaintenanceMode;
                PlayerId = playerId;
                PlayerState = playerState;
                LocalizationVersions = localizationVersions;
                DeveloperMaintenanceBypass = developerMaintenanceBypass;
                EntityStates = entityStates;
                GamePayload = gamePayload;
                CorrectedDeviceGuid = correctedDeviceGuid;
                ResumptionToken = resumptionToken;
            }
        }

        [MetaMessage(MessageCodesCore.SessionStartFailure, MessageDirection.ServerToClient, hasExplicitMembers: true)]
        public class SessionStartFailure : MetaMessage
        {
            [MetaSerializable]
            public enum ReasonCode
            {
                InternalError = 0,
                Banned = 3,
                PlayerDeserializationFailure = 4, // \todo Currently not implemented on the server - only in OfflineServer.
                LogicVersionDowngradeNotAllowed = 5,
                Deleted = 6,
            }

            [MetaMember(1)] public int                          QueryId                 { get; private set; }
            [MetaMember(2)] public ReasonCode                   Reason                  { get; private set; }

            /// <summary>
            /// Human readable error message, but only if server has set <see cref="Metaplay.Cloud.Application.EnvironmentOptions.EnableDevelopmentFeatures" />.
            /// Null otherwise.
            /// </summary>
            [MetaMember(4)] public string                       DebugOnlyErrorMessage   { get; private set; }

            SessionStartFailure() { }
            public SessionStartFailure(int queryId, ReasonCode reason, string debugOnlyErrorMessage)
            {
                QueryId = queryId;
                Reason = reason;
                DebugOnlyErrorMessage = debugOnlyErrorMessage;
            }
        }

        [MetaMessage(MessageCodesCore.SessionStartResourceCorrection, MessageDirection.ServerToClient, hasExplicitMembers: true)]
        public class SessionStartResourceCorrection : MetaMessage
        {
            [MetaMember(1)] public int                          QueryId             { get; private set; }
            [MetaMember(2)] public SessionResourceCorrection    ResourceCorrection  { get; private set; }

            SessionStartResourceCorrection() { }
            public SessionStartResourceCorrection(int queryId, SessionResourceCorrection resourceCorrection)
            {
                QueryId = queryId;
                ResourceCorrection = resourceCorrection;
            }
        }

        [MetaMessage(MessageCodesCore.SessionResumeSuccess, MessageDirection.ServerToClient, hasExplicitMembers: true)]
        public class SessionResumeSuccess : MetaMessage
        {
            [MetaMember(1)] public SessionAcknowledgement               ServerSessionAcknowledgement    { get; private set; }
            [MetaMember(2)] public SessionToken                         SessionToken                    { get; private set; }
            [MetaMember(3)] public ScheduledMaintenanceModeForClient    ScheduledMaintenanceMode        { get; private set; }

            SessionResumeSuccess() { }
            public SessionResumeSuccess(SessionAcknowledgement serverSessionAcknowledgement, SessionToken sessionToken, ScheduledMaintenanceModeForClient scheduledMaintenanceMode)
            {
                ServerSessionAcknowledgement = serverSessionAcknowledgement;
                SessionToken = sessionToken;
                ScheduledMaintenanceMode = scheduledMaintenanceMode;
            }
        }

        [MetaMessage(MessageCodesCore.SessionResumeFailure, MessageDirection.ServerToClient, hasExplicitMembers: true)]
        public class SessionResumeFailure : MetaMessage
        {
            // When true, client should generate an incident report for this session resume failure. Expired sessions and
            // resume attempts over stale connections are expected and should not cause an incident report to be generated.
            [MetaMember(1)] public bool GenerateIncidentReport;

            [MetaDeserializationConstructor]
            public SessionResumeFailure(bool generateIncidentReport)
            {
                GenerateIncidentReport = generateIncidentReport;
            }
        }
    }
}
