// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server.WorkloadBalancing
{
    [EntityConfig]
    class WorkloadTrackingServiceEntityConfig : EphemeralEntityConfig
    {
        public override EntityKind        EntityKind           => EntityKindCloudCore.LoadTracker;
        public override Type              EntityActorType      => typeof(WorkloadTrackingServiceActor);
        public override EntityShardGroup  EntityShardGroup     { get; } = EntityShardGroup.BaseServices;
        public override NodeSetPlacement  NodeSetPlacement     { get; } = NodeSetPlacement.All;
        public override IShardingStrategy ShardingStrategy     => ShardingStrategies.CreateDynamicService();
        public override TimeSpan          ShardShutdownTimeout => TimeSpan.FromSeconds(10);
    }

    [MetaMessage(MessageCodesCore.InternalLoadTrackingSubscribeRequest, MessageDirection.ServerInternal)]
    public class WorkloadTrackingSubscribeRequest : MetaMessage { }

    [MetaMessage(MessageCodesCore.InternalLoadTrackingSnapshotRequest, MessageDirection.ServerInternal)]
    public class WorkloadTrackingSnapshotRequest : EntityAskRequest<WorkloadTrackingSnapshot> { }

    [MetaMessage(MessageCodesCore.InternalLoadTrackingSnapshot, MessageDirection.ServerInternal)]
    public class WorkloadTrackingSnapshot : EntityAskResponse
    {
        [MetaMember(1)] public MetaDictionary<EntityShardId, WorkloadBase> Workloads;

        [MetaDeserializationConstructor]
        public WorkloadTrackingSnapshot(MetaDictionary<EntityShardId, WorkloadBase> workloads)
        {
            Workloads = workloads;
        }
    }

    [MetaMessage(MessageCodesCore.InternalLoadTrackingUpdateLeader, MessageDirection.ServerInternal)]
    public class WorkloadTrackingUpdateLeader : MetaMessage
    {
        [MetaMember(1)] public WorkloadBase Workload;

        [MetaMember(2)] public EntityId EntityId;

        [MetaDeserializationConstructor]
        public WorkloadTrackingUpdateLeader(WorkloadBase workload, EntityId entityId)
        {
            Workload = workload;
            EntityId = entityId;
        }
    }

    public class WorkloadTrackingServiceActor : EphemeralEntityActor
    {
        public static EntityId LeaderEntityId = EntityId.Create(EntityKindCloudCore.LoadTracker, 0);

        bool                     _isLeader;
        static readonly TimeSpan _interval = TimeSpan.FromMilliseconds(250);
        static readonly TimeSpan _resubscribeRetryCooldown = TimeSpan.FromSeconds(1);

        internal MetaDictionary<EntityShardId, WorkloadBase> Workloads = new MetaDictionary<EntityShardId, WorkloadBase>();

        EntitySubscription          _subscription;
        DateTime                    _nextResubscribeAt = DateTime.UnixEpoch;

        WorkloadSchedulingUtility _workloadSchedulingUtility;

        readonly ClusterConfig     _clusterConfig;
        readonly IShardingStrategy _shardingStrategy;

        static readonly Counter _gameWorkloadInc   = Metrics.CreateCounter("game_workload_inc_total", "", "property");
        static readonly Counter _gameWorkloadDec   = Metrics.CreateCounter("game_workload_dec_total", "", "property");
        static readonly Gauge   _gameWorkloadGauge = Metrics.CreateGauge("game_workload_gauge", "", "property");

        protected override AutoShutdownPolicy ShutdownPolicy => AutoShutdownPolicy.ShutdownNever();

        public class CollectLoadTick
        {
            public static CollectLoadTick Instance { get; } = new CollectLoadTick();
            CollectLoadTick() { }
        }

        public WorkloadTrackingServiceActor(EntityId entityId) : base(entityId)
        {
            ClusteringOptions clusterOpts = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>();
            _clusterConfig    = clusterOpts.ClusterConfig;
            _shardingStrategy = EntityConfigRegistry.Instance.GetConfig(_entityId.Kind).ShardingStrategy;

            _isLeader = _entityId.Value == 0;

            _workloadSchedulingUtility = new WorkloadSchedulingUtility(this, EntityConfigRegistry.Instance.GetConfig(_entityId.Kind).ShardingStrategy);

            if (_isLeader)
                ClusterConnectionManager.SubscribeToClusterEvents(_self);
        }

        protected override async Task Initialize()
        {
            if (!_isLeader)
            {
                (EntitySubscription subscription, WorkloadTrackingSnapshot snapshot) = await SubscribeToAsync<WorkloadTrackingSnapshot>(LeaderEntityId, EntityTopic.Member, new WorkloadTrackingSubscribeRequest());
                Workloads                                                            = snapshot.Workloads;
                _subscription                                                        = subscription;
            }

            StartPeriodicTimer(_interval, _interval, CollectLoadTick.Instance);
        }

        [CommandHandler]
        Task ReceiveClusterChangedEvent(ClusterConnectionManager.ClusterChangedEvent arg)
        {
            bool anyChanged = false;

            // Remove all nodes that disappeared/disconnected
            // \note: We mutate the Workloads so we iterate over a copy of the keys
            foreach (EntityShardId entityShardId in Workloads.Keys.ToList())
            {
                if (_clusterConfig.TryGetNodeAddressForShardId(entityShardId, out ClusterNodeAddress nodeAddress))
                {
                    bool found = false;

                    foreach (ClusterConnectionManager.ClusterChangedEvent.ClusterMember clusterMember in arg.Members)
                    {
                        if (clusterMember.Address == nodeAddress && clusterMember.IsConnected)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        anyChanged |= FindAndRemoveWorkload(nodeAddress);
                }
            }

            if (anyChanged)
                PublishMessage(EntityTopic.Member, new WorkloadTrackingSnapshot(Workloads));

            return Task.CompletedTask;
        }

        bool FindAndRemoveWorkload(ClusterNodeAddress clusterNodeAddress)
        {
            if (_clusterConfig.ResolveNodeShardId(EntityKindCloudCore.LoadTracker, clusterNodeAddress, out EntityShardId shardId))
                return Workloads.Remove(shardId, out _);

            return false;
        }

        [CommandHandler]
        Task HandleCollectLoadTick(CollectLoadTick _)
        {
            return CollectLoadAndUpdateLeader();
        }

        [MessageHandler]
        void HandleSnapshot(EntityId _, WorkloadTrackingSnapshot message)
        {
            if (_isLeader)
                return;

            Workloads = message.Workloads;
        }

        [EntityAskHandler]
        WorkloadTrackingSnapshot HandleSnapshotRequest(WorkloadTrackingSnapshotRequest _)
        {
            return new WorkloadTrackingSnapshot(Workloads);
        }

        [MessageHandler]
        void HandleUpdateLeader(EntityId entityId, WorkloadTrackingUpdateLeader message)
        {
            if (!_isLeader)
                return;

            // Update workload from a follower node.
            EntityShardId shardId   = _shardingStrategy.ResolveShardId(entityId);
            bool          isInitial = UpdateWorkloadForEntity(shardId, message.Workload);
            if (isInitial)
            {
                _log.Info("Initial workload info received from {EntityId} (nodeset={NodeSetIndex}), node={NodeIndex})", entityId, shardId.NodeSetIndex, shardId.NodeIndex);
            }
        }

        /// <summary>
        /// Returns true if the update was the initial update for the entity.
        /// </summary>
        bool UpdateWorkloadForEntity(EntityShardId shardId, WorkloadBase workload)
        {
            bool wasInitial;

            // Null workload means that the node has no information on the load state and
            // it's not available for the loadbalancing. Not-available nodes are not present
            // in the Workloads.
            if (workload == null)
            {
                Workloads.Remove(shardId);
                wasInitial = false;
            }
            else
            {
                wasInitial = Workloads.AddOrReplace(shardId, workload);
            }

            return wasInitial;
        }

        async Task CollectLoadAndUpdateLeader()
        {
            WorkloadCollectorBase workloadCollector = IntegrationRegistry.Get<WorkloadCollectorBase>();
            if (workloadCollector != null)
            {
                EntityShardId shardId = _shardingStrategy.ResolveShardId(_entityId);

                WorkloadBase workloadBase;
                try
                {
                    workloadBase = await workloadCollector.Collect();
                }
                catch (Exception ex)
                {
                    _log.Warning("Collecting workload failed due to exception, trying again in {Interval}, {Exception}", _interval, ex);
                    return;
                }

                PublishMetricsForWorkload(workloadBase);

                // Leader: Update local node state.
                // Follower: Speculate local node state. This can get overwritten by leader's update
                //           but it will be eventually broadcast by Leader anyway.
                UpdateWorkloadForEntity(shardId, workloadBase);

                if (!_isLeader)
                {
                    if (_subscription == null && DateTime.UtcNow >= _nextResubscribeAt)
                    {
                        try
                        {
                            (EntitySubscription subscription, WorkloadTrackingSnapshot _) =
                                await SubscribeToAsync<WorkloadTrackingSnapshot>(
                                    LeaderEntityId,
                                    EntityTopic.Member,
                                    new WorkloadTrackingSubscribeRequest());
                            _subscription = subscription;
                        }
                        catch (Exception ex)
                        {
                            _nextResubscribeAt = DateTime.UtcNow + _resubscribeRetryCooldown;
                            _log.Warning("Subscribing to leader failed due to exception, trying again in {Interval}, {Exception}", _resubscribeRetryCooldown, ex);
                            return;
                        }
                    }

                    if (_subscription != null)
                        SendMessage(_subscription, new WorkloadTrackingUpdateLeader(workloadBase, _entityId));
                }
                else
                {
                    PublishMessage(EntityTopic.Member, new WorkloadTrackingSnapshot(Workloads));
                }
            }
        }

        static void PublishMetricsForWorkload(WorkloadBase workload)
        {
            if (workload != null && MetaSerializerTypeRegistry.TryGetTypeSpec(workload.GetType(), out MetaSerializableType spec))
            {
                foreach (MetaSerializableMember mem in spec.Members)
                {
                    object val = mem.GetValue(workload);

                    switch (val)
                    {
                        case WorkloadCounter counter:
                            _gameWorkloadDec.WithLabels(mem.Name).IncTo(counter.DecrCounter);
                            _gameWorkloadInc.WithLabels(mem.Name).IncTo(counter.Counter);
                            break;
                        case WorkloadGauge gauge:
                            _gameWorkloadGauge.WithLabels(mem.Name).IncTo(gauge.Value);
                            break;
                    }
                }
            }
        }

        protected override Task<MetaMessage> OnNewSubscriber(EntitySubscriber subscriber, MetaMessage message)
        {
            if (_isLeader)
                return Task.FromResult<MetaMessage>(new WorkloadTrackingSnapshot(Workloads));

            return Task.FromResult<MetaMessage>(null);
        }

        protected override Task OnSubscriptionKickedAsync(EntitySubscription subscription, MetaMessage message)
        {
            _subscription = null;
            return base.OnSubscriptionKickedAsync(subscription, message);
        }

        protected override Task OnSubscriptionLostAsync(EntitySubscription subscription)
        {
            _subscription = null;
            return base.OnSubscriptionLostAsync(subscription);
        }
    }
}
