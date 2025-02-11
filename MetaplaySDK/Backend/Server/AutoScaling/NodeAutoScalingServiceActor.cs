// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Json;
using Metaplay.Core.Model;
using Metaplay.Core.TypeCodes;
using Metaplay.Server.WorkloadBalancing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server.AutoScaling
{
    [MetaMessage(MessageCodesCore.InternalAutoScalingSnapshot, MessageDirection.ServerInternal)]
    public class NodeAutoScalingSnapshot : EntityAskResponse
    {
        [MetaMember(1)] public Dictionary<string, NodeSetScaling>  NodeSets;

        public NodeAutoScalingSnapshot(MetaDictionary<NodeSetConfig, NodeSetScaling> nodeSets)
        {
            NodeSets = nodeSets.ToDictionary(x => x.Key.ShardName, x => x.Value);
        }

        [MetaDeserializationConstructor]
        NodeAutoScalingSnapshot(Dictionary<string, NodeSetScaling> nodeSets)
        {
            NodeSets = nodeSets;
        }
    }

    [MetaMessage(MessageCodesCore.InternalAutoScalingSnapshotRequest, MessageDirection.ServerInternal)]
    public class NodeAutoScalingSnapshotRequest : EntityAskRequest<NodeAutoScalingSnapshot> { }

    [MetaSerializable]
    public class NodeSetScaling
    {
        [MetaSerializable]
        public class NodeInfo
        {
            [MetaMember(1)] public DateTimeOffset? LastWorkAt           { get; private set; }
            [MetaMember(2)] public DateTimeOffset? GracePeriodEndTime   { get; private set; }
            [MetaMember(3)] public NodeState       State                { get; private set; } = NodeState.Dead;
            [MetaMember(4)] public DateTimeOffset? KillingStartedAtTime { get; private set; }

            [MetaDeserializationConstructor]
            NodeInfo(DateTimeOffset? lastWorkAt, DateTimeOffset? gracePeriodEndTime, NodeState state, DateTimeOffset? killingStartedAtTime) : base()
            {
                LastWorkAt           = lastWorkAt;
                State                = state;
                KillingStartedAtTime = killingStartedAtTime;
                GracePeriodEndTime   = gracePeriodEndTime;
            }

            public NodeInfo()
            {
                var options = RuntimeOptionsRegistry.Instance.GetCurrent<AutoScalingRuntimeOptions>();
                UpdateLastWorkAt(DateTimeOffset.UtcNow);
                UpdateState(NodeState.Working);
                UpdateGracePeriod(options.NodeStartGracePeriod);
            }

            public bool IsInGracePeriod()
            {
                return DateTimeOffset.UtcNow < GracePeriodEndTime;
            }

            internal void ChangeStateToKilling()
            {
                UpdateState(NodeState.Killing);
                KillingStartedAtTime = DateTimeOffset.UtcNow;
            }

            internal void UpdateGracePeriod(TimeSpan length)
            {
                GracePeriodEndTime = DateTimeOffset.UtcNow + length;
            }

            internal void UpdateLastWorkAt(DateTimeOffset lastSync)
            {
                LastWorkAt = lastSync;
            }

            internal void UpdateState(NodeState state)
            {
                State = state;
            }
        }

        [MetaSerializable]
        public enum NodeState
        {
            /// <summary>
            /// This node is currently not alive, e.g. never started or has been shutdown.
            /// </summary>
            Dead,
            /// <summary>
            /// This node is current alive, this node is expected to actively be doing work.
            /// </summary>
            Working,
            /// <summary>
            /// This node is a candidate to be shutdown, therefore we no longer are scheduling work.
            /// </summary>
            Draining,
            /// <summary>
            /// This node is actively being killed, likely means that the game server instance is in process of shutting down.
            /// </summary>
            Killing,
        }

        [MetaDeserializationConstructor]
        NodeSetScaling(MetaDictionary<ClusterNodeAddress, NodeInfo> nodes, int currentNodeCount, int desiredNodeCount)
        {
            Nodes            = nodes;
            CurrentNodeCount = currentNodeCount;
            DesiredNodeCount = desiredNodeCount;
        }

        public NodeSetScaling(MetaDictionary<ClusterNodeAddress, NodeInfo> nodes)
        {
            Nodes            = nodes;
            CurrentNodeCount = nodes.Count;
            DesiredNodeCount = CurrentNodeCount;
        }

        [MetaMember(1)] public MetaDictionary<ClusterNodeAddress, NodeInfo> Nodes { get; }

        [MetaMember(2)] public int CurrentNodeCount { get; private set; }
        [MetaMember(3)] public int DesiredNodeCount { get; private set; }

        internal void UpdateCurrentNodeCount()
        {
            CurrentNodeCount = Nodes.Count(x => x.Value.State != NodeState.Dead);
        }

        public bool IsBeingUpscaled() => DesiredNodeCount > CurrentNodeCount;
        public bool IsBeingDownscaled() => DesiredNodeCount < CurrentNodeCount;

        internal void IncrementDesiredNodeCount() => DesiredNodeCount++;
        internal void DecrementDesiredNodeCount() => DesiredNodeCount--;
    }

    [EntityConfig]
    class AutoScalingEntityConfig : EphemeralEntityConfig
    {
        public override EntityKind        EntityKind           => EntityKindCloudCore.AutoScaling;
        public override Type              EntityActorType      => typeof(NodeAutoScalingServiceActor);
        public override EntityShardGroup  EntityShardGroup     => EntityShardGroup.BaseServices;
        public override NodeSetPlacement  NodeSetPlacement     => NodeSetPlacement.Service;
        public override IShardingStrategy ShardingStrategy     => ShardingStrategies.CreateSingletonService();
        public override TimeSpan          ShardShutdownTimeout => TimeSpan.FromSeconds(10);
    }

    public class NodeAutoScalingServiceActor : EphemeralEntityActor
    {
        const              long               RetryKillingNodeAfterSeconds = 60;
        public static      EntityId           EntityId       => EntityId.Create(EntityKindCloudCore.AutoScaling, 0);
        protected override AutoShutdownPolicy ShutdownPolicy { get; } = AutoShutdownPolicy.ShutdownNever();

        DynamicServiceShardingStrategy _shardingStrategy = new DynamicServiceShardingStrategy();

        public class AutoScalingTick
        {
            public static AutoScalingTick Instance { get; } = new AutoScalingTick();
            AutoScalingTick() { }
        }

        /// <summary>
        /// LUT of global nodeset index to scaling settings for the nodeset
        /// </summary>
        MetaDictionary<NodeSetConfig, NodeSetScaling> _nodeSetScalingTable = new MetaDictionary<NodeSetConfig, NodeSetScaling>();
        ClusterConfig     _clusterConfig;
        IInfraIntegration _infraIntegration;

        public NodeAutoScalingServiceActor(EntityId entityId) : base(entityId)
        {
        }

        protected override async Task Initialize()
        {
            var autoScalingOptions = RuntimeOptionsRegistry.Instance.GetCurrent<AutoScalingRuntimeOptions>();

            if (!autoScalingOptions.Enabled)
                return;

            _infraIntegration = IntegrationRegistry.Create<IInfraIntegration>();

            ClusterConnectionManager.ClusterDebugStatusResponse response = await ClusterConnectionManager.GetClusterStatusAsync();

            ClusteringOptions clusterOpts        = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>();
            _clusterConfig = clusterOpts.ClusterConfig;

            foreach (NodeSetConfig nodeSet in _clusterConfig.EnumerateNodeSets())
            {
                if (nodeSet.ScalingMode == NodeSetScalingMode.DynamicLinear)
                {
                    MetaDictionary<ClusterNodeAddress, NodeSetScaling.NodeInfo> nodes = new MetaDictionary<ClusterNodeAddress, NodeSetScaling.NodeInfo>();
                    for (int i = 0; i < nodeSet.DynamicLinearMaxNodeCount; i++)
                    {
                        ClusterNodeAddress nodeAddress = nodeSet.ResolveNodeAddress(i);
                        if (response.CurrentCluster.Members.Any(x => x.Address == nodeAddress))
                        {
                            NodeSetScaling.NodeInfo nodeInfo = new NodeSetScaling.NodeInfo();
                            nodes.Add(nodeAddress, nodeInfo);
                        }
                    }

                    _nodeSetScalingTable.Add(nodeSet, new NodeSetScaling(nodes));
                }
            }

            ClusterConnectionManager.SubscribeToClusterEvents(_self);

            TimeSpan interval = autoScalingOptions.UpdateInterval;
            StartPeriodicTimer(interval, interval, AutoScalingTick.Instance);
        }

        [EntityAskHandler]
        NodeAutoScalingSnapshot HandleSnapshotRequest(NodeAutoScalingSnapshotRequest _)
        {
            return new NodeAutoScalingSnapshot(_nodeSetScalingTable);
        }

        [CommandHandler]
        void ReceiveClusterChangedEvent(ClusterConnectionManager.ClusterChangedEvent arg)
        {
            var options = RuntimeOptionsRegistry.Instance.GetCurrent<AutoScalingRuntimeOptions>();
            if (!options.Enabled)
                return;

            try
            {
                // Remove all nodes that disappeared
                foreach ((NodeSetConfig _, NodeSetScaling value) in _nodeSetScalingTable)
                {
                    List<ClusterNodeAddress> addressesToRemove = new List<ClusterNodeAddress>();
                    foreach ((ClusterNodeAddress clusterNodeAddress, NodeSetScaling.NodeInfo _) in value.Nodes)
                    {
                        bool found = false;
                        foreach (ClusterConnectionManager.ClusterChangedEvent.ClusterMember clusterMember in arg.Members)
                        {
                            if (clusterMember.Address == clusterNodeAddress)
                            {
                                found = true;
                                break;
                            }
                        }

                        if(!found)
                            addressesToRemove.Add(clusterNodeAddress);
                    }

                    foreach (ClusterNodeAddress clusterNodeAddress in addressesToRemove)
                        value.Nodes.Remove(clusterNodeAddress);
                }

                // Add/update all others
                foreach (ClusterConnectionManager.ClusterChangedEvent.ClusterMember clusterMember in arg.Members)
                {
                    NodeSetConfig nodeSet = _clusterConfig.GetNodeSetConfigForAddress(clusterMember.Address);

                    if (nodeSet.ScalingMode == NodeSetScalingMode.DynamicLinear)
                    {
                        NodeSetScaling nodeSetScaling = _nodeSetScalingTable[nodeSet];

                        if (clusterMember.IsConnected)
                        {
                            if (!nodeSetScaling.Nodes.TryGetValue(clusterMember.Address, out NodeSetScaling.NodeInfo nodeInfo))
                            {
                                nodeInfo = new NodeSetScaling.NodeInfo();
                                _log.Debug($"Adding Node {clusterMember.Address} to autoscaling");
                                nodeSetScaling.Nodes.Add(clusterMember.Address, nodeInfo);
                            }
                        }

                        if (!clusterMember.IsConnected)
                        {
                            _log.Debug($"Removing Node {clusterMember.Address} from autoscaling");
                            nodeSetScaling.Nodes.Remove(clusterMember.Address);
                        }

                        nodeSetScaling.UpdateCurrentNodeCount();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error("Failed to parse cluster changed event: {ex}", ex);
            }
        }

        [CommandHandler]
        async Task HandleTick(AutoScalingTick _)
        {
            var options = RuntimeOptionsRegistry.Instance.GetCurrent<AutoScalingRuntimeOptions>();
            if (!options.Enabled)
                return;

            DateTime lastWorkTimestamp = DateTime.UtcNow;

            WorkloadTrackingSnapshot snapshot = await EntityAskAsync(WorkloadTrackingServiceActor.LeaderEntityId, new WorkloadTrackingSnapshotRequest());
            foreach ((EntityShardId shardId, WorkloadBase workload) in snapshot.Workloads)
            {
                NodeSetConfig nodeSet = _clusterConfig.GetNodeSetConfigForShardId(shardId);
                if (!_nodeSetScalingTable.TryGetValue(nodeSet, out NodeSetScaling nodeSetScaling))
                    continue;

                ClusterNodeAddress      nodeAddress = _clusterConfig.GetNodeAddressForShardId(shardId);
                NodeSetScaling.NodeInfo nodeInfo    = nodeSetScaling.Nodes[nodeAddress];

                if (!workload.IsEmpty())
                    nodeInfo.UpdateLastWorkAt(lastWorkTimestamp);
            }

            foreach ((NodeSetConfig nodeSet, NodeSetScaling nodeSetScaling) in _nodeSetScalingTable)
            {
                IAutoScalingStrategy scalingStrategy = GetStrategyForNodeSet(nodeSet, nodeSetScaling);

                List<(ClusterNodeAddress clusterNodeAddress, NodeSetScaling.NodeInfo nodeInfo, WorkloadBase workload)> nodes = nodeSetScaling.Nodes.Select(
                    x =>
                    {
                        _clusterConfig.ResolveNodeShardId(EntityKindCloudCore.LoadTracker, x.Key, out EntityShardId shardId);
                        return (x.Key, x.Value, snapshot.Workloads.GetValueOrDefault(shardId));
                    }).ToList();

                bool anyBeingKilled = nodes.Any(x => x.nodeInfo.State == NodeSetScaling.NodeState.Killing);

                // Extra safety check if a node that is shutting down, does not die within RetryKillingNodeAfterSeconds.
                // We don't have great tools to deal with this so we just tell k8s to set the replica count again...
                if (anyBeingKilled)
                {
                    foreach ((ClusterNodeAddress clusterNodeAddress, NodeSetScaling.NodeInfo nodeInfo, WorkloadBase _) in nodes.Where(x => x.nodeInfo.State == NodeSetScaling.NodeState.Killing))
                    {
                        if (nodeInfo.KillingStartedAtTime + TimeSpan.FromSeconds(RetryKillingNodeAfterSeconds) < DateTimeOffset.UtcNow)
                        {
                            _log.Warning(FormattableString.Invariant($"Node '{clusterNodeAddress}' was not killed after {RetryKillingNodeAfterSeconds} seconds, trying again..."));
                            nodeInfo.ChangeStateToKilling();

                            try
                            {
                                await _infraIntegration.SetReplicaCount(nodeSet.ShardName, nodeSetScaling.DesiredNodeCount);
                            }
                            catch (Exception ex)
                            {
                                _log.Error("Scaling down node failed: {ex}", ex);
                            }
                        }
                    }
                }

                bool shouldUpscale = scalingStrategy.ShouldScaleUp(
                    nodeSetScaling,
                    nodeSet,
                    nodes);

                List<KeyValuePair<ClusterNodeAddress, NodeSetScaling.NodeInfo>> currentlyDrainingNodes = nodeSetScaling.Nodes.Where(x => x.Value.State == NodeSetScaling.NodeState.Draining).ToList();

                if (shouldUpscale)
                {
                    // If we're scaling up, attempt to recover one draining node back to running.
                    // If there are no draining nodes, try spawn a new one.
                    if (!currentlyDrainingNodes.Any())
                    {
                        if (nodeSetScaling.DesiredNodeCount < nodeSet.DynamicLinearMaxNodeCount)
                        {
                            nodeSetScaling.IncrementDesiredNodeCount();
                        }
                    }
                    else
                    {
                        currentlyDrainingNodes.First().Value.UpdateState(NodeSetScaling.NodeState.Working);
                        nodeSetScaling.IncrementDesiredNodeCount();
                    }
                }

                if (!currentlyDrainingNodes.Any())
                {
                    // If we have more than the minimum amount of nodes, try to see if we can scale down. If so, mark one node at a time as Draining.
                    // When Draining node becomes Killable (ShouldKillNode), kill it.
                    // TODO: This should be refactored to allow draining, multiple nodes at the same time
                    // There's some concerns about how fast we should try to scale down, too aggressively and we're constantly ping-ponging, too slow and we're wasting resources
                    if (nodeSetScaling.DesiredNodeCount > nodeSet.DynamicLinearMinNodeCount)
                    {
                        if (!nodeSetScaling.IsBeingDownscaled())
                        {
                            if (scalingStrategy.ShouldScaleDown(nodeSetScaling, nodeSet, nodes))
                            {
                                nodeSetScaling.DecrementDesiredNodeCount();
                            }
                        }
                    }

                    if (nodeSetScaling.DesiredNodeCount >= nodeSet.DynamicLinearMinNodeCount)
                    {
                        if (nodeSetScaling.IsBeingDownscaled())
                        {
                            NodeSetScaling.NodeInfo nodeInfo = scalingStrategy.FindNodeToDrain(nodeSet, nodeSetScaling);
                            nodeInfo?.UpdateState(NodeSetScaling.NodeState.Draining);
                        }
                    }
                }
                else if (currentlyDrainingNodes.Count >= 1
                         && nodeSetScaling.IsBeingDownscaled())
                {
                    for (int i = currentlyDrainingNodes.Count - 1; i >= 0; i--)
                    {
                        (ClusterNodeAddress key, NodeSetScaling.NodeInfo nodeInfo) = currentlyDrainingNodes[i];

                        if (nodeInfo == null)
                            _log.Error($"NodeInfo disappeared from currentlyDrainingNodes, address: {key}");
                        _clusterConfig.ResolveNodeShardId(EntityKindCloudCore.LoadTracker, key, out EntityShardId shardId);

                        // If we can't kill the last node, we can't kill any, wait for last node to die
                        if (snapshot.Workloads.TryGetValue(shardId, out WorkloadBase workloadBase) && scalingStrategy.ShouldKillNode(nodeSetScaling, nodeSet, nodeInfo, workloadBase))
                        {
                            nodeInfo?.ChangeStateToKilling();
                            try
                            {
                                await _infraIntegration.SetReplicaCount(nodeSet.ShardName, nodeSetScaling.DesiredNodeCount);
                            }
                            catch (Exception ex)
                            {
                                _log.Error("Scaling down node failed: {ex}", ex);
                            }
                        }
                        else
                            break;
                    }
                }

                // Note that we can't scale up while a node is being killed, the instance that is being killed is likely in the process of shutting down, which is irreversible, for safety we wait until it is done.
                if (nodeSetScaling.IsBeingUpscaled()
                    && !anyBeingKilled)
                {
                    try
                    {
                        await _infraIntegration.SetReplicaCount(nodeSet.ShardName, nodeSetScaling.DesiredNodeCount);
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Scaling up node failed: {ex}", ex);
                    }
                }
            }

            PublishMessage(EntityTopic.Member, new NodeAutoScalingSnapshot(_nodeSetScalingTable));
        }

        IAutoScalingStrategy GetStrategyForNodeSet(NodeSetConfig nodeSetConfig, NodeSetScaling nodeSetScaling)
        {
            // TODO: Eventually allow overriding on a per nodeset basis...
            return new DefaultAutoScalingStrategy();
        }
    }
}
