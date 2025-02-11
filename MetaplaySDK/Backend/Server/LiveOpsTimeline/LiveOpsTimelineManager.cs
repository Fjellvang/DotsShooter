// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace Metaplay.Server.LiveOpsTimeline
{
    [EntityConfig]
    internal sealed class LiveOpsTimelineManagerConfig : PersistedEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindCloudCore.LiveOpsTimelineManager;
        public override Type                EntityActorType         => typeof(LiveOpsTimelineManager);
        public override EntityShardGroup    EntityShardGroup        => EntityShardGroup.BaseServices;
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.Service;
        public override IShardingStrategy   ShardingStrategy        => ShardingStrategies.CreateSingletonService();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(10);
    }

    [Table("LiveOpsTimelineManagers")]
    public class PersistedLiveOpsTimelineManager : IPersistedEntity
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
        public byte[]   Payload         { get; set; }

        [Required]
        public int      SchemaVersion   { get; set; }

        [Required]
        public bool     IsFinal         { get; set; }
    }

    [MetaSerializable]
    [SupportedSchemaVersions(1, 1)]
    public class LiveOpsTimelineManagerState : ISchemaMigratable
    {
        [MetaMember(1)] public Timeline.TimelineState Timeline { get; set; } = LiveOpsTimeline.Timeline.TimelineState.CreateInitialState();
        [MetaMember(2)] public LiveOpsEventsGlobalState LiveOpsEvents { get; set; } = new LiveOpsEventsGlobalState();
        [MetaMember(3)] public bool HasMigratedLiveOpsEventsFromGlobalState { get; set; } = false;
    }

    [MetaMessage(MessageCodesCore.LiveOpsTimelineManagerSubscribeRequest, MessageDirection.ServerInternal)]
    public class LiveOpsTimelineManagerSubscribeRequest : MetaMessage
    {
    }

    [MetaMessage(MessageCodesCore.LiveOpsTimelineManagerSubscribeResponse, MessageDirection.ServerInternal)]
    public class LiveOpsTimelineManagerSubscribeResponse : MetaMessage
    {
        public MetaSerialized<LiveOpsTimelineManagerState> State;

        [MetaDeserializationConstructor]
        public LiveOpsTimelineManagerSubscribeResponse(MetaSerialized<LiveOpsTimelineManagerState> state)
        {
            State = state;
        }
    }

    public partial class LiveOpsTimelineManager : PersistedEntityActor<PersistedLiveOpsTimelineManager, LiveOpsTimelineManagerState>
    {
        public static readonly EntityId EntityId = EntityId.Create(EntityKindCloudCore.LiveOpsTimelineManager, 0);

        protected LiveOpsTimelineManagerState _state;

        public LiveOpsTimelineManager(EntityId entityId)
            : base(entityId)
        {
        }

        protected override void PreStart()
        {
            base.PreStart();

            LiveOpsEventsPreStart();
        }

        protected override AutoShutdownPolicy ShutdownPolicy => AutoShutdownPolicy.ShutdownNever();
        protected override TimeSpan SnapshotInterval => TimeSpan.FromMinutes(3);

        protected override sealed async Task Initialize()
        {
            PersistedLiveOpsTimelineManager persisted = await MetaDatabase.Get().TryGetAsync<PersistedLiveOpsTimelineManager>(_entityId.ToString());
            await InitializePersisted(persisted);
        }

        protected override Task<LiveOpsTimelineManagerState> InitializeNew()
        {
            LiveOpsTimelineManagerState state = new LiveOpsTimelineManagerState();
            return Task.FromResult(state);
        }

        protected override sealed Task<LiveOpsTimelineManagerState> RestoreFromPersisted(PersistedLiveOpsTimelineManager persisted)
        {
            LiveOpsTimelineManagerState state = DeserializePersistedPayload<LiveOpsTimelineManagerState>(persisted.Payload, resolver: null, logicVersion: null);
            return Task.FromResult(state);
        }

        protected override sealed Task PostLoad(LiveOpsTimelineManagerState payload, DateTime persistedAt, TimeSpan elapsedTime)
        {
            _state = payload;

            LiveOpsEventsPostLoad();

            return Task.CompletedTask;
        }

        protected override sealed async Task PersistStateImpl(bool isInitial, bool isFinal)
        {
            _log.Debug("Persisting state (isInitial={IsInitial}, isFinal={IsFinal}, schemaVersion={SchemaVersion})", isInitial, isFinal, CurrentSchemaVersion);

            byte[] persistedPayload = SerializeToPersistedPayload(_state, resolver: null, logicVersion: null);

            PersistedLiveOpsTimelineManager persisted = new PersistedLiveOpsTimelineManager
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

        protected override sealed Task<MetaMessage> OnNewSubscriber(EntitySubscriber subscriber, MetaMessage message)
        {
            MetaSerialized<LiveOpsTimelineManagerState> serialized = new MetaSerialized<LiveOpsTimelineManagerState>(_state, MetaSerializationFlags.IncludeAll, logicVersion: null);
            return Task.FromResult<MetaMessage>(new LiveOpsTimelineManagerSubscribeResponse(serialized));
        }
    }
}
