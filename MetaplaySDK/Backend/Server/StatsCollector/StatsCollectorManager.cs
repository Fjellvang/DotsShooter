// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Akka.Util.Internal;
using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Metaplay.Core.Activables;
using Metaplay.Core.Math;
using Metaplay.Core.Model;
using Metaplay.Core.Offers;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using Metaplay.Server.Database;
using Metaplay.Server.StatisticsEvents;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server
{
    /// <summary>
    /// Reports a batch of MetaActivable statistics information.
    ///
    /// Also includes additional information for certain kinds of activables
    /// (namely, MetaOffers). They're sent together with general activables
    /// info to ensure they're updated at the same time.
    ///
    /// Sent by StatsCollectorProxys periodically to StatsCollectorManager.
    /// </summary>
    [MetaMessage(MessageCodesCore.MetaActivableStatisticsInfo, MessageDirection.ServerInternal)]
    public class MetaActivableStatisticsInfo : MetaMessage
    {
        public FullMetaActivableStatistics  ActivableStatistics;
        public MetaOffersStatistics         MetaOffersStatistics;

        MetaActivableStatisticsInfo(){ }
        public MetaActivableStatisticsInfo(FullMetaActivableStatistics activableStatistics, MetaOffersStatistics metaOffersStatistics)
        {
            ActivableStatistics = activableStatistics ?? throw new ArgumentNullException(nameof(activableStatistics));
            MetaOffersStatistics = metaOffersStatistics ?? throw new ArgumentNullException(nameof(metaOffersStatistics));
        }
    }

    [MetaMessage(MessageCodesCore.LiveOpsEventsStatisticsInfo, MessageDirection.ServerInternal)]
    public class LiveOpsEventsStatisticsInfo : MetaMessage
    {
        public readonly Dictionary<MetaGuid, int> EventParticipantCounts;

        [MetaDeserializationConstructor]
        public LiveOpsEventsStatisticsInfo(Dictionary<MetaGuid, int> eventParticipantCounts)
        {
            EventParticipantCounts = eventParticipantCounts;
        }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorStateRequest, MessageDirection.ServerInternal)]
    public class StatsCollectorStateRequest : EntityAskRequest<StatsCollectorStateResponse>
    {
        public static readonly StatsCollectorStateRequest Instance = new StatsCollectorStateRequest();
    }

    [MetaMessage(MessageCodesCore.StatsCollectorStateResponse, MessageDirection.ServerInternal)]
    public class StatsCollectorStateResponse : EntityAskResponse
    {
        public MetaSerialized<StatsCollectorState> State { get; private set; }

        StatsCollectorStateResponse() { }
        public StatsCollectorStateResponse(MetaSerialized<StatsCollectorState> state)
        {
            State = state;
        }
    }

    /// <summary>
    /// Query how many items of a specific <see cref="EntityKind"/> are persisted in the database.
    /// </summary>
    [MetaMessage(MessageCodesCore.StatsCollectorDatabaseEntityCountRequest, MessageDirection.ServerInternal)]
    public class StatsCollectorDatabaseEntityCountRequest : EntityAskRequest<StatsCollectorDatabaseEntityCountResponse>
    {
        public EntityKind EntityKind { get; private set; }

        StatsCollectorDatabaseEntityCountRequest() { }
        public StatsCollectorDatabaseEntityCountRequest(EntityKind entityKind) { EntityKind = entityKind; }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorDatabaseEntityCountResponse, MessageDirection.ServerInternal)]
    public class StatsCollectorDatabaseEntityCountResponse : EntityAskResponse
    {
        public int      EntityCount { get; private set; }
        public MetaTime UpdatedAt   { get; private set; }

        StatsCollectorDatabaseEntityCountResponse() { }
        public StatsCollectorDatabaseEntityCountResponse(int entityCount, MetaTime updatedAt) { EntityCount = entityCount; UpdatedAt = updatedAt; }
    }

    /// <summary>
    /// Query how many Entities of each kind are currently live in the cluster.
    /// </summary>
    [MetaMessage(MessageCodesCore.StatsCollectorLiveEntityCountPerNodeRequest, MessageDirection.ServerInternal)]
    public class StatsCollectorLiveEntityCountPerNodeRequest : EntityAskRequest<StatsCollectorLiveEntityCountPerNodeResponse>
    {
        public static readonly StatsCollectorLiveEntityCountPerNodeRequest Instance = new StatsCollectorLiveEntityCountPerNodeRequest();
    }

    [MetaMessage(MessageCodesCore.StatsCollectorLiveEntityCountPerNodeResponse, MessageDirection.ServerInternal)]
    public class StatsCollectorLiveEntityCountPerNodeResponse : EntityAskResponse
    {
        public MetaDictionary<ClusterNodeAddress, MetaDictionary<EntityKind, int>> LiveEntityCounts { get; private set; }

        StatsCollectorLiveEntityCountPerNodeResponse() { }
        public StatsCollectorLiveEntityCountPerNodeResponse(MetaDictionary<ClusterNodeAddress, MetaDictionary<EntityKind, int>> liveEntityCounts) { LiveEntityCounts = liveEntityCounts; }
    }

    /// <summary>
    /// Query how many Entities of each kind are currently live in the cluster.
    /// </summary>
    [MetaMessage(MessageCodesCore.StatsCollectorLiveEntityCountRequest, MessageDirection.ServerInternal)]
    public class StatsCollectorLiveEntityCountRequest : EntityAskRequest<StatsCollectorLiveEntityCountResponse>
    {
        public static readonly StatsCollectorLiveEntityCountRequest Instance = new StatsCollectorLiveEntityCountRequest();
    }

    [MetaMessage(MessageCodesCore.StatsCollectorLiveEntityCountResponse, MessageDirection.ServerInternal)]
    public class StatsCollectorLiveEntityCountResponse : EntityAskResponse
    {
        public MetaDictionary<EntityKind, int> LiveEntityCounts { get; private set; }

        StatsCollectorLiveEntityCountResponse() { }
        public StatsCollectorLiveEntityCountResponse(MetaDictionary<EntityKind, int> liveEntityCounts) { LiveEntityCounts = liveEntityCounts; }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorRecentLoggedErrorsInfo, MessageDirection.ServerInternal)]
    public class RecentLoggedErrorsInfo : MetaMessage
    {
        public RecentLogEventCounter.LogEventBatch RecentLoggedErrors;

        public RecentLoggedErrorsInfo() { }
        public RecentLoggedErrorsInfo(RecentLogEventCounter.LogEventBatch recentLoggedErrors)
        {
            RecentLoggedErrors   = recentLoggedErrors;
        }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorRecentLoggedErrorsResponse, MessageDirection.ServerInternal)]
    public class RecentLoggedErrorsResponse : EntityAskResponse
    {
        public int                                           RecentLoggedErrors;
        public bool                                          OverMaxErrorCount;
        public MetaDuration                                  MaxAge;
        public bool                                          CollectorRestartedWithinMaxAge;
        public MetaTime                                      CollectorRestartTime;
        public RecentLogEventCounter.LogEventInfo[]          ErrorsDetails;

        public RecentLoggedErrorsResponse() { }
        public RecentLoggedErrorsResponse(int recentLoggedErrors, bool overMaxErrorCount, MetaDuration maxAge, bool collectorRestartedWithinMaxAge, MetaTime collectorRestartTime, RecentLogEventCounter.LogEventInfo[] errorsDetails)
        {
            RecentLoggedErrors             = recentLoggedErrors;
            OverMaxErrorCount              = overMaxErrorCount;
            MaxAge                         = maxAge;
            CollectorRestartedWithinMaxAge = collectorRestartedWithinMaxAge;
            CollectorRestartTime           = collectorRestartTime;
            ErrorsDetails                  = errorsDetails;
        }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorRecentLoggedErrorsRequest, MessageDirection.ServerInternal)]
    public class RecentLoggedErrorsRequest : EntityAskRequest<RecentLoggedErrorsResponse>
    {
        public static readonly RecentLoggedErrorsRequest Instance = new RecentLoggedErrorsRequest();
    }

    [MetaMessage(MessageCodesCore.StatsCollectorLiveOpsEventStatisticsRequest, MessageDirection.ServerInternal)]
    public class StatsCollectorLiveOpsEventStatisticsRequest : EntityAskRequest<StatsCollectorLiveOpsEventStatisticsResponse>
    {
        public readonly OrderedSet<MetaGuid> OccurrenceIds;

        [MetaDeserializationConstructor]
        public StatsCollectorLiveOpsEventStatisticsRequest(OrderedSet<MetaGuid> occurrenceIds)
        {
            OccurrenceIds = occurrenceIds ?? throw new ArgumentNullException(nameof(occurrenceIds));
        }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorLiveOpsEventStatisticsResponse, MessageDirection.ServerInternal)]
    public class StatsCollectorLiveOpsEventStatisticsResponse : EntityAskResponse
    {
        public readonly MetaDictionary<MetaGuid, LiveOpsEventOccurrenceStatistics> Statistics;

        [MetaDeserializationConstructor]
        public StatsCollectorLiveOpsEventStatisticsResponse(MetaDictionary<MetaGuid, LiveOpsEventOccurrenceStatistics> statistics)
        {
            Statistics = statistics;
        }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorStatisticsPageReadRequest, MessageDirection.ServerInternal)]
    public class StatsCollectorStatisticsPageReadRequest : MetaMessage
    {
        public string PageId { get; private set; }

        public StatsCollectorStatisticsPageReadRequest(string pageId)
        {
            PageId = pageId;
        }

        // For deserialization
        StatsCollectorStatisticsPageReadRequest() { }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorStatisticsPageReadResponse, MessageDirection.ServerInternal)]
    public class StatsCollectorStatisticsPageReadResponse : MetaMessage
    {
        public StatisticsStoragePage Page { get; private set; }

        public StatsCollectorStatisticsPageReadResponse(StatisticsStoragePage page)
        {
            Page = page;
        }

        // For deserialization
        StatsCollectorStatisticsPageReadResponse() { }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorStatisticsTimelineRequest, MessageDirection.ServerInternal)]
    public class StatsCollectorStatisticsTimelineRequest : MetaMessage
    {
        public static StatsCollectorStatisticsTimelineRequest Instance = new();

        StatsCollectorStatisticsTimelineRequest() { }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorStatisticsTimelineResponse, MessageDirection.ServerInternal)]
    public class StatsCollectorStatisticsTimelineResponse : MetaMessage
    {
        public StatisticsPageTimeline Minutely { get; private set; }
        public StatisticsPageTimeline Hourly { get; private set; }
        public StatisticsPageTimeline Daily { get; private set; }

        public StatsCollectorStatisticsTimelineResponse(StatisticsPageTimeline minutely, StatisticsPageTimeline hourly, StatisticsPageTimeline daily)
        {
            Minutely = minutely;
            Hourly = hourly;
            Daily  = daily;
        }

        // For deserialization
        StatsCollectorStatisticsTimelineResponse() { }
    }

    #region ActiveEntityLists

    /// <summary>
    /// Information about an active Entity to be displayed in Dashboard in Recently Active entities lists. Use
    /// <see cref="EntityId.Kind"/> of <see cref="IActiveEntityInfo.EntityId"/> to determine which type of an
    /// actor published the info.
    /// </summary>
    [MetaSerializable]
    public interface IActiveEntityInfo
    {
        EntityId EntityId { get; }
        string DisplayName { get; }
        MetaTime CreatedAt { get; }
        MetaTime ActivityAt { get; }
    }

    /// <summary>
    /// Wrapper around an <see cref="IActiveEntityInfo"/> so that we can send it
    /// over an EventStream (which can only handle concrete types)
    /// </summary>
    public sealed class ActiveEntityInfoEnvelope
    {
        public readonly IActiveEntityInfo Value;
        public ActiveEntityInfoEnvelope(IActiveEntityInfo value) { Value = value; }
    }

    /// <summary>
    /// Information about an active player
    /// </summary>
    [MetaSerializableDerived(100)]
    public class ActivePlayerInfo : IActiveEntityInfo
    {
        [MetaMember(1)] public EntityId             EntityId        { get; private set; }
        [MetaMember(2)] public string               DisplayName     { get; private set; }
        [MetaMember(3)] public MetaTime             CreatedAt       { get; private set; }
        [MetaMember(4)] public MetaTime             ActivityAt      { get; private set; }

        [MetaMember(5)] public int                  Level           { get; private set; }
        [MetaMember(6)] public PlayerDeletionStatus DeletionStatus  { get; private set; }
        [MetaMember(7)] public F64                  TotalIapSpend   { get; private set; }
        [MetaMember(8)] public bool                 IsDeveloper     { get; private set; }

        public ActivePlayerInfo() { }
        public ActivePlayerInfo(EntityId entityId, string displayName, MetaTime createdAt, MetaTime activityAt, int level, PlayerDeletionStatus deletionStatus, F64 totalIapSpend, bool isDeveloper)
        {
            EntityId = entityId;
            DisplayName = displayName;
            CreatedAt = createdAt;
            ActivityAt = activityAt;
            Level = level;
            DeletionStatus = deletionStatus;
            TotalIapSpend = totalIapSpend;
            IsDeveloper = isDeveloper;
        }
    }

    /// <summary>
    /// * -> SCM Request to fetch current NumConcurrents
    /// </summary>
    [MetaMessage(MessageCodesCore.StatsCollectorNumConcurrentsRequest, MessageDirection.ServerInternal)]
    public class StatsCollectorNumConcurrentsRequest : EntityAskRequest<StatsCollectorNumConcurrentsResponse>
    {
        public static readonly StatsCollectorNumConcurrentsRequest Instance = new StatsCollectorNumConcurrentsRequest();
    }
    [MetaMessage(MessageCodesCore.StatsCollectorNumConcurrentsResponse, MessageDirection.ServerInternal)]
    public class StatsCollectorNumConcurrentsResponse : EntityAskResponse
    {
        public int NumConcurrents { get; private set; }

        StatsCollectorNumConcurrentsResponse() { }
        public StatsCollectorNumConcurrentsResponse(int numConcurrents)
        {
            NumConcurrents = numConcurrents;
        }
    }

    /// <summary>
    /// Periodically inform the StatsCollectorManager of the recently active entities on each shard.
    /// </summary>
    [MetaMessage(MessageCodesCore.UpdateShardActiveEntityList, MessageDirection.ServerInternal)]
    public class UpdateShardActiveEntityList : MetaMessage
    {
        public string                                           ShardName       { get; private set; }
        public Dictionary<EntityKind, List<IActiveEntityInfo>>  ActiveEntities  { get; private set; }

        UpdateShardActiveEntityList() { }
        public UpdateShardActiveEntityList(string shardName, Dictionary<EntityKind, List<IActiveEntityInfo>> entities)
        {
            ShardName = shardName;
            ActiveEntities = entities;
        }
    }

    /// <summary>
    /// Request a list of recently active entities. StatsCollectorManager responds with a <see cref="ActiveEntitiesResponse"/>.
    /// </summary>
    [MetaMessage(MessageCodesCore.ActiveEntitiesRequest, MessageDirection.ServerInternal)]
    public class ActiveEntitiesRequest : EntityAskRequest<ActiveEntitiesResponse>
    {
        public EntityKind EntityKind { get; private set; }

        ActiveEntitiesRequest() { }
        public ActiveEntitiesRequest(EntityKind entityKind) { EntityKind = entityKind; }
    }

    /// <summary>
    /// Response to a <see cref="ActiveEntitiesRequest"/>. Contains information about recently active entities.
    /// </summary>
    [MetaMessage(MessageCodesCore.ActiveEntitiesResponse, MessageDirection.ServerInternal)]
    public class ActiveEntitiesResponse : EntityAskResponse
    {
        public List<IActiveEntityInfo> ActiveEntities { get; private set; }

        ActiveEntitiesResponse() { }
        public ActiveEntitiesResponse(List<IActiveEntityInfo> activeEntities) { ActiveEntities = activeEntities; }
    }

    #endregion // ActiveEntityLists

    /// <summary>
    /// SCP -> SCM info to update stats on the its pod.
    /// </summary>
    [MetaMessage(MessageCodesCore.StatsCollectorNumConcurrentsUpdate, MessageDirection.ServerInternal)]
    public class StatsCollectorNumConcurrentsUpdate : MetaMessage
    {
        public string   ShardName       { get; private set; }
        public int      NumConcurrents  { get; private set; }
        public MetaTime SampledAt       { get; private set; }

        StatsCollectorNumConcurrentsUpdate() { }
        public StatsCollectorNumConcurrentsUpdate(string shardName, int numConcurrents, MetaTime sampledAt)
        {
            ShardName = shardName;
            NumConcurrents = numConcurrents;
            SampledAt = sampledAt;
        }
    }

    /// <summary>
    /// SCP -> SCM cast message to update experiment statistics
    /// </summary>
    [MetaMessage(MessageCodesCore.StatsCollectorPlayerExperimentAssignmentSampleUpdate, MessageDirection.ServerInternal)]
    public class StatsCollectorPlayerExperimentAssignmentSampleUpdate : MetaMessage
    {
        /// <summary>
        /// A single of the sampled operations.
        /// </summary>
        [MetaSerializable]
        public struct Sample
        {
            [MetaMember(1)] public ExperimentVariantId  IntoVariant;
            [MetaMember(2)] public ExperimentVariantId  FromVariant;
            [MetaMember(3)] public bool                 HasFromVariant;

            public Sample(ExperimentVariantId intoVariant, ExperimentVariantId fromVariant, bool hasFromVariant)
            {
                IntoVariant = intoVariant;
                FromVariant = fromVariant;
                HasFromVariant = hasFromVariant;
            }
        }

        public MetaDictionary<PlayerExperimentId, MetaDictionary<EntityId, Sample>> ExperimentSamples { get; private set; }

        StatsCollectorPlayerExperimentAssignmentSampleUpdate() { }
        public StatsCollectorPlayerExperimentAssignmentSampleUpdate(MetaDictionary<PlayerExperimentId, MetaDictionary<EntityId, Sample>> experimentSample)
        {
            ExperimentSamples = experimentSample;
        }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorSubscribeRequest, MessageDirection.ServerInternal)]
    public class StatsCollectorSubscribeRequest : MetaMessage
    {
        public MetaTime WatermarkTimestamp { get; private set; }

        StatsCollectorSubscribeRequest() { }
        public StatsCollectorSubscribeRequest(MetaTime watermarkTimestamp)
        {
            WatermarkTimestamp = watermarkTimestamp;
        }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorSubscribeResponse, MessageDirection.ServerInternal)]
    public class StatsCollectorSubscribeResponse : MetaMessage
    {
        public StatsCollectorSubscribeResponse()
        {
        }
    }

    [MetaMessage(MessageCodesCore.StatsCollectorUpdateWatermark, MessageDirection.ServerInternal)]
    public class StatsCollectorUpdateWatermark : MetaMessage
    {
        public MetaTime WatermarkTimestamp { get; private set; }

        StatsCollectorUpdateWatermark() { }
        public StatsCollectorUpdateWatermark(MetaTime watermarkTimestamp)
        {
            WatermarkTimestamp = watermarkTimestamp;
        }
    }


    [Table("StatsCollectors")]
    public class PersistedStatsCollector : IPersistedEntity
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
        public byte[]   Payload         { get; set; }   // TaggedSerialized<StatsCollectorState>

        [Required]
        public int      SchemaVersion   { get; set; }   // Schema version for object

        [Required]
        public bool     IsFinal         { get; set; }
    }

    // StatsCollectorState

    [MetaSerializable]
    [MetaBlockedMembers(3, 5)]
    [SupportedSchemaVersions(1, 1)]
    public class StatsCollectorState : ISchemaMigratable
    {
        [MetaMember(1)] public FullMetaActivableStatistics  ActivableStatistics { get; set; } = new FullMetaActivableStatistics();
        [MetaMember(2)] public MetaOffersStatistics         MetaOfferStatistics { get; set; } = new MetaOffersStatistics();
        [MetaMember(8)] public LiveOpsEventsStatistics      LiveOpsEventsStatistics { get; set; } = new LiveOpsEventsStatistics();

        [MetaMember(4)] public MetaDictionary<PlayerExperimentId, PlayerExperimentStatistics> PlayerExperimentStatistics { get; set; } = new MetaDictionary<PlayerExperimentId, PlayerExperimentStatistics>();

        // Accurate tracking of database item counts.
        [MetaMember(6)] public MetaTime                         DatabaseItemCountsUpdatedAt { get; set; }
        [MetaMember(7)] public MetaDictionary<string, int[]>    DatabaseShardItemCounts     { get; set; }
		
        // Historical statistics events metrics (collected based on StatisticsEventsWatermark)
        [MetaMember(10)] public MetaTime                       StatisticsEventsWatermark   { get; set; }
        [MetaMember(11)] public MetaTime                       StatisticsCollectionEpoch   { get; set; }
        [MetaMember(12)] public StatisticsPageWriteBuffer      StatisticsMinuteWriteBuffer { get; set; }
        [MetaMember(13)] public StatisticsPageWriteBuffer      StatisticsHourlyWriteBuffer { get; set; }
        [MetaMember(14)] public StatisticsPageWriteBuffer      StatisticsDailyWriteBuffer  { get; set; }

   }

    public class StatsCollectorInMemoryState
    {
        public struct ProxyConcurrentsStatistics
        {
            public DateTime ExpireAt;
            public int      NumConcurrents;

            public ProxyConcurrentsStatistics(DateTime expireAt, int numConcurrents)
            {
                ExpireAt = expireAt;
                NumConcurrents = numConcurrents;
            }
        }

        public MetaDictionary<string, ProxyConcurrentsStatistics> ProxyConcurrents = new MetaDictionary<string, ProxyConcurrentsStatistics>(); // shard name -> stats
    }

    // StatsCollectorManager

    [EntityConfig]
    internal sealed class StatsCollectorManagerConfig : PersistedEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindCloudCore.StatsCollectorManager;
        public override Type                EntityActorType         => typeof(StatsCollectorManager);
        public override EntityShardGroup    EntityShardGroup        => EntityShardGroup.BaseServices;
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.Service;
        public override IShardingStrategy   ShardingStrategy        => ShardingStrategies.CreateSingletonService();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(5);
    }

    public class StatsCollectorManager : PersistedEntityActor<PersistedStatsCollector, StatsCollectorState>
    {
        public const int                                MaxActiveEntities = 50;

        public static readonly EntityId                 EntityId = EntityId.Create(EntityKindCloudCore.StatsCollectorManager, 0);

        protected override sealed AutoShutdownPolicy    ShutdownPolicy      => AutoShutdownPolicy.ShutdownNever();
        protected override sealed TimeSpan              SnapshotInterval    => TimeSpan.FromMinutes(3);

        StatsCollectorState                             _state;
        StatsCollectorInMemoryState                     _inMemory = new StatsCollectorInMemoryState();

        /// <summary> Watermarks of statistics event writes from each node/SCP. Used to determine how eagerly we can aggregate the events. </summary>
        Dictionary<EntityId, MetaTime>                  _subscriberWatermarks = new Dictionary<EntityId, MetaTime>();

        // Number of active entities on each shard by entity kind
        Dictionary<ClusterNodeAddress, Dictionary<EntityKind, int>> _shardLiveEntityCounts  = new Dictionary<ClusterNodeAddress, Dictionary<EntityKind, int>>();
        static readonly TimeSpan                                    LogStatsInterval        = TimeSpan.FromSeconds(30);
        internal class LogLiveEntityCounts { public static readonly LogLiveEntityCounts Instance = new LogLiveEntityCounts(); }

        // In-memory list of entities that were recently active on all shards
        Dictionary<string, Dictionary<EntityKind, List<IActiveEntityInfo>>> _shardActiveEntities    = new Dictionary<string, Dictionary<EntityKind, List<IActiveEntityInfo>>>();
        Dictionary<EntityKind, List<IActiveEntityInfo>>                     _globalActiveEntities   = new Dictionary<EntityKind, List<IActiveEntityInfo>>();

        // Database item count tracking
        static readonly TimeSpan                        UpdateDatabaseItemCountInterval = TimeSpan.FromMinutes(15);
        internal class UpdateDatabaseItemCounts { public static readonly UpdateDatabaseItemCounts Instance = new UpdateDatabaseItemCounts(); }

        // In-memory count and details of recent errors
        RecentLogEventCounter _recentLoggedErrors = new RecentLogEventCounter(MaxRecentErrorListSize);

        // Time that the collector started
        MetaTime _startTime = MetaTime.Now;

        // Recent errors tracking
        static readonly        TimeSpan      UpdateRecentLoggedErrorsInterval = TimeSpan.FromSeconds(10);
        public static readonly MetaDuration  RecentErrorsKeepDuration         = MetaDuration.FromDays(5);
        public static readonly int           MaxRecentErrorListSize           = 100;
        internal class UpdateRecentLoggedErrors { public static readonly UpdateRecentLoggedErrors Instance = new UpdateRecentLoggedErrors(); }

        List<StatisticsTimeSeriesPageWriter> _statisticsPageWriters;
        DatabaseStatisticsPageStorage        _dbStatisticsPageStorage;

        public StatsCollectorManager(EntityId entityId) : base(entityId)
        {
            // Initialize empty global active entity lists
            foreach (EntityKind kind in EntityKindRegistry.AllValues)
                _globalActiveEntities.Add(kind, new List<IActiveEntityInfo>());
        }

        protected override void PreStart()
        {
            base.PreStart();

            StartPeriodicTimer(TimeSpan.FromSeconds(5), LogStatsInterval, LogLiveEntityCounts.Instance);

            StartPeriodicTimer(TimeSpan.FromSeconds(10), UpdateRecentLoggedErrorsInterval, UpdateRecentLoggedErrors.Instance);

            StartPeriodicTimer(CollectLiveMetricsInterval, CollectLiveMetricsInterval, CollectLiveMetrics.Instance);
            StartPeriodicTimer(CollectStatisticsEventsInterval, CollectStatisticsEventsInterval, CollectStatisticsEvents.Instance);

            // Use manual timer to avoid risking unbounded growth of the inbox, in case the update takes longer than the interval
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(10), _self, UpdateDatabaseItemCounts.Instance, _self);
        }

        protected override sealed async Task Initialize()
        {
            PersistedStatsCollector persisted = await MetaDatabase.Get().TryGetAsync<PersistedStatsCollector>(_entityId.ToString());
            await InitializePersisted(persisted);
            ClusterConnectionManager.SubscribeToClusterEvents(_self);
        }

        protected override sealed Task<StatsCollectorState> InitializeNew()
        {
            StatsCollectorState state = new StatsCollectorState();

            return Task.FromResult(state);
        }

        protected override sealed Task<StatsCollectorState> RestoreFromPersisted(PersistedStatsCollector persisted)
        {
            StatsCollectorState state = DeserializePersistedPayload<StatsCollectorState>(persisted.Payload, resolver: null, logicVersion: null);

            return Task.FromResult(state);
        }

        protected override sealed async Task PostLoad(StatsCollectorState payload, DateTime persistedAt, TimeSpan elapsedTime)
        {
            _state = payload;

            // If we don't yet have database item counts, get an estimate so we always have valid values.
            if (_state.DatabaseShardItemCounts == null)
            {
                _state.DatabaseShardItemCounts = await MetaDatabase.Get(QueryPriority.Normal).EstimateTableItemCountsAsync();
                _state.DatabaseItemCountsUpdatedAt = MetaTime.Now;
            }

            // If there is no existing statistics data, generate some mock data to help development.
            // \todo Put this under some flag
            bool generateMockStatisticsEvents = false; //(_state.StatisticsEventsWatermark == MetaTime.Epoch);
            int  mockDataTimeWindowDays       = 60;
            if (generateMockStatisticsEvents)
            {
                int numMockDataPlayers = 100000;
                _log.Warning("Generating mock data for {NumPlayers} players over {NumDays} days", numMockDataPlayers, mockDataTimeWindowDays);
                await StatsEventsMockDataGenerator.GenerateDebugEventsAsync(numMockDataPlayers, mockDataTimeWindowDays);
                _log.Warning("Mock data generation done!");
            }

            // Initialize business metrics, if not yet initialized
            // \todo Only start tracking at the next full UTC day to avoid issues with partial values
            if (_state.StatisticsEventsWatermark == MetaTime.Epoch)
                _state.StatisticsEventsWatermark = generateMockStatisticsEvents ? MetaTime.Now - MetaDuration.FromDays(mockDataTimeWindowDays) : MetaTime.Now;

            if (_state.StatisticsCollectionEpoch == MetaTime.Epoch)
            {
                // Init epoch to 00:00:00 of the current day (or the day from when we have mock data)
                DateTime epoch = generateMockStatisticsEvents ? DateTime.UtcNow - TimeSpan.FromDays(mockDataTimeWindowDays) : DateTime.UtcNow;
                _state.StatisticsCollectionEpoch = MetaTime.FromDateTime(epoch.Date);

                StatisticsPageTimeline minuteTimeline = new StatisticsPageTimeline(_state.StatisticsCollectionEpoch, StatisticsPageResolutions.Minute);
                StatisticsPageTimeline hourlyTimeline = new StatisticsPageTimeline(_state.StatisticsCollectionEpoch, StatisticsPageResolutions.Hourly);
                StatisticsPageTimeline dailyTimeline = new StatisticsPageTimeline(_state.StatisticsCollectionEpoch, StatisticsPageResolutions.Daily);

                _state.StatisticsMinuteWriteBuffer = new StatisticsPageWriteBuffer(minuteTimeline);
                _state.StatisticsHourlyWriteBuffer = new StatisticsPageWriteBuffer(hourlyTimeline);
                _state.StatisticsDailyWriteBuffer  = new StatisticsPageWriteBuffer(dailyTimeline);
            }

            _dbStatisticsPageStorage = new DatabaseStatisticsPageStorage();

            _state.StatisticsMinuteWriteBuffer.SetBackingStorage(_dbStatisticsPageStorage);
            _state.StatisticsHourlyWriteBuffer.SetBackingStorage(_dbStatisticsPageStorage);
            _state.StatisticsDailyWriteBuffer.SetBackingStorage(_dbStatisticsPageStorage);

            _statisticsPageWriters =
            [
                new StatisticsTimeSeriesPageWriter(_state.StatisticsMinuteWriteBuffer,
                    StatisticsTimeSeriesRegistryBase.Instance.GetTimeSeriesForResolution(StatisticsPageResolutions.Minute), _log),
                new StatisticsTimeSeriesPageWriter(_state.StatisticsHourlyWriteBuffer,
                    StatisticsTimeSeriesRegistryBase.Instance.GetTimeSeriesForResolution(StatisticsPageResolutions.Hourly), _log),
                new StatisticsTimeSeriesPageWriter(_state.StatisticsDailyWriteBuffer,
                    StatisticsTimeSeriesRegistryBase.Instance.GetTimeSeriesForResolution(StatisticsPageResolutions.Daily), _log),
            ];

            // A legacy migration was removed in R29 moving this data from GSP here. However, if user run server
            // on R29, and then checks out to R28 and runs the server again, the legacy migration will run again
            // since the flag indicating the migration was run was cleared in R29. Running the migration again
            // will break the state. Detect and fix this.
            foreach (PlayerExperimentStatistics statistics in _state.PlayerExperimentStatistics.Values)
            {
                if (statistics.PlayerSample == null)
                    statistics.PlayerSample = new List<EntityId>();
            }
        }

        protected override sealed async Task PersistStateImpl(bool isInitial, bool isFinal)
        {
            SchemaMigrator migrator = SchemaMigrationRegistry.Instance.GetSchemaMigrator<StatsCollectorState>();
            _log.Debug("Persisting state (isInitial={IsInitial}, isFinal={IsFinal}, schemaVersion={SchemaVersion})", isInitial, isFinal, migrator.CurrentSchemaVersion);

            // Serialize and compress the state
            byte[] persistedPayload = SerializeToPersistedPayload(_state, resolver: null, logicVersion: null);

            // Persist in database
            PersistedStatsCollector persisted = new PersistedStatsCollector
            {
                EntityId        = _entityId.ToString(),
                PersistedAt     = MetaTime.Now.ToDateTime(),
                Payload         = persistedPayload,
                SchemaVersion   = migrator.CurrentSchemaVersion,
                IsFinal         = isFinal,
            };

            if (isInitial)
                await MetaDatabase.Get().InsertAsync(persisted).ConfigureAwait(false);
            else
                await MetaDatabase.Get().UpdateAsync(persisted).ConfigureAwait(false);
        }

        protected override sealed Task<MetaMessage> OnNewSubscriber(EntitySubscriber subscriber, MetaMessage message)
        {
            if (message is StatsCollectorSubscribeRequest request)
            {
                if (_subscriberWatermarks.ContainsKey(subscriber.EntityId))
                    _log.Warning("Duplicate subscriber {EntityId}", subscriber.EntityId);
                _subscriberWatermarks.AddOrSet(subscriber.EntityId, request.WatermarkTimestamp);
                return Task.FromResult<MetaMessage>(new StatsCollectorSubscribeResponse());
            }
            else
            {
                _log.Error("Invalid subscribe from {EntityId}", subscriber.EntityId);
                return Task.FromResult<MetaMessage>(null);
            }
        }

        /// <summary>
        /// Handler for an <see cref="StatsCollectorProxy"/> unsubscribing normally.
        /// </summary>
        /// <param name="subscriber"></param>
        /// <returns></returns>
        protected sealed override Task OnSubscriberLostAsync(EntitySubscriber subscriber)
        {
            _log.Information("{SubscriberId} unsubscribed ({SubscriberActorRef})", subscriber.EntityId, subscriber.ActorRef);
            _subscriberWatermarks.Remove(subscriber.EntityId);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handler for a <see cref="StatsCollectorProxy"/> unsubscribing unexpectedly,
        /// for example due to networking issue or a node loss.
        /// </summary>
        /// <param name="subscriber"></param>
        /// <returns></returns>
        protected sealed override Task OnSubscriberTerminatedAsync(EntitySubscriber subscriber)
        {
            _log.Warning("Subscriber lost unexpectedly: {SubscriberId} ({SubscriberActorRef})", subscriber.EntityId, subscriber.ActorRef);
            _subscriberWatermarks.Remove(subscriber.EntityId);
            // \todo Implement some kind of grace period to not progress watermark to give the write events
            //       time to propagate in unexpected circumstances.

            return Task.CompletedTask;
        }

        [MessageHandler]
        void HandleStatsCollectorUpdateWatermark(EntityId fromEntityId, StatsCollectorUpdateWatermark update)
        {
            //_log.Info("Received watermark from {EntityId}: {Watermark}", fromEntityId, update.WatermarkTimestamp);
            _subscriberWatermarks.AddOrSet(fromEntityId, update.WatermarkTimestamp);
        }

        [EntityAskHandler]
        StatsCollectorStateResponse HandleStateRequest(StatsCollectorStateRequest _)
        {
            MetaSerialized<StatsCollectorState> serializedState = new MetaSerialized<StatsCollectorState>(_state, MetaSerializationFlags.IncludeAll, logicVersion: null);
            return new StatsCollectorStateResponse(serializedState);
        }

        /// <summary>
        /// Handle report from StatsCollectorProxy concerning statistics about activables.
        /// The reported statistics get combined to StatsCollectorManager's total statistics.
        /// </summary>
        [MessageHandler]
        public void HandleMetaActivableStatisticsInfo(MetaActivableStatisticsInfo info)
        {
            AggregateActivableStatisticsInPlace(
                src: info.ActivableStatistics,
                dst: _state.ActivableStatistics);

            AggregateMetaOffersStatisticsInPlace(
                src: info.MetaOffersStatistics,
                dst: _state.MetaOfferStatistics);
        }

        static void AggregateActivableStatisticsInPlace(FullMetaActivableStatistics src, FullMetaActivableStatistics dst)
        {
            foreach ((MetaActivableKindId kindId, MetaActivableKindStatistics srcKindStats) in src.KindStatistics)
            {
                MetaActivableKindStatistics dstKindStats = StatsUtil.GetOrAddDefaultConstructed(dst.KindStatistics, kindId);
                StatsUtil.AggregateValues(dstKindStats.ActivableStatistics, srcKindStats.ActivableStatistics, MetaActivableStatistics.Sum);
            }
        }

        static void AggregateMetaOffersStatisticsInPlace(MetaOffersStatistics src, MetaOffersStatistics dst)
        {
            foreach ((MetaOfferGroupId groupId, MetaOfferGroupStatistics srcGroupStats) in src.OfferGroups)
            {
                MetaOfferGroupStatistics dstGroupStats = StatsUtil.GetOrAddDefaultConstructed(dst.OfferGroups, groupId);
                StatsUtil.AggregateValues(dstGroupStats.OfferPerGroupStatistics, srcGroupStats.OfferPerGroupStatistics, MetaOfferStatistics.Sum);
            }

            StatsUtil.AggregateValues(dst.Offers, src.Offers, MetaOfferStatistics.Sum);
        }

        [MessageHandler]
        public void HandleLiveOpsEventsStatisticsInfo(LiveOpsEventsStatisticsInfo info)
        {
            foreach ((MetaGuid eventId, int participantCount) in info.EventParticipantCounts)
            {
                if (!_state.LiveOpsEventsStatistics.OccurrenceStatistics.TryGetValue(eventId, out LiveOpsEventOccurrenceStatistics occurrenceStatistics))
                {
                    occurrenceStatistics = new();
                    _state.LiveOpsEventsStatistics.OccurrenceStatistics.Add(eventId, occurrenceStatistics);
                }

                occurrenceStatistics.ParticipantCount += participantCount;
            }
        }

        [EntityAskHandler]
        public StatsCollectorLiveOpsEventStatisticsResponse HandleStatsCollectorLiveOpsEventStatisticsRequest(StatsCollectorLiveOpsEventStatisticsRequest request)
        {
            MetaDictionary<MetaGuid, LiveOpsEventOccurrenceStatistics> occurrenceStatistics = request.OccurrenceIds.ToMetaDictionary(
                occurrenceId => occurrenceId,
                occurrenceId => _state.LiveOpsEventsStatistics.OccurrenceStatistics.GetValueOrDefault(occurrenceId)
                                ?? new());

            return new StatsCollectorLiveOpsEventStatisticsResponse(occurrenceStatistics);
        }

        #region DatabaseItemCounts

        [CommandHandler]
        void HandleUpdateDatabaseItemCounts(UpdateDatabaseItemCounts _)
        {
            IScheduler scheduler = Context.System.Scheduler;

            // Count the items in background. MySQL is quite slow at counting and may take even minutes
            // with a large database. When results are ready, store them in the state.
            ContinueTaskOnActorContext(
                Task.Run(async () =>
                {
                    try
                    {
                        // \note These queries can take minutes with a large database, and thus block the Lowest pipeline for a good while.
                        return await MetaDatabase.Get(QueryPriority.Lowest).GetTableItemCountsAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        // Use manual timer to avoid risking unbounded growth of the inbox, in case the update takes longer than the interval.
                        // \note The duration it takes to run the operation is included interval of the operations, but that's okay.
                        scheduler.ScheduleTellOnce(UpdateDatabaseItemCountInterval, _self, UpdateDatabaseItemCounts.Instance, _self);
                    }
                }),
                (MetaDictionary<string, int[]> shardItemCounts) =>
                {
                    _state.DatabaseShardItemCounts = shardItemCounts;
                    _state.DatabaseItemCountsUpdatedAt = MetaTime.Now;
                },
                ex =>
                {
                    _log.Error("Failed to update database item counts: {Exception}", ex);
                });
        }

        [EntityAskHandler]
        StatsCollectorDatabaseEntityCountResponse HandleStatsCollectorDatabaseEntityCountRequest(StatsCollectorDatabaseEntityCountRequest request)
        {
            PersistedEntityConfig   entityConfig    = EntityConfigRegistry.Instance.GetPersistedConfig(request.EntityKind);
            DatabaseItemSpec        itemSpec        = DatabaseTypeRegistry.GetItemSpec(entityConfig.PersistedType);
            int                     entityCount     = _state.DatabaseShardItemCounts[itemSpec.TableName].Sum();
            return new StatsCollectorDatabaseEntityCountResponse(entityCount, _state.DatabaseItemCountsUpdatedAt);
        }

        #endregion

        #region LiveEntityCounts

        [CommandHandler]
        void HandleLogLiveEntityCounts(LogLiveEntityCounts _)
        {
            if (_log.IsInformationEnabled)
            {
                IEnumerable<string> lines = _shardLiveEntityCounts.Select(kvp => $"  {kvp.Key}: {PrettyPrint.Compact(kvp.Value)}");
                _log.Info("Global entity counts:\n{0}", string.Join("\n", lines));
            }
        }

        [MessageHandler]
        public void HandleUpdateShardEntityCount(UpdateShardLiveEntityCount updateStats)
        {
            //_log.Info("Received entity {0} count: {1}", updateStats.EntityKind, updateStats.EntityCount);

            // Make sure we have LiveEntityCounts dictionary for given shard
            if (!_shardLiveEntityCounts.TryGetValue(updateStats.NodeAddress, out Dictionary<EntityKind, int> liveEntityCounts))
            {
                liveEntityCounts = new Dictionary<EntityKind, int>();
                _shardLiveEntityCounts[updateStats.NodeAddress] = liveEntityCounts;
            }

            // Update entity count
            liveEntityCounts[updateStats.EntityKind] = updateStats.LiveEntityCount;
        }

        [EntityAskHandler]
        StatsCollectorLiveEntityCountPerNodeResponse HandleStatsCollectorLiveEntityCountPerNodeRequest(StatsCollectorLiveEntityCountPerNodeRequest _)
        {
            ClusterConfig clusterConfig = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>().ClusterConfig;

            MetaDictionary<ClusterNodeAddress, MetaDictionary<EntityKind, int>> liveEntityCounts = new MetaDictionary<ClusterNodeAddress, MetaDictionary<EntityKind, int>>();
            foreach ((ClusterNodeAddress key, Dictionary<EntityKind, int> value) in _shardLiveEntityCounts)
            {
                MetaDictionary<EntityKind,int> entityKinds = new MetaDictionary<EntityKind, int>();
                foreach (EntityKind kind in clusterConfig.GetNodeSetConfigForAddress(key).EntityKindMask.GetKinds())
                {
                    if (value.TryGetValue(kind, out int count))
                        entityKinds[kind] = count;
                    else
                        entityKinds[kind] = 0;
                }

                liveEntityCounts.Add(key, entityKinds);
            }

            return new StatsCollectorLiveEntityCountPerNodeResponse(liveEntityCounts);
        }

        [EntityAskHandler]
        StatsCollectorLiveEntityCountResponse HandleStatsCollectorLiveEntityCountRequest(StatsCollectorLiveEntityCountRequest _)
        {
            // Initialize with zero for each EntityKind (ensure there's a value for each known EntityKind)
            MetaDictionary<EntityKind, int> liveEntityCounts = new MetaDictionary<EntityKind, int>();
            foreach (EntityKind kind in EntityKindRegistry.AllValues)
                liveEntityCounts[kind] = 0;

            // Sum entity counts from all shards
            foreach (Dictionary<EntityKind, int> liveEntities in _shardLiveEntityCounts.Values)
            {
                foreach ((EntityKind entityKind, int numLive) in liveEntities)
                    liveEntityCounts[entityKind] += numLive;
            }

            return new StatsCollectorLiveEntityCountResponse(liveEntityCounts);
        }

        #endregion

        #region Active entities

        /// <summary>
        /// Handle a list of time-ordered active entities sent from a shard. These are sent
        /// periodically by the shards.
        /// </summary>
        [MessageHandler]
        public void HandleUpdateShardActiveEntityList(UpdateShardActiveEntityList update)
        {
            // Store per-shard list.
            _shardActiveEntities[update.ShardName] = update.ActiveEntities;

            // Combine the updated per-shard entity lists with the global lists
            foreach ((EntityKind kind, List<IActiveEntityInfo> activeEntities) in update.ActiveEntities)
            {
                _globalActiveEntities[kind] =
                    _globalActiveEntities[kind].Concat(activeEntities)
                    .OrderByDescending(item => item.ActivityAt)
                    .DistinctBy(item => item.EntityId)
                    .Take(MaxActiveEntities)
                    .ToList();
            }
        }

        /// <summary>
        /// Respond to a request for the active entities list
        /// </summary>
        /// <returns></returns>
        [EntityAskHandler]
        public ActiveEntitiesResponse HandleActiveEntitiesRequest(ActiveEntitiesRequest request)
        {
            return new ActiveEntitiesResponse(_globalActiveEntities.GetValueOrDefault(request.EntityKind, new List<IActiveEntityInfo>()));
        }

        #endregion

        #region Concurrents

        [MessageHandler]
        void HandleStatsCollectorNumConcurrentsUpdate(EntityId fromEntityId, StatsCollectorNumConcurrentsUpdate update)
        {
            PruneExpiredProxyConcurrents();

            _inMemory.ProxyConcurrents[update.ShardName] = new StatsCollectorInMemoryState.ProxyConcurrentsStatistics(
                expireAt: update.SampledAt.ToDateTime() + TimeSpan.FromMinutes(5),
                numConcurrents: update.NumConcurrents);
        }

        [EntityAskHandler]
        StatsCollectorNumConcurrentsResponse HandleStatsCollectorNumConcurrentsRequest(StatsCollectorNumConcurrentsRequest _)
        {
            PruneExpiredProxyConcurrents();
            return new StatsCollectorNumConcurrentsResponse(ComputeNumConcurrents());
        }

        int ComputeNumConcurrents()
        {
            // Sum recent data
            int numConcurrents = 0;
            foreach (StatsCollectorInMemoryState.ProxyConcurrentsStatistics proxyStats in _inMemory.ProxyConcurrents.Values)
                numConcurrents += proxyStats.NumConcurrents;
            return numConcurrents;
        }

        void PruneExpiredProxyConcurrents()
        {
            // Prune old data
            bool removedSomething = true;
            while (removedSomething)
            {
                removedSomething = false;
                foreach ((string shardName, StatsCollectorInMemoryState.ProxyConcurrentsStatistics stats) in _inMemory.ProxyConcurrents)
                {
                    if (DateTime.UtcNow >= stats.ExpireAt)
                    {
                        _log.Warning("StatsCollectorProxy on {ShardName} did not update concurrent count within time limit. Assuming previous values are stale and removing its contribution of {NumConcurrents}.", shardName, stats.NumConcurrents);
                        _inMemory.ProxyConcurrents.Remove(shardName);
                        removedSomething = true;
                        break;
                    }
                }
            }
        }

        #endregion // Concurrents

        #region Experiment player samples

        [MessageHandler]
        void HandleStatsCollectorPlayerExperimentAssignmentSampleUpdate(StatsCollectorPlayerExperimentAssignmentSampleUpdate message)
        {
            foreach ((PlayerExperimentId experimentId, MetaDictionary<EntityId, StatsCollectorPlayerExperimentAssignmentSampleUpdate.Sample> playerUpdates) in message.ExperimentSamples)
            {
                if (!_state.PlayerExperimentStatistics.TryGetValue(experimentId, out PlayerExperimentStatistics statistics))
                {
                    statistics = new PlayerExperimentStatistics();
                    _state.PlayerExperimentStatistics.Add(experimentId, statistics);
                }

                foreach ((EntityId playerId, StatsCollectorPlayerExperimentAssignmentSampleUpdate.Sample update) in playerUpdates)
                {
                    if (!update.HasFromVariant)
                    {
                        // new assignment. Add to experiment samples.
                        PlayerExperimentPlayerSampleUtil.InsertPlayerSample(statistics.PlayerSample, playerId);
                    }
                    else
                    {
                        // assignment changed. Remove from old variant samples.
                        if (statistics.Variants.TryGetValue(update.FromVariant, out PlayerExperimentStatistics.VariantStats oldVariantStats))
                        {
                            PlayerExperimentPlayerSampleUtil.RemovePlayerSample(oldVariantStats.PlayerSample, playerId);
                        }
                    }

                    // Insert into new variant samples
                    PlayerExperimentStatistics.VariantStats variantStats;
                    if (!statistics.Variants.TryGetValue(update.IntoVariant, out variantStats))
                    {
                        variantStats = new PlayerExperimentStatistics.VariantStats();
                        statistics.Variants.Add(update.IntoVariant, variantStats);
                    }
                    PlayerExperimentPlayerSampleUtil.InsertPlayerSample(variantStats.PlayerSample, playerId);
                }
            }
        }

        #endregion

        #region Recent logged errors

        [MessageHandler]
        public void HandleRecentLoggedErrorsInfo(RecentLoggedErrorsInfo info)
        {
            // Add log events from proxy
            _recentLoggedErrors.AddBatch(info.RecentLoggedErrors);
        }

        [EntityAskHandler]
        public RecentLoggedErrorsResponse HandleRecentLoggedErrorsRequest(RecentLoggedErrorsRequest request)
        {
            return new RecentLoggedErrorsResponse(
                _recentLoggedErrors.GetCount(),
                _recentLoggedErrors.OverMaxRecentLogEvents,
                RecentErrorsKeepDuration,
                _startTime > (MetaTime.Now - RecentErrorsKeepDuration),
                _startTime,
                _recentLoggedErrors.GetErrorsDetails());
        }

        [CommandHandler]
        void HandleUpdateRecentLoggedErrors(UpdateRecentLoggedErrors update)
        {
            _recentLoggedErrors.Update(RecentErrorsKeepDuration);
        }

        #endregion

        #region Statistics events tracking

        public static readonly TimeSpan CollectLiveMetricsInterval = TimeSpan.FromSeconds(1);
        public class CollectLiveMetrics { public static readonly CollectLiveMetrics Instance = new CollectLiveMetrics(); }

        public static readonly TimeSpan CollectStatisticsEventsInterval = TimeSpan.FromSeconds(1);
        public class CollectStatisticsEvents { public static readonly CollectStatisticsEvents Instance = new CollectStatisticsEvents(); }

        public static readonly MetaDuration FarBehindThreshold      = MetaDuration.FromMinutes(30);         // 30min or more is considered far behind -- use batch restoring to catch-up
        public static readonly MetaDuration CollectDelay            = MetaDuration.FromMilliseconds(100);   // Delay reading of event stream a bit to make sure all events have had time to propagate
        public static readonly MetaDuration BatchCollectInterval    = MetaDuration.FromMinutes(1);          // Time range to collect in catch-up mode
        public static readonly MetaDuration OnlineCollectInterval   = MetaDuration.FromSeconds(1);          // Time range to collect in regular mode

        static MetaTime TruncateToCollectInterval(MetaTime time, MetaDuration interval) =>
            MetaTime.FromMillisecondsSinceEpoch(time.MillisecondsSinceEpoch / interval.Milliseconds * interval.Milliseconds);

        MetaTime ResolveAllowCollectUntilWatermark()
        {
            // If no subscribers, use the old watermark -- this can happen when SCM is starting
            if (_subscriberWatermarks.Count == 0)
                return _state.StatisticsEventsWatermark;

            // \todo Figure out if there are missing nodes. If so, be conservative with watermark.

            // In healthy state, use the oldest subscriber node watermark
            return _subscriberWatermarks.Values.Min();
        }

        /// <summary>
        /// Update all live metrics, i.e., metrics that depend on the current state of
        /// the system. These are independent of the statistics events, so we process
        /// them independently.
        /// </summary>
        /// <param name="_"></param>
        [CommandHandler]
        public async Task HandleCollectLiveMetrics(CollectLiveMetrics _)
        {
            // Update concurrents (CCU).
            int numConcurrents = ComputeNumConcurrents();

            await StatisticsEventWriter.WriteEventAsync(time => new StatisticsEventLiveMetrics(time, numConcurrents));
        }

        /// <summary>
        /// Update all metrics derived from the statistics events. These metrics update based on
        /// safe watermarks collected from all nodes in the cluster: each node reports the timestamp
        /// such that any events older than that have already been written into the database. Thus,
        /// we can read all events up to the oldest of these timestamps.
        ///
        /// There are two modes of collecting the events, depending on how far behind the aggregation
        /// is:
        /// - Catch-up mode, if more than 30min behind wall clock: Read in one minute chunks to speed
        ///   up processing but use the read-only database replica to avoid putting pressure on the
        ///   read-write replica. This typically handles about a day of events in a second.
        /// - Online mode, if less than 30min behind wall clock: Read in one second chunks to keep
        ///   read latency low to achieve close to real-time metrics. Read the events from the read-write
        ///   db replica to ensure the watermarking gives consistent results as we can't control the
        ///   replication delay between the db instances.
        /// </summary>
        /// <param name="_"></param>
        /// <returns></returns>
        [CommandHandler]
        public async Task HandleCollectStatisticsEvents(CollectStatisticsEvents _)
        {
            MetaDatabase db = MetaDatabase.Get(QueryPriority.Low);

            // Check if we should use batch catch-up (when far behind) or online aggregation (otherwise)
            MetaTime        now             = MetaTime.Now;
            bool            isFarBehind     = (now - _state.StatisticsEventsWatermark) >= FarBehindThreshold;
            MetaDuration    collectInterval = isFarBehind ? BatchCollectInterval : OnlineCollectInterval;

            // Determine range to collect:
            // - For online aggregation, use 1sec ranges (on read-write replica) to get up-to-date values and get consistent results.
            // - For catch-up aggregation, use 1min ranges (on read-only replica) to make progress faster.
            MetaTime        rangeFrom          = _state.StatisticsEventsWatermark;
            MetaTime        rangeTo            = TruncateToCollectInterval(rangeFrom + collectInterval, collectInterval);
            MetaTime        allowCollectUntil  = ResolveAllowCollectUntilWatermark() - CollectDelay;
            DatabaseReplica replica             = isFarBehind ? DatabaseReplica.ReadOnly : DatabaseReplica.ReadWrite;

            // If catching up, print an event for each day of data
            if (isFarBehind && (rangeTo.MillisecondsSinceEpoch % 86400000) == 0)
                _log.Info("Statistics aggregator catching up, now at {Timestamp}", rangeTo);

            // Collect an interval if allowed to (while respecting the collection delay)
            if (rangeTo <= allowCollectUntil)
            {
                //_log.Information("Collecting range {Start}..{End}, allowUntil={AllowUntil}", rangeFrom, rangeTo, allowCollectUntil);

                // Handle all events in the range (using streaming to avoid reading all the events into memory)
                IAsyncEnumerable<PersistedStatisticsEvent> events = await db.StreamStatisticsEventsAsync(replica, rangeFrom, rangeTo);
                await foreach (PersistedStatisticsEvent persisted in events)
                {
                    StatisticsEventBase ev = MetaSerialization.DeserializeTagged<StatisticsEventBase>(persisted.Payload, MetaSerializationFlags.IncludeAll, resolver: null, logicVersion: null);
                    //_log.Debug("Handle statistics event: {Event}", PrettyPrint.Compact(ev)); -- disabled due to being noisy, enable as needed for debugging purposes
                    foreach (StatisticsTimeSeriesPageWriter writer in _statisticsPageWriters)
                        writer.Write(ev);
                }

                // Flush any final pages to the database.
                foreach (StatisticsTimeSeriesPageWriter writer in _statisticsPageWriters)
                    await writer.FlushAsync();

                // Update gather watermark (as we've processed events up to that point)
                _state.StatisticsEventsWatermark = rangeTo;
            }

            // \todo Purge old statistics events

            // If can collect more, send a message to continue another chunk immediately
            if (rangeTo + collectInterval < allowCollectUntil)
            {
                _self.Tell(CollectStatisticsEvents.Instance);
                //_log.Debug("Catching up statistics events aggregation.. now at {Timestamp}", rangeTo);
            }
        }

        [EntityAskHandler]
        StatsCollectorStatisticsTimelineResponse HandleTimelineRequest(StatsCollectorStatisticsTimelineRequest _)
        {
            return new StatsCollectorStatisticsTimelineResponse(
                _state.StatisticsMinuteWriteBuffer.Timeline,
                _state.StatisticsHourlyWriteBuffer.Timeline,
                _state.StatisticsDailyWriteBuffer.Timeline);
        }

        [EntityAskHandler]
        async Task<StatsCollectorStatisticsPageReadResponse> HandlePageRequest(StatsCollectorStatisticsPageReadRequest req)
        {
            string pageId = req.PageId;

            if (_state.StatisticsMinuteWriteBuffer.Exists(pageId))
                return new StatsCollectorStatisticsPageReadResponse(await _state.StatisticsMinuteWriteBuffer.TryGetAsync(pageId));
            if (_state.StatisticsHourlyWriteBuffer.Exists(pageId))
                return new StatsCollectorStatisticsPageReadResponse(await _state.StatisticsHourlyWriteBuffer.TryGetAsync(pageId));
            if (_state.StatisticsDailyWriteBuffer.Exists(pageId))
                return new StatsCollectorStatisticsPageReadResponse(await _state.StatisticsDailyWriteBuffer.TryGetAsync(pageId));

            return new StatsCollectorStatisticsPageReadResponse(null);
        }

        #endregion

        [CommandHandler]
        void ReceiveClusterChangedEvent(ClusterConnectionManager.ClusterChangedEvent clusterChanged)
        {
            // When node leaves the cluster, remove it from entity counts.
            HashSet<ClusterNodeAddress> clusterNodes = new HashSet<ClusterNodeAddress>();
            foreach (ClusterConnectionManager.ClusterChangedEvent.ClusterMember member in clusterChanged.Members)
                clusterNodes.Add(member.Address);

            foreach (ClusterNodeAddress nodeAddress in _shardLiveEntityCounts.Keys.ToArray())
            {
                if (!clusterNodes.Contains(nodeAddress))
                    _shardLiveEntityCounts.Remove(nodeAddress);
            }

            // When node enters the cluster, add empty row for it immediately. Don't wait for the Proxy to provide initial info.
            foreach (ClusterNodeAddress clusterNode in clusterNodes)
            {
                if (!_shardLiveEntityCounts.ContainsKey(clusterNode))
                {
                    _shardLiveEntityCounts[clusterNode] = new Dictionary<EntityKind, int>();
                }
            }
        }
    }
}
