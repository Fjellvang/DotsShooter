// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Cloud;
using Metaplay.Cloud.Analytics;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Analytics;
using Metaplay.Core.Config;
using Metaplay.Core.Localization;
using Metaplay.Core.Math;
using Metaplay.Core.Message;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using Metaplay.Server.Database;
using Metaplay.Server.LiveOpsEvent;
using Metaplay.Server.ServerAnalyticsEvents;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server
{
    /// <summary>
    /// Request the current status of <see cref="GlobalStateManager"/> (maintenance status, logic version config, etc.).
    /// <see cref="GlobalStateManager"/> responds with a <see cref="GlobalStatusResponse"/>.
    /// </summary>
    [MetaMessage(MessageCodesCore.GlobalStatusRequest, MessageDirection.ServerInternal)]
    public class GlobalStatusRequest : EntityAskRequest<GlobalStatusResponse>
    {
        public static readonly GlobalStatusRequest Instance = new GlobalStatusRequest();

        public GlobalStatusRequest() { }
    }

    /// <summary>
    /// Represents the current status of <see cref="GlobalStateManager"/>: contains maintenance status, logic version config, active entity
    /// counts, etc. Requested by admin dashboard.
    /// </summary>
    [MetaMessage(MessageCodesCore.GlobalStatusResponse, MessageDirection.ServerInternal)]
    public class GlobalStatusResponse : EntityAskResponse
    {
        public MaintenanceStatus            MaintenanceStatus           { get; init; }
        public ClientCompatibilitySettings  ClientCompatibilitySettings { get; init; }
        public MetaGuid                     ActiveStaticGameConfigId    { get; init; }
        public MetaGuid                     ActiveLocalizationsId       { get; init; }
        public MetaTime                     AnyGameConfigLastChangedAt  { get; init; }
        public MetaDuration                 GameTimeOffset              { get; init; }

        [MetaDeserializationConstructor]
        public GlobalStatusResponse(MaintenanceStatus maintenanceStatus, ClientCompatibilitySettings clientCompatibilitySettings,
            MetaGuid activeStaticGameConfigId, MetaGuid activeLocalizationsId,
            MetaTime anyGameConfigLastChangedAt,
            MetaDuration gameTimeOffset)
        {
            MaintenanceStatus               = maintenanceStatus;
            ClientCompatibilitySettings     = clientCompatibilitySettings;
            ActiveStaticGameConfigId        = activeStaticGameConfigId;
            ActiveLocalizationsId           = activeLocalizationsId;
            AnyGameConfigLastChangedAt = anyGameConfigLastChangedAt;
            GameTimeOffset = gameTimeOffset;
        }
    }

    /// <summary>
    /// A minimum client patch version requirement for clients on a given logic version and optionally for a given client
    /// platform. The list of client minimum patch version requirements are processed on client login and entries are filtered
    /// based on logic version and client platform. For entries not filtered out, the `MinPatchVersion` integer is checked
    /// against the ClientVersion.Patch information communicated by the client.
    /// </summary>
    [MetaSerializable]
    public struct ClientPatchVersionRequirement
    {
        [MetaMember(1)] public int             ForLogicVersion { get; private set; }
        [MetaMember(2)] public ClientPlatform? ForPlatform     { get; private set; }
        [MetaMember(3)] public int             MinPatchVersion { get; private set; }

        public override string ToString()
        {
            string platformOrAll = !ForPlatform.HasValue ? "All" : ForPlatform.Value.ToString();
            return Invariant($"LogicVersion:{ForLogicVersion} Platform:{platformOrAll} -> MinPatchVersion={MinPatchVersion}");
        }

        public bool IsApplicableForClient(int clientLogicVersion, ClientPlatform clientPlatform)
        {
            if (ForLogicVersion != clientLogicVersion)
                return false;
            if (ForPlatform.HasValue && ForPlatform.Value != clientPlatform)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Represents the settings of which client versions are compatible with the server, and whether the server
    /// should redirect clients above the accepted version to the server specified by <see cref="RedirectServerEndpoint"/>.
    /// </summary>
    [MetaSerializable]
    [MetaBlockedMembers(4)]
    public class ClientCompatibilitySettings
    {
        [MetaMember(5)] public MetaVersionRange                ActiveLogicVersionRange    { get; } // Currently active LogicVersion range
        [MetaMember(3)] public bool                            RedirectEnabled            { get; } // Enable redirecting for clients above AcceptedClientLogicVersions.MaxVersion
        [MetaMember(2)] public ServerEndpoint                  RedirectServerEndpoint     { get; } // Server endpoint to redirect too new clients: client.SupportedLogicVersions.max > server.ActiveLogicVersion
        [MetaMember(6)] public List<ClientPatchVersionRequirement> ClientPatchVersionRequirements { get; }

        /// <summary>
        /// The Currently active server LogicVersion. This is the same as <see cref="ActiveLogicVersionRange"/>.MaxVersion.
        /// </summary>
        public int ActiveLogicVersion => ActiveLogicVersionRange.MaxVersion;

        [MetaDeserializationConstructor]
        public ClientCompatibilitySettings(MetaVersionRange activeLogicVersionRange, bool redirectEnabled, ServerEndpoint redirectServerEndpoint, List<ClientPatchVersionRequirement> clientPatchVersionRequirements)
        {
            ActiveLogicVersionRange        = activeLogicVersionRange;
            RedirectEnabled                = redirectEnabled;
            RedirectServerEndpoint         = redirectServerEndpoint;
            ClientPatchVersionRequirements = clientPatchVersionRequirements ?? new List<ClientPatchVersionRequirement>();
        }

        internal ClientCompatibilitySettings WithActiveLogicVersionRange(MetaVersionRange newActiveLogicVersionRange)
        {
            return new ClientCompatibilitySettings(newActiveLogicVersionRange, RedirectEnabled, RedirectServerEndpoint, ClientPatchVersionRequirements);
        }

        internal ClientCompatibilitySettings WithClientPatchVersionRequirements(List<ClientPatchVersionRequirement> newClientPatchVersionRequirements)
        {
            return new ClientCompatibilitySettings(ActiveLogicVersionRange, RedirectEnabled, RedirectServerEndpoint, newClientPatchVersionRequirements);
        }
    }

    [MetaMessage(MessageCodesCore.UpdateClientCompatibilitySettingsRequest, MessageDirection.ServerInternal)]
    public class UpdateClientCompatibilitySettingsRequest : EntityAskRequest<UpdateClientCompatibilitySettingsResponse>
    {
        public ClientCompatibilitySettings Settings { get; private set; }

        public UpdateClientCompatibilitySettingsRequest() { }
        public UpdateClientCompatibilitySettingsRequest(ClientCompatibilitySettings config) { Settings = config; }
    }

    [MetaMessage(MessageCodesCore.UpdateClientCompatibilitySettingsResponse, MessageDirection.ServerInternal)]
    public class UpdateClientCompatibilitySettingsResponse : EntityAskResponse
    {
        public bool     IsSuccess       { get; private set; }
        public string   ErrorMessage    { get; private set; }

        public UpdateClientCompatibilitySettingsResponse() { }
        public UpdateClientCompatibilitySettingsResponse(bool isSuccess, string errorMessage) { IsSuccess = isSuccess; ErrorMessage = errorMessage; }
    }

    #region BroadcastConsumption

    /// <summary>
    /// A whole list of consumed broadcast counts. The GlobalStateProxies periodically
    /// send these to the GlobalStateManager
    /// </summary>
    [MetaMessage(MessageCodesCore.BroadcastConsumedCountInfo, MessageDirection.ServerInternal)]
    public class BroadcastConsumedCountInfo : MetaMessage
    {
        public Dictionary<int, int> BroadcastsConsumed { get; private set; }

        public BroadcastConsumedCountInfo() { }
        public BroadcastConsumedCountInfo(Dictionary<int, int> broadcastsConsumed) { BroadcastsConsumed = broadcastsConsumed; }
    }

    #endregion // BroadcastConsumption

    /// <summary>
    /// Request for toggling a player's developer status. GlobalStateManager responds with a <see cref="SetDeveloperPlayerResponse"/>.
    /// </summary>
    [MetaMessage(MessageCodesCore.SetDeveloperPlayerRequest, MessageDirection.ServerInternal)]
    public class SetDeveloperPlayerRequest : EntityAskRequest<SetDeveloperPlayerResponse>
    {
        public EntityId PlayerId;
        public bool     IsDeveloper;

        public SetDeveloperPlayerRequest() { }

        public SetDeveloperPlayerRequest(EntityId playerId, bool isDeveloper)
        {
            PlayerId        = playerId;
            IsDeveloper = isDeveloper;
        }
    }

    /// <summary>
    /// Response to a <see cref="SetDeveloperPlayerRequest"/>. Contains whether the request was successful or not.
    /// </summary>
    [MetaMessage(MessageCodesCore.SetDeveloperPlayerResponse, MessageDirection.ServerInternal)]
    public class SetDeveloperPlayerResponse : EntityAskResponse
    {
        public static SetDeveloperPlayerResponse Success = new SetDeveloperPlayerResponse(true);
        public static SetDeveloperPlayerResponse AlreadySatisfied = new SetDeveloperPlayerResponse(false);

        public bool IsSuccess { get; private set; }

        private SetDeveloperPlayerResponse() { }

        private SetDeveloperPlayerResponse(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }
    }

    /// <summary>
    /// Represents the database-persisted portion <see cref="GlobalStateManager"/>.
    /// </summary>
    [Table("GlobalStates")]
    public class PersistedGlobalState : IPersistedEntity
    {
        [Key]
        [PartitionKey]
        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string   EntityId        { get; set; }

        [Required]
        [Column(TypeName = "DateTime")]
        public DateTime PersistedAt     { get; set; }

        [Required]
        public byte[]   Payload         { get; set; }   // TaggedSerialized<GlobalState>

        [Required]
        public int      SchemaVersion   { get; set; }   // Schema version for object

        [Required]
        public bool     IsFinal         { get; set; }
    }

    [MetaMessage(MessageCodesCore.GlobalStateRequest, MessageDirection.ServerInternal)]
    public class GlobalStateRequest : EntityAskRequest<GlobalStateSnapshot>
    {
        public static readonly GlobalStateRequest Instance = new GlobalStateRequest();
    }

    [MetaMessage(MessageCodesCore.GlobalStateSubscribeRequest, MessageDirection.ServerInternal)]
    public class GlobalStateSubscribeRequest : MetaMessage
    {
        public GlobalStateSubscribeRequest() { }
    }

    [MetaMessage(MessageCodesCore.GlobalStateSnapshot, MessageDirection.ServerInternal)]
    public class GlobalStateSnapshot : EntityAskResponse
    {
        public MetaSerialized<GlobalState> GlobalState { get; private set; }

        GlobalStateSnapshot() { }
        public GlobalStateSnapshot(MetaSerialized<GlobalState> globalState)
        {
            GlobalState = globalState;
        }
    }

    [MetaMessage(MessageCodesCore.GlobalStateUpdateGameConfig, MessageDirection.ServerInternal)]
    public class GlobalStateUpdateGameConfig : MetaMessage
    {
        public MetaGuid                                                           StaticConfigId                     { get; private set; }
        public MetaGuid                                                           DynamicConfigId                    { get; private set; }
        public ConfigArchiveDeliverables                                          SharedConfigDeliverables           { get; private set; }
        public MetaDictionary<PlayerExperimentId, PlayerExperimentGlobalState> PlayerExperiments                  { get; private set; } // experiment state is tied to the config state, and both must update atomically.
        public ContentHash                                                        SharedPatchesForPlayersContentHash { get; private set; }
        public ContentHash                                                        SharedPatchesForTestersContentHash { get; private set; }
        public MetaTime                                                           Timestamp                          { get; private set; }
        public MetaTime                                                           ClientForceUpdateTimestamp         { get; private set; }

        GlobalStateUpdateGameConfig() { }
        public GlobalStateUpdateGameConfig(MetaGuid staticConfigId, MetaGuid dynamicConfigId, ConfigArchiveDeliverables sharedConfigDeliverables,
            MetaDictionary<PlayerExperimentId, PlayerExperimentGlobalState> playerExperiments, ContentHash sharedPatchesForPlayers, ContentHash sharedPatchesForTesters, MetaTime timestamp, MetaTime clientForceUpdateTimestamp)
        {
            StaticConfigId = staticConfigId;
            DynamicConfigId = dynamicConfigId;
            SharedConfigDeliverables = sharedConfigDeliverables;
            PlayerExperiments = playerExperiments;
            SharedPatchesForPlayersContentHash = sharedPatchesForPlayers;
            SharedPatchesForTestersContentHash = sharedPatchesForTesters;
            Timestamp = timestamp;
            ClientForceUpdateTimestamp = clientForceUpdateTimestamp;
        }
    }

    /// <summary>
    /// Notification to GSPs that the active localization has changed.
    /// </summary>
    [MetaMessage(MessageCodesCore.GlobalStateUpdateLocalizationsConfigVersion, MessageDirection.ServerInternal)]
    [LocalizationsEnabledCondition]
    public class GlobalStateUpdateLocalizationsConfigVersion : MetaMessage
    {
        public MetaGuid                  LocalizationsId { get; private set; }
        public LocalizationsDeliverables Deliverables    { get; private set; }

        private GlobalStateUpdateLocalizationsConfigVersion() { }

        public GlobalStateUpdateLocalizationsConfigVersion(MetaGuid localizationsId, LocalizationsDeliverables deliverables)
        {
            LocalizationsId = localizationsId;
            Deliverables    = deliverables;
        }
    }

    [MetaMessage(MessageCodesCore.SetGameTimeOffsetRequest, MessageDirection.ServerInternal)]
    public class SetGameTimeOffsetRequest : EntityAskRequest<SetGameTimeOffsetResponse>
    {
        public MetaDuration GameTimeOffset { get; }

        [MetaDeserializationConstructor]
        public SetGameTimeOffsetRequest(MetaDuration gameTimeOffset)
        {
            GameTimeOffset = gameTimeOffset;
        }
    }

    [MetaMessage(MessageCodesCore.SetGameTimeOffsetResponse, MessageDirection.ServerInternal)]
    public class SetGameTimeOffsetResponse : EntityAskResponse
    {
        public bool IsSuccess { get; }
        public string Error { get; }

        [MetaDeserializationConstructor]
        public SetGameTimeOffsetResponse(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }
    }

    [MetaMessage(MessageCodesCore.GlobalStateGameTimeOffsetUpdatedMessage, MessageDirection.ServerInternal)]
    public class GlobalStateGameTimeOffsetUpdatedMessage : MetaMessage
    {
        public MetaDuration GameTimeOffset { get; }

        [MetaDeserializationConstructor]
        public GlobalStateGameTimeOffsetUpdatedMessage(MetaDuration gameTimeOffset)
        {
            GameTimeOffset = gameTimeOffset;
        }
    }

    /// <summary>
    /// Results of a write-config-archive-into-CDN operation.
    /// </summary>
    [MetaSerializable]
    public struct ConfigArchiveDeliverables
    {
        [MetaMember(1)] public ContentHash Version;
        // \todo: list of delta encoded versions here

        public ConfigArchiveDeliverables(ContentHash version)
        {
            Version = version;
        }
    }

    /// <summary>
    /// Results of a write-localizations-into-CDN operation.
    /// </summary>
    [MetaSerializable]
    public struct LocalizationsDeliverables
    {
        [MetaMember(1)] public MetaDictionary<LanguageId, ContentHash> Languages;

        public LocalizationsDeliverables(IEnumerable<(LanguageId id, ContentHash version)> languages)
        {
            Languages = languages.ToMetaDictionary(x => x.id, x => x.version);
        }
    }

    /// <summary>
    /// Version identifying a server build.
    /// </summary>
    [MetaSerializable]
    public class ServerBuildVersion
    {
        [MetaMember(1)] public string BuildNumber   { get; private set; }
        [MetaMember(2)] public string CommitId      { get; private set; }

        ServerBuildVersion() { }
        public static ServerBuildVersion GetCurrent()
        {
            return new ServerBuildVersion()
            {
                BuildNumber = CloudCoreVersion.BuildNumber,
                CommitId = CloudCoreVersion.CommitId,
            };
        }

        public static string ToDisplayString(ServerBuildVersion version)
        {
            if (version == null)
                return "(version:unknown)";
            string buildNumber = version.BuildNumber ?? "unknown";
            string commitId = version.CommitId ?? "unknown";
            return $"(version:{buildNumber};commit:{commitId})";
        }
    }

    [MetaSerializable]
    public class LegacyLiveOpsEventsGlobalState
    {
        [MetaMember(1)] public MetaDictionary<MetaGuid, LiveOpsEventSpec> EventSpecs = new();
        [MetaMember(2)] public MetaDictionary<MetaGuid, LiveOpsEventOccurrence> EventOccurrences = new();
    }

    [MetaMessage(MessageCodesCore.GlobalStateForgetLegacyLiveOpsEventsState, MessageDirection.ServerInternal)]
    public class GlobalStateForgetLegacyLiveOpsEventsState : MetaMessage
    {
    }

    // GlobalState

    [MetaSerializable]
    [MetaBlockedMembers(3, 6, 20, 21, 9, 27, 38)]
    [MetaReservedMembers(1, 100)]
    public abstract partial class GlobalState : ISchemaMigratable
    {
        [MetaMember(4)] public ClientCompatibilitySettings                      ClientCompatibilitySettings { get; set; } = null;
        [MetaMember(1)] public ScheduledMaintenanceMode                         ScheduledMaintenanceMode    { get; set; } = null;
        [MetaMember(2)] public int                                              RunningBroadcastMessageId   { get; set; } = 1;
        [MetaMember(8)] public MetaDictionary<int, BroadcastMessage>         BroadcastMessages           { get; set; } = new MetaDictionary<int, BroadcastMessage>();
        [MetaMember(24)] public MetaGuid                                        StaticGameConfigId          { get; set; }

        // \todo [petri] made GameConfigVersions non-Transient, need to properly handle backward compatibility ..
        //[MetaMember(6)] public Dictionary<string, ContentHash>                  GameConfigVersions          { get; set; } = new Dictionary<string, ContentHash>();
        [MetaMember(7), Transient]  public byte[]                                                                  SharedClusterNonce                           { get; set; } // 32 bytes (256 bits) of random, shared between all nodes.
        [MetaMember(22)]            public MetaDictionary<PlayerExperimentId, PlayerExperimentGlobalState>      PlayerExperiments                            { get; set; } = new MetaDictionary<PlayerExperimentId, PlayerExperimentGlobalState>();
        [MetaMember(23)]            public MetaDictionary<PlayerExperimentId, PlayerExperimentGlobalStatistics> PlayerExperimentsStats                       { get; set; } = new MetaDictionary<PlayerExperimentId, PlayerExperimentGlobalStatistics>();
        [MetaMember(25)]            public ContentHash                                                             SharedGameConfigPatchesForPlayersContentHash { get; set; }
        [MetaMember(26)]            public ContentHash                                                             SharedGameConfigPatchesForTestersContentHash { get; set; }
        [MetaMember(28), Transient] public ConfigArchiveDeliverables                                               SharedGameConfigDeliverables                 { get; set; }
        [MetaMember(30)]            public OrderedSet<EntityId>                                                    DeveloperPlayerIds                           { get; set; } = new OrderedSet<EntityId>();
        [MetaMember(31)]            public MetaTime                                                                LatestGameConfigUpdate                       { get; set; } = MetaTime.Now; // \note: Default value only gets used when upgrading from older version and not updating game config
        [MetaMember(32)]            public ServerBuildVersion                                                      ServerBuildVersion                           { get; set; }
        [MetaMember(33)]            public MetaGuid                                                                LatestGameConfigAutoUpdateId                 { get; set; }
        [MetaMember(34)]            public MetaGuid                                                                ActiveLocalizationsId                        { get; set; }
        [MetaMember(35)]            public MetaTime                                                                LatestLocalizationsUpdate                    { get; set; }
        [MetaMember(36)]            public MetaGuid                                                                LatestLocalizationsAutoUpdateId              { get; set; }
        [MetaMember(37), Transient] public LocalizationsDeliverables                                               LocalizationsDeliverables                    { get; set; }

        /// <summary>
        /// Legacy. Only used for migrating the state to <see cref="LiveOpsTimeline.LiveOpsTimelineManager"/>,
        /// and then this gets set to null.
        /// </summary>
        [MetaMember(39)] public LegacyLiveOpsEventsGlobalState LegacyLiveOpsEvents { get; set; } = new();

        // The timestamp of the latest game config update that requested clients to update.
        [MetaMember(40)] public MetaTime ClientForceUpdateGameConfigTimestamp { get; set; }

        [MetaMember(41)] public MetaDuration GameTimeOffset { get; set; } = MetaDuration.Zero;

        #region Schema migrations

        protected void MigrateBroadcasts7To8()
        {
            // Version 8 changed broadcast targeting
            foreach (BroadcastMessage broadcast in BroadcastMessages.Values)
            {
                broadcast.Params.MigrateTargetSegments();
            }
        }

        protected void MigrateExperiments8To9()
        {
            // Version 9 changed experiments targeting
            foreach (PlayerExperimentGlobalState experiment in PlayerExperiments.Values)
            {
                experiment.MigrateDataFrom8To9();
            }
        }

        protected static Action<GlobalState> DefaultMigrationForVersion(int fromVersion)
        {
            return fromVersion switch
            {
                7 => x => x.MigrateBroadcasts7To8(),
                8 => x => x.MigrateExperiments8To9(),
                _ => null,
            };
        }

        #endregion
    }

    [MetaSerializableDerived(100)]
    [SupportedSchemaVersions(7, 9)]
    public class DefaultGlobalState : GlobalState
    {
        [RegisterMigrationsFunction]
        public static Action<DefaultGlobalState> RegisterMigration(int fromVersion)
        {
            return DefaultMigrationForVersion(fromVersion);
        }
    }

    // GlobalStateManager

    [EntityConfig]
    internal sealed class GlobalStateManagerConfig : PersistedEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindCloudCore.GlobalStateManager;
        public override Type                EntityActorType         => IntegrationRegistry.GetSingleIntegrationType<IGlobalStateManager>();
        public override EntityShardGroup    EntityShardGroup        => EntityShardGroup.BaseServices;
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.Service;
        public override IShardingStrategy   ShardingStrategy        => ShardingStrategies.CreateSingletonService();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(10);
        public override List<Type>          EntityComponentTypes    => new List<Type>() { typeof(GameConfigManager), typeof(LocalizationsDataManager) };
    }

    public interface IGlobalStateManager : IMetaIntegration<IGlobalStateManager>, IRequireSingleConcreteType
    {
        /// <summary>
        /// The time that any game config has changed last.
        /// <para>
        /// The server holds this in memory only, and does not remember the timestamp
        /// across server reboots; to be conservative, this is initialized to current
        /// time at server startup (even if no config changes happened at that time)
        /// in order to invalidate caches.
        /// </para>
        /// </summary>
        DateTime AnyGameConfigLastChangedAt { get; set; }

        internal IMetaLogger Logger { get; }
        internal GlobalState State  { get; }
        internal EntityActor Actor  { get; }
        internal Task OnActiveGameConfigChanged(FullGameConfig gameConfig, bool isInitial);
        internal Task OnActiveLocalizationsChanged();
    }

    public abstract partial class GlobalStateManagerBase<TGlobalState> : PersistedEntityActor<PersistedGlobalState, TGlobalState>, IGlobalStateManager where TGlobalState : GlobalState
    {
        protected override sealed AutoShutdownPolicy    ShutdownPolicy      => AutoShutdownPolicy.ShutdownNever();
        protected override sealed TimeSpan              SnapshotInterval    => TimeSpan.FromMinutes(3);     // Persist state every 3 minutes to minimize data loss on failures

        protected TGlobalState                          _state;

        readonly RandomPCG                                                      _analyticsEventRandom = RandomPCG.CreateNew();
        readonly AnalyticsEventBatcher<ServerEventBase, ServerAnalyticsContext> _analyticsEventBatcher;
        static readonly TimeSpan                                                AnalyticsEventFlushInterval = TimeSpan.FromSeconds(15);
        internal class AnalyticsEventFlush { public static readonly AnalyticsEventFlush Instance = new AnalyticsEventFlush(); }

        protected GameConfigManager _gameConfigManager;
        protected LocalizationsDataManager _localizationsManager;

        /// <inheritdoc/>
        public DateTime AnyGameConfigLastChangedAt { get; set; } = DateTime.UtcNow;

        protected GlobalStateManagerBase(EntityId entityId) : base(entityId)
        {
            _analyticsEventBatcher = new AnalyticsEventBatcher<ServerEventBase, ServerAnalyticsContext>(entityId, maxBatchSize: 10);
        }

        protected override void PreStart()
        {
            base.PreStart();

            StartPeriodicTimer(TimeSpan.FromSeconds(5), AnalyticsEventFlushInterval, AnalyticsEventFlush.Instance);
        }

        [EntityAskHandler]
        public async Task<SetGameTimeOffsetResponse> HandleSetGameTimeOffsetRequestAsync(SetGameTimeOffsetRequest request)
        {
            if (!RuntimeOptionsRegistry.Instance.GetCurrent<GameTimeOptions>().EnableTimeSkipping)
                return new SetGameTimeOffsetResponse(isSuccess: false, error: $"Game time skipping is disabled. See runtime option GameTime:{nameof(GameTimeOptions.EnableTimeSkipping)} .");

            if (request.GameTimeOffset < _state.GameTimeOffset)
                return new SetGameTimeOffsetResponse(isSuccess: false, error: $"Game time offset cannot be decreased from its existing value. Existing value is {_state.GameTimeOffset} and requested value is {request.GameTimeOffset}");

            if (request.GameTimeOffset == _state.GameTimeOffset)
                return new SetGameTimeOffsetResponse(isSuccess: true, error: null);

            _log.Info("Game time offset updated from {OldOffset} to {NewOffset}", _state.GameTimeOffset, request.GameTimeOffset);
            _state.GameTimeOffset = request.GameTimeOffset;

            await PersistStateIntermediate();

            PublishMessage(EntityTopic.Member, new GlobalStateGameTimeOffsetUpdatedMessage(_state.GameTimeOffset));

            return new SetGameTimeOffsetResponse(isSuccess: true, error: null);
        }

        protected override async Task OnShutdown()
        {
            _analyticsEventBatcher.Flush();

            await OnShutdownMaintenanceMode();

            await base.OnShutdown();
        }

        protected override EntityComponent CreateComponent(Type componentType)
        {
            if (componentType == typeof(GameConfigManager))
            {
                _gameConfigManager = new GameConfigManager(this);
                return _gameConfigManager;
            }
            else if (componentType == typeof(LocalizationsDataManager))
            {
                _localizationsManager = new LocalizationsDataManager(this);
                return _localizationsManager;
            }

            return base.CreateComponent(componentType);
        }

        protected override sealed async Task Initialize()
        {
            // Try to fetch from database & restore from it (if exists)
            PersistedGlobalState persisted = await MetaDatabase.Get().TryGetAsync<PersistedGlobalState>(_entityId.ToString());
            await InitializePersisted(persisted);
        }

        protected override sealed Task<TGlobalState> RestoreFromPersisted(PersistedGlobalState persisted)
        {
            // Deserialize actual state
            TGlobalState state = DeserializePersistedPayload<TGlobalState>(persisted.Payload, resolver: null, logicVersion: null);

            return Task.FromResult(state);
        }

        protected override sealed async Task PostLoad(TGlobalState payload, DateTime persistedAt, TimeSpan elapsedTime)
        {
            _state = payload;
            _state.SharedClusterNonce = NewClusterNonce();

            if (_state.ClientCompatibilitySettings == null)
            {
                // Initialize ClientCompatibilitySettings with defaults
                _log.Info("Initializing ClientCompatibilitySettings with accepted client LogicVersions={AcceptedClientVersions}", MetaplayCore.Options.SupportedLogicVersions);
                _state.ClientCompatibilitySettings = new ClientCompatibilitySettings(
                    activeLogicVersionRange:            MetaplayCore.Options.SupportedLogicVersions,
                    redirectEnabled:                    false,
                    redirectServerEndpoint:             null,
                    clientPatchVersionRequirements:     null);
            }
            else if (_state.ClientCompatibilitySettings.ActiveLogicVersionRange == null)
            {
                // Initialize active logic version range if null
                _state.ClientCompatibilitySettings = _state.ClientCompatibilitySettings.WithActiveLogicVersionRange(MetaplayCore.Options.SupportedLogicVersions);
            }
            else
            {
                // Update ActiveLogicVersion: must be at least supported min version & check for auto-upgrade to latest (useful in local builds)
                SystemOptions systemOpts            = RuntimeOptionsRegistry.Instance.GetCurrent<SystemOptions>();
                int           activeLogicVersionMax = _state.ClientCompatibilitySettings.ActiveLogicVersionRange.MaxVersion;
                int           activeLogicVersionMin = _state.ClientCompatibilitySettings.ActiveLogicVersionRange.MinVersion;

                if (activeLogicVersionMin < MetaplayCore.Options.SupportedLogicVersions.MinVersion)
                {
                    _log.Info("Force-upgrading Minimum ActiveLogicVersion from {OldActiveLogicVersion} to {NewActiveLogicVersion}", activeLogicVersionMin, MetaplayCore.Options.SupportedLogicVersions.MinVersion);
                    activeLogicVersionMin = MetaplayCore.Options.SupportedLogicVersions.MinVersion;
                    if (activeLogicVersionMax < activeLogicVersionMin)
                        activeLogicVersionMax = activeLogicVersionMin;
                }

                if (systemOpts.AutoUpgradeLogicVersion && activeLogicVersionMax < MetaplayCore.Options.SupportedLogicVersions.MaxVersion)
                {
                    _log.Info("Auto-upgrading Maximum ActiveLogicVersion from {OldActiveLogicVersion} to latest {NewActiveLogicVersion}", activeLogicVersionMax, MetaplayCore.Options.SupportedLogicVersions.MaxVersion);
                    activeLogicVersionMax = MetaplayCore.Options.SupportedLogicVersions.MaxVersion;
                }
                else if (activeLogicVersionMax > MetaplayCore.Options.SupportedLogicVersions.MaxVersion)
                {
                    _log.Error(
                        "ActiveLogicVersion {ActiveLogicVersionMax} is newer than the latest supported logic version {SupportedLogicVersionMax}. "
                        + "This is most likely an unintended version rollback which could result in data loss. "
                        + "Aborting. "
                        + "Previously successfully running server version was {PreviousVersion} and this server is {CurrentVersion}",
                        activeLogicVersionMax,
                        MetaplayCore.Options.SupportedLogicVersions.MaxVersion,
                        ServerBuildVersion.ToDisplayString(_state.ServerBuildVersion),
                        ServerBuildVersion.ToDisplayString(ServerBuildVersion.GetCurrent()));
                    throw new InvalidOperationException("Illegal SupportedLogicVersions. Max version too low.");
                }

                MetaVersionRange newRange = new MetaVersionRange(activeLogicVersionMin, activeLogicVersionMax);
                if (!newRange.Equals(_state.ClientCompatibilitySettings.ActiveLogicVersionRange))
                    _state.ClientCompatibilitySettings = _state.ClientCompatibilitySettings.WithActiveLogicVersionRange(newRange);
            }

            // Update ClientPatchVersionRequirements: filter out versions outside of the supported range
            if (_state.ClientCompatibilitySettings.ClientPatchVersionRequirements.Any(r => !MetaplayCore.Options.SupportedLogicVersions.Contains(r.ForLogicVersion)))
            {
                _state.ClientCompatibilitySettings = _state.ClientCompatibilitySettings.WithClientPatchVersionRequirements(
                    _state.ClientCompatibilitySettings.ClientPatchVersionRequirements
                        .Where(r => MetaplayCore.Options.SupportedLogicVersions.Contains(r.ForLogicVersion))
                        .ToList());
            }

            await _localizationsManager.Initialize();
            await _gameConfigManager.Initialize();
            InitializeMaintenanceMode();

            // Find & filter out any invalid broadcasts
            List<BroadcastMessage> invalidBroadcasts = _state.BroadcastMessages.Values.Where(broadcast => !broadcast.Params.IsContentsValid).ToList();
            if (invalidBroadcasts.Count > 0)
            {
                _log.Warning("Filtering out {NumInvalidBroadcasts} invalid broadcasts: {InvalidBroadcasts}", invalidBroadcasts.Count, PrettyPrint.Verbose(invalidBroadcasts));
                foreach (BroadcastMessage broadcast in invalidBroadcasts)
                    _state.BroadcastMessages.Remove(broadcast.Params.Id);
            }

            _state.ServerBuildVersion = ServerBuildVersion.GetCurrent();
        }

        async Task IGlobalStateManager.OnActiveGameConfigChanged(FullGameConfig config, bool isInitial)
        {
            // Sync experiments state with new config
            SyncExperimentsPlan syncPlan = PrepareSyncExperimentsWithConfig(config);
            await ActivatePlannedExperimentChangeAsync(syncPlan, uploadPatchesEvenIfNoChanges: isInitial);

            if (!isInitial)
            {
                // Persist changes to game config related global state
                await PersistStateIntermediate();

                // Broadcast to proxies
                PublishGameConfigOrExperimentUpdate();
            }
        }

        async Task IGlobalStateManager.OnActiveLocalizationsChanged()
        {
            // Ensure updated config is persisted
            await PersistStateIntermediate();

            // Publish update to proxies
            PublishMessage(EntityTopic.Member, new GlobalStateUpdateLocalizationsConfigVersion(_state.ActiveLocalizationsId, _state.LocalizationsDeliverables));
        }

        protected override sealed async Task PersistStateImpl(bool isInitial, bool isFinal)
        {
            _log.Debug("Persisting state (isInitial={IsInitial}, isFinal={IsFinal}, schemaVersion={SchemaVersion})", isInitial, isFinal, CurrentSchemaVersion);

            // Serialize and compress the state
            byte[] persistedPayload = SerializeToPersistedPayload(_state, resolver: null, logicVersion: null);

            // Persist in database
            PersistedGlobalState persisted = new PersistedGlobalState
            {
                EntityId        = _entityId.ToString(),
                PersistedAt     = MetaTime.Now.ToDateTime(),
                Payload         = persistedPayload,
                SchemaVersion   = CurrentSchemaVersion,
                IsFinal         = isFinal,
            };

            if (isInitial)
                await MetaDatabase.Get().InsertAsync(persisted).ConfigureAwait(false);
            else
                await MetaDatabase.Get().UpdateAsync(persisted).ConfigureAwait(false);
        }

        [CommandHandler]
        void ReceiveAnalyticsEventFlush(AnalyticsEventFlush _)
        {
            _analyticsEventBatcher.Flush();
        }

        protected override sealed Task<MetaMessage> OnNewSubscriber(EntitySubscriber subscriber, MetaMessage message)
        {
            // Respond to proxies with the global state
            MetaSerialized<GlobalState> serialized = new MetaSerialized<GlobalState>(_state, MetaSerializationFlags.IncludeAll, logicVersion: null);
            return Task.FromResult<MetaMessage>(new GlobalStateSnapshot(serialized));
        }

        protected sealed override Task OnSubscriberTerminatedAsync(EntitySubscriber subscriber)
        {
            _log.Warning("Subscriber unexpectedly lost: {SubscriberId} ({SubscriberActorRef})", subscriber.EntityId, subscriber.ActorRef);
            return Task.CompletedTask;
        }

        [EntityAskHandler]
        public async Task<AddBroadcastMessageResponse> HandleAddBroadcastMessage(AddBroadcastMessage addBroadcast)
        {
            AddBroadcastMessageResponse reply;
            if (!addBroadcast.BroadcastParams.Validate(out string paramsValidationError))
                reply = AddBroadcastMessageResponse.Failure($"Invalid broadcast params: {paramsValidationError}");
            else
            {
                // Allocate id for broadcast
                BroadcastMessageParams broadcastParams = addBroadcast.BroadcastParams;
                _log.Info("Add broadcast: {Message}", PrettyPrint.Verbose(broadcastParams));
                int broadcastId = _state.RunningBroadcastMessageId++;
                broadcastParams.Id = broadcastId;
                _state.BroadcastMessages.Add(broadcastParams.Id, new BroadcastMessage(broadcastParams, new BroadcastMessageStats()));

                // Persist & forward to proxies (with broadcast.Id set)
                await PersistStateIntermediate();
                PublishMessage(EntityTopic.Member, addBroadcast);
                reply = AddBroadcastMessageResponse.Ok(broadcastId);
            }

            return reply;
        }

        [MessageHandler]
        public async Task HandleUpdateBroadcastMessage(UpdateBroadcastMessage updateBroadcast)
        {
            // Update broadcast in state
            BroadcastMessageParams broadcastParams = updateBroadcast.BroadcastParams;
            _log.Info("Update broadcast: {Message}", PrettyPrint.Verbose(broadcastParams));
            _state.BroadcastMessages[broadcastParams.Id].Params = broadcastParams;

            // Persist & forward to proxies
            await PersistStateIntermediate();
            PublishMessage(EntityTopic.Member, updateBroadcast);
        }

        [MessageHandler]
        public async Task HandleDeleteBroadcastMessage(DeleteBroadcastMessage deleteBroadcast)
        {
            // Delete broadcast
            _log.Info("Delete broadcast: #{BroadcastId}", deleteBroadcast.BroadcastId);
            _state.BroadcastMessages.Remove(deleteBroadcast.BroadcastId);

            // Persist & forward to proxies
            await PersistStateIntermediate();
            PublishMessage(EntityTopic.Member, deleteBroadcast);
        }

        [EntityAskHandler]
        public GlobalStateSnapshot HandleGlobalStateRequest(GlobalStateRequest _)
        {
            MetaSerialized<GlobalState> serialized = new MetaSerialized<GlobalState>(_state, MetaSerializationFlags.IncludeAll, logicVersion: null);
            return new GlobalStateSnapshot(serialized);
        }

        [EntityAskHandler]
        public GlobalStatusResponse HandleGlobalStatusRequest(GlobalStatusRequest _)
        {
            MaintenanceStatus maintenanceStatus = GetMaintenanceStatusAtNow();
            return new GlobalStatusResponse(maintenanceStatus, _state.ClientCompatibilitySettings, _state.StaticGameConfigId, _state.ActiveLocalizationsId, MetaTime.FromDateTime(AnyGameConfigLastChangedAt), _state.GameTimeOffset);
        }

        [EntityAskHandler]
        public async Task<UpdateClientCompatibilitySettingsResponse> HandleUpdateClientCompatibilitySettingsRequest(UpdateClientCompatibilitySettingsRequest updateSettings)
        {
            if(updateSettings.Settings.ActiveLogicVersionRange.MinVersion < MetaplayCore.Options.SupportedLogicVersions.MinVersion)
                return new UpdateClientCompatibilitySettingsResponse(isSuccess: false, errorMessage: FormattableString.Invariant($"Minimum ActiveLogicVersion {updateSettings.Settings.ActiveLogicVersionRange.MinVersion} is lower than the minimum supported version {MetaplayCore.Options.SupportedLogicVersions.MinVersion}."));
            if(updateSettings.Settings.ActiveLogicVersionRange.MaxVersion > MetaplayCore.Options.SupportedLogicVersions.MaxVersion)
                return new UpdateClientCompatibilitySettingsResponse(isSuccess: false, errorMessage: FormattableString.Invariant($"Maximum ActiveLogicVersion {updateSettings.Settings.ActiveLogicVersionRange.MaxVersion} is higher than the maximum supported version {MetaplayCore.Options.SupportedLogicVersions.MaxVersion}."));
            if(updateSettings.Settings.ActiveLogicVersionRange.MinVersion > updateSettings.Settings.ActiveLogicVersionRange.MaxVersion)
                return new UpdateClientCompatibilitySettingsResponse(isSuccess: false, errorMessage: FormattableString.Invariant($"Minimum ActiveLogicVersion {updateSettings.Settings.ActiveLogicVersionRange.MinVersion} is higher than the maximum ActiveLogicVersion {updateSettings.Settings.ActiveLogicVersionRange.MaxVersion}."));

            // Store new config & save
            ClientCompatibilitySettings settings = updateSettings.Settings;
            _log.Info("Updating ClientCompatibilitySettings to {ClientCompatibilitySettings}", PrettyPrint.Verbose(settings));
            _state.ClientCompatibilitySettings = settings;

            // Persist & publish to proxies
            await PersistStateIntermediate();
            PublishMessage(EntityTopic.Member, updateSettings);

            // Respond
            return new UpdateClientCompatibilitySettingsResponse(isSuccess: true, errorMessage: null);
        }

        [EntityAskHandler]
        public async Task<SetDeveloperPlayerResponse> HandleSetDeveloperPlayerRequest(EntityId sender, SetDeveloperPlayerRequest request)
        {
            _log.Info("Set developer player: {Message}", PrettyPrint.Verbose(request));

            if(request.IsDeveloper)
            {
                if(_state.DeveloperPlayerIds.Contains(request.PlayerId))
                    return SetDeveloperPlayerResponse.AlreadySatisfied;

                _state.DeveloperPlayerIds.Add(request.PlayerId);
            }
            else
            {
                if(!_state.DeveloperPlayerIds.Contains(request.PlayerId))
                    return SetDeveloperPlayerResponse.AlreadySatisfied;

                _state.DeveloperPlayerIds.Remove(request.PlayerId);
            }

            // Persist & forward to proxies
            await PersistStateIntermediate();
            PublishMessage(EntityTopic.Member, request);

            /* BEWARE:
             * Some race conditions are possible here:
             *
             * GSM is always the authority of developer-hood
             * GSP is always behind for GSM
             * Model.IsDeveloper is both ahead and behind of GSP.
             * Behind after PostLoad (GSP continues to evolve, model frozen)
             * Possibly ahead when controller uses api route and GSM informs the client directly.
             * (Theoretically the Model could shutdown and wake up before GSP is updated, getting a stale value)
             *
             * This behavior of Model.IsDeveloper is not a problem, as long as IsDeveloper is used to deliver developer-hood info to client.
             * Client cannot blindly trust the value anyway as it can change, so no problems there.
             *
             * However, if IsDeveloper is used for other purposes, Model.IsDeveloper should always be behind GSP.
             * This means the GSM cannot inform Model of flag change as it can only happen after GSP update.
             */
            if (sender != request.PlayerId)
                CastMessage(request.PlayerId, new InternalPlayerDeveloperStatusChanged(request.IsDeveloper));

            return SetDeveloperPlayerResponse.Success;
        }

        /// <summary>
        /// Publishes game config changes to all GSPs, making it active.
        /// </summary>
        void PublishGameConfigOrExperimentUpdate()
        {
            PublishMessage(EntityTopic.Member, new GlobalStateUpdateGameConfig(
                staticConfigId:                 _state.StaticGameConfigId,
                dynamicConfigId:                MetaGuid.None, // _state.DynamicGameConfigVersion
                sharedConfigDeliverables:       _state.SharedGameConfigDeliverables,
                playerExperiments:              _state.PlayerExperiments,
                sharedPatchesForPlayers:        _state.SharedGameConfigPatchesForPlayersContentHash,
                sharedPatchesForTesters:        _state.SharedGameConfigPatchesForTestersContentHash,
                timestamp:                      _state.LatestGameConfigUpdate,
                clientForceUpdateTimestamp:     _state.ClientForceUpdateGameConfigTimestamp));
        }

        /// <summary>
        /// Handle a list of consumed broadcast messages that are sent sent periodically from a shard
        /// </summary>
        /// <param name="fromEntityId"></param>
        /// <param name="info"></param>
        [MessageHandler]
        public void HandleBroadcastConsumedCountInfo(EntityId fromEntityId, BroadcastConsumedCountInfo info)
        {
            foreach ((int broadcastId, int count) in info.BroadcastsConsumed)
            {
                // Add to a broadcast's received count if that broadcast still exists
                if (_state.BroadcastMessages.ContainsKey(broadcastId))
                    _state.BroadcastMessages[broadcastId].Stats.ReceivedCount += count;
            }
        }

        [MessageHandler]
        public void HandleGlobalStateForgetLegacyLiveOpsEventsStateMessage(GlobalStateForgetLegacyLiveOpsEventsState _)
        {
            if (_state.LegacyLiveOpsEvents != null && (_state.LegacyLiveOpsEvents.EventSpecs.Count > 0 || _state.LegacyLiveOpsEvents.EventOccurrences.Count > 0))
            {
                _log.Info($"Clearing {nameof(_state.LegacyLiveOpsEvents)} due to migration to {nameof(LiveOpsTimeline.LiveOpsTimelineManager)}");
                // \note Intentionally clearing instead of setting to null, to make development life easier when jumping
                //       between branches in some of which this migration didn't yet exist (null LiveOpsEvents in GSM would cause problems).
                //       Going backwards in SDK versions is still not properly supported, this is just for development-time quality of life.
                _state.LegacyLiveOpsEvents = new LegacyLiveOpsEventsGlobalState();
            }

            if (_state.LegacyLiveOpsEvents == null)
            {
                // \note See comment above for reason.
                //       Previously this had been set to null; fix-up to non-null now.
                _state.LegacyLiveOpsEvents = new LegacyLiveOpsEventsGlobalState();
            }
        }

        protected override sealed void OnTerminated(Terminated terminated)
        {
            _log.Info("LOST SUBSCRIBER: {Actor}", terminated.ActorRef);
        }

        void EmitAnalyticsEvent(ServerEventBase payload)
        {
            AnalyticsEventSpec      eventSpec       = AnalyticsEventRegistry.GetEventSpec(payload.GetType());
            string                  eventType       = eventSpec.EventType;
            MetaTime                collectedAt     = MetaTime.Now;
            MetaUInt128             uniqueId        = new MetaUInt128((ulong)collectedAt.MillisecondsSinceEpoch, _analyticsEventRandom.NextULong());
            int                     schemaVersion   = eventSpec.SchemaVersion;
            ServerAnalyticsContext  context         = new ServerAnalyticsContext();

            _analyticsEventBatcher.Enqueue(_entityId, collectedAt, modelTime: collectedAt, uniqueId, eventType, schemaVersion, payload, context, labels: null, resolver: null, logicVersion: null);
        }

        static byte[] NewClusterNonce()
        {
            return RandomNumberGenerator.GetBytes(32);
        }

        // Expose internals to internal EntityComponents
        IMetaLogger IGlobalStateManager.Logger => _log;
        GlobalState IGlobalStateManager.State  => _state;
        EntityActor IGlobalStateManager.Actor  => this;
    }

    public class DefaultGlobalStateManager : GlobalStateManagerBase<DefaultGlobalState>
    {
        public DefaultGlobalStateManager(EntityId entityId) : base(entityId)
        {
        }

        protected override Task<DefaultGlobalState> InitializeNew()
        {
            // Create new state
            DefaultGlobalState state = new DefaultGlobalState();
            return Task.FromResult(state);
        }
    }

    public static class GlobalStateManager
    {
        public static readonly EntityId EntityId = EntityId.Create(EntityKindCloudCore.GlobalStateManager, 0);
    }
}
