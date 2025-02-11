// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Server.AutoScaling;
using Metaplay.Server.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server.WorkloadBalancing
{
    public class WorkloadSchedulingUtility
    {
        /// <summary>
        /// They key selector used to order the workloads, the ordered collection in iterated in descending order to find the first viable node.
        /// The default implementation can be found in <see cref="WorkloadSchedulingUtility.DefaultWorkloadScoringImplementation"/>.
        /// </summary>
        public delegate TResult CalculateWorkloadScore<out TResult>((EntityShardId shardId, EntityId entityId, WorkloadBase workload) nodeInfo, IEnumerable<KeyValuePair<EntityShardId, WorkloadBase>> workloads);

        readonly WorkloadTrackingServiceActor _parentActor;
        readonly IShardingStrategy            _shardingStrategy;

        static WorkloadSchedulingUtility _self;

        public WorkloadSchedulingUtility(WorkloadTrackingServiceActor parentActor, IShardingStrategy loadTrackerShardingStrategy)
        {
            // This is a bit dirty to prevent creating a second actor for work load scheduling purposes,
            // chances are this will change/go away in the future for a different solution
            // Sharding APIs are a bit in flux at the time of writing thus it's hard to find a neat user-friendly API
            _parentActor      = parentActor;
            _shardingStrategy = loadTrackerShardingStrategy;
            _self             = this;
        }

        /// <summary>
        /// Verifies whether a node is available to schedule work on as per the auto-scaling system. I.e. whether the node is not currently draining or shutting down.
        /// </summary>
        public static async Task<bool> CanScheduleWorkOnNode(ClusterNodeAddress nodeAddress)
        {
            NodeAutoScalingSnapshot nodeAutoScalingSnapshot = null;
            if (RuntimeOptionsRegistry.Instance.GetCurrent<AutoScalingRuntimeOptions>().Enabled)
                nodeAutoScalingSnapshot = await _self._parentActor.EntityAskAsync(NodeAutoScalingServiceActor.EntityId, new NodeAutoScalingSnapshotRequest());

            return CanScheduleWorkOnNode(nodeAddress, nodeAutoScalingSnapshot?.NodeSets);
        }

        static bool CanScheduleWorkOnNode(ClusterNodeAddress clusterNodeAddress, Dictionary<string, NodeSetScaling> scalableNodeSets)
        {
            if (scalableNodeSets != null)
            {
                foreach ((string _, NodeSetScaling value) in scalableNodeSets)
                {
                    foreach ((ClusterNodeAddress nodeAddress, NodeSetScaling.NodeInfo nodeInfo) in value.Nodes)
                    {
                        if (nodeAddress == clusterNodeAddress)
                            return nodeInfo.State == NodeSetScaling.NodeState.Working;
                    }
                }
            }

            // Not known to autoscaling, assume that it's available
            return true;
        }

        /// <summary>
        /// Returns the workload (if known) based on the nodeset and index, fetched from the leader to ensure that it is up-to-date.
        /// </summary>
        /// <param name="address">The address of the node that you're interested in</param>
        public static async Task<WorkloadBase> TryGetWorkloadOnNode(ClusterNodeAddress address)
        {
            WorkloadTrackingSnapshot snapshot = await _self._parentActor.EntityAskAsync(WorkloadTrackingServiceActor.LeaderEntityId, new WorkloadTrackingSnapshotRequest());

            MetaDictionary<EntityShardId, WorkloadBase> workloads = snapshot.Workloads;

            ClusteringOptions clusterOpts = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>();

            ClusterConfig cc = clusterOpts.ClusterConfig;

            NodeSetConfig config = cc.GetNodeSetConfigForAddress(address);
            if (!config.ResolveNodeIndex(address, out int nodeIndex) || !cc.ResolveNodeShardId(EntityKindCloudCore.LoadTracker, config, nodeIndex, out EntityShardId id))
                throw new InvalidOperationException($"Could not resolve node for {address}.");

            return TryGetWorkloadOnNode(id.NodeSetIndex, id.NodeIndex, workloads);
        }

        /// <param name="nodeSetIndex">The index of the nodeSet in the global collection of nodesets</param>
        /// <param name="nodeIndex">The index of the node inside the NodeSet</param>
        static WorkloadBase TryGetWorkloadOnNode(int nodeSetIndex, int nodeIndex, MetaDictionary<EntityShardId, WorkloadBase> workloads)
        {
            if (workloads.Count == 0)
                throw new InvalidOperationException($"No nodes are known to LoadTracker, unable to find workload.");

            EntityId      entityId = DynamicServiceShardingStrategy.CreatePlacedEntityId(EntityKindCloudCore.LoadTracker, nodeSetIndex, nodeIndex);
            EntityShardId shardId  = _self._shardingStrategy.ResolveShardId(entityId);

            return workloads[shardId];
        }

        /// <inheritdoc cref="CreateEntityIdOnBestNode(Metaplay.Core.EntityKind)"/>
        public static Task<EntityId> CreateEntityIdOnBestNode(EntityKind kind)
        {
            return CreateEntityIdOnBestNode(kind, DefaultWorkloadScoringImplementation);
        }
        /// <summary>
        /// Creates an EntityId that will run on the node with the lowest amount of "load", determine by the nodes available to <see cref="WorkloadTrackingServiceActor"/>.
        /// This does not guarantee uniqueness, you should verify that it is unique after creation (e.g. by using the return value of <see cref="DatabaseEntityUtil.CreateNewEntityAsync{TPersisted}(EntityId, TPersisted)"/>).
        /// </summary>
        public static async Task<EntityId> CreateEntityIdOnBestNode<TKey>(EntityKind kind, CalculateWorkloadScore<TKey> workloadScoreCalculator)
        {
            WorkloadTrackingSnapshot snapshot = await _self._parentActor.EntityAskAsync(WorkloadTrackingServiceActor.LeaderEntityId, new WorkloadTrackingSnapshotRequest());

            NodeAutoScalingSnapshot nodeAutoScalingSnapshot = null;
            if (RuntimeOptionsRegistry.Instance.GetCurrent<AutoScalingRuntimeOptions>().Enabled)
                nodeAutoScalingSnapshot = await _self._parentActor.EntityAskAsync(NodeAutoScalingServiceActor.EntityId, new NodeAutoScalingSnapshotRequest());

            MetaDictionary<EntityShardId, WorkloadBase> workloads = snapshot.Workloads;

            return CreateEntityIdOnBestNode(kind, workloads, nodeAutoScalingSnapshot?.NodeSets, workloadScoreCalculator);
        }

        /// <summary>
        /// Returns the first EntityShard that is within the soft limit, or if the soft limit for all nodes has been reached, the first node that is within the hard limit.
        /// Allow custom ordering to give priority to certain nodes (e.g. for prioritizing nodes in a specific region).
        /// This does not guarantee uniqueness, you should verify that it is unique after creation (e.g. by using the return value of <see cref="DatabaseEntityUtil.CreateNewEntityAsync{TPersisted}(EntityId, TPersisted)"/>).
        /// </summary>
        public static async Task<EntityShardId> FindBestNode<TKey>(EntityKind kind, CalculateWorkloadScore<TKey> calculateWorkloadOrderKeySelector)
        {
            WorkloadTrackingSnapshot snapshot = await _self._parentActor.EntityAskAsync(WorkloadTrackingServiceActor.LeaderEntityId, new WorkloadTrackingSnapshotRequest());

            NodeAutoScalingSnapshot nodeAutoScalingSnapshot = null;
            if (RuntimeOptionsRegistry.Instance.GetCurrent<AutoScalingRuntimeOptions>().Enabled)
                nodeAutoScalingSnapshot = await _self._parentActor.EntityAskAsync(NodeAutoScalingServiceActor.EntityId, new NodeAutoScalingSnapshotRequest());

            MetaDictionary<EntityShardId, WorkloadBase> workloads = snapshot.Workloads;

            return FindBestNode(kind, workloads, nodeAutoScalingSnapshot?.NodeSets, calculateWorkloadOrderKeySelector);
        }

        /// <summary>
        /// Creates an entity of <paramref name="kind"/> on the given <paramref name="entityShardId"/>, this is meant to be used in conjunction with <see cref="FindBestNode{TKey}"/>.
        /// The following sharding strategies are supported: <see cref="StaticModuloShardingStrategy"/>, <see cref="DynamicServiceShardingStrategy"/>, and <see cref="ManualShardingStrategy"/>.
        /// </summary>
        public static EntityId CreateEntityId(EntityKind kind, EntityShardId entityShardId)
        {
            VerifyShardingStrategyAndThrow(kind);

            ClusteringOptions clusterOpts = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>();

            ClusterConfig cc = clusterOpts.ClusterConfig;

            EntityConfigBase entityConfigBase = EntityConfigRegistry.Instance.GetConfig(kind);
            NodeSetConfig    nodeSetConfig    = cc.GetNodeSetConfigForShardId(entityShardId);

            if (nodeSetConfig.ScalingMode == NodeSetScalingMode.Static)
            {
                switch (entityConfigBase.ShardingStrategy)
                {
                    case StaticModuloShardingStrategy:
                        int linearShardIndex = cc.GetLinearShardIndexForNodeSetAndNode(kind, entityShardId.NodeSetIndex, entityShardId.NodeIndex);

#pragma warning disable CS0618 // Type or member is obsolete
                        ulong nodeCount = (ulong)cc.GetNodeCountForEntityKind(kind);
#pragma warning restore CS0618 // Type or member is obsolete

                        EntityId entityId = EntityId.CreateRandom(kind);

                        ulong value = entityId.Value - (entityId.Value % nodeCount);
                        value += (ulong)linearShardIndex;

                        // TODO: This is not safe due to the birthday problem, is likely to return duplicates with large enough scale
                        EntityId entityIdOnBestNode = EntityId.Create(kind, value);

                        if ((entityIdOnBestNode.Value % nodeCount) != (ulong)linearShardIndex)
                            throw new InvalidOperationException($"Expected node does not match actual, expected {linearShardIndex} but was {entityIdOnBestNode.Value % nodeCount}.");

                        return entityIdOnBestNode;
                    case ManualShardingStrategy:
                        // TODO: This is not safe due to the birthday problem, is likely to return duplicates with large enough scale
                        return ManualShardingStrategy.CreateEntityId(entityShardId, RandomPCG.CreateNew().NextULong() % ManualShardingStrategy.MaxValue);
                    case DynamicServiceShardingStrategy:
                        return DynamicServiceShardingStrategy.CreatePlacedEntityId(entityShardId);
                    default:
                        throw new ArgumentOutOfRangeException(message: $"{entityConfigBase.ShardingStrategy} is not supported", innerException: null);
                }
            }

            if (nodeSetConfig.ScalingMode == NodeSetScalingMode.DynamicLinear)
            {
                switch (entityConfigBase.ShardingStrategy)
                {
                    case DynamicServiceShardingStrategy:
                        return DynamicServiceShardingStrategy.CreatePlacedEntityId(entityShardId);
                    case ManualShardingStrategy:
                        // TODO: This is not safe due to the birthday problem, is likely to return duplicates with large enough scale
                        return ManualShardingStrategy.CreateEntityId(entityShardId, RandomPCG.CreateNew().NextULong() % ManualShardingStrategy.MaxValue);
                    default:
                        throw new ArgumentOutOfRangeException(message: $"{entityConfigBase.ShardingStrategy} is not supported", innerException: null);
                }
            }
            throw new InvalidOperationException("Unsupported scaling mode or sharding strategy.");
        }

        /// <summary>
        /// The default parameter passed to workloadScoreCalculator of <see cref="CreateEntityIdOnBestNode(Metaplay.Core.EntityKind)"/>
        /// </summary>
        public static int DefaultWorkloadScoringImplementation((EntityShardId shardId, EntityId entityId, WorkloadBase workload) nodeInfo, IEnumerable<KeyValuePair<EntityShardId, WorkloadBase>> workloads)
        {
            // De-prioritize the last node to help it drain when necessary
            return (nodeInfo.shardId.NodeIndex < workloads.Count() - 1) ? 2 : 1;
        }

        static EntityShardId FindBestNode<TKey>(
            EntityKind kind,
            MetaDictionary<EntityShardId, WorkloadBase> workloads,
            Dictionary<string, NodeSetScaling> scalableNodeSets,
            CalculateWorkloadScore<TKey> workloadScoreCalculator)
        {
            if (workloads.Count == 0)
                throw new InvalidOperationException($"No nodes are known to load tracker, unable to create EntityIds.");

            ClusteringOptions clusterOpts = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>();
            ClusterConfig     cc          = clusterOpts.ClusterConfig;

            var results = workloads.Select(x=> (shardId: x.Key, entityId: DynamicServiceShardingStrategy.CreatePlacedEntityId(EntityKindCloudCore.LoadTracker, x.Key.NodeSetIndex, x.Key.NodeIndex), workload: x.Value)).ToList();

            var orderedNodes = results.OrderByDescending(x => workloadScoreCalculator(x, workloads.Where(y=> y.Key.NodeSetIndex == x.shardId.NodeSetIndex))).ToList();

            // Find best in soft limit,
            EntityShardId entityShardId = FindBestEntityShard(kind, orderedNodes, scalableNodeSets, cc, checkHardLimit: false);

            //  if none... Try again in hard limit
            if (!entityShardId.IsValid)
                entityShardId = FindBestEntityShard(kind, orderedNodes, scalableNodeSets, cc, checkHardLimit: true);

            // If this still none... throw as no valid node is available
            if (!entityShardId.IsValid)
                throw new InvalidOperationException($"No available nodes found to schedule work on for EntityKind '{kind}', either all nodes have reached the hard limit or no nodes available for this EntityKind.");

            VerifyShardingStrategyAndThrow(kind);

            return entityShardId;
        }

        static void VerifyShardingStrategyAndThrow(EntityKind kind)
        {
            //TODO: also check static/dynamic scaling type here
            EntityConfigBase entityConfigBase = EntityConfigRegistry.Instance.GetConfig(kind);
            if (entityConfigBase.ShardingStrategy is not StaticModuloShardingStrategy &&
                entityConfigBase.ShardingStrategy is not StaticServiceShardingStrategy &&
                entityConfigBase.ShardingStrategy is not DynamicServiceShardingStrategy &&
                entityConfigBase.ShardingStrategy is not ManualShardingStrategy)
                throw new InvalidOperationException($"Workload balancing is currently only available for EntityKinds using the {nameof(StaticModuloShardingStrategy)}, {nameof(StaticServiceShardingStrategy)}, {nameof(DynamicServiceShardingStrategy)}, or {nameof(ManualShardingStrategy)}. '{kind}' is using the {entityConfigBase.ShardingStrategy.GetType().Name} instead.");
        }

        static EntityId CreateEntityIdOnBestNode<TKey>(EntityKind kind, MetaDictionary<EntityShardId, WorkloadBase> workloads, Dictionary<string, NodeSetScaling> nodeSets, CalculateWorkloadScore<TKey> workloadScoreCalculator)
        {
            EntityShardId entityShardId      = FindBestNode(kind, workloads, nodeSets, workloadScoreCalculator);
            EntityId      entityIdOnBestNode = CreateEntityId(kind, entityShardId);

            return entityIdOnBestNode;
        }

        static EntityShardId FindBestEntityShard(
            EntityKind kind,
            List<(EntityShardId shardId, EntityId entityId, WorkloadBase workload)> orderedWorkloads,
            Dictionary<string, NodeSetScaling> scalableNodeSets,
            ClusterConfig cc,
            bool checkHardLimit)
        {
            foreach ((EntityShardId shardId, EntityId entityId, WorkloadBase workload) in orderedWorkloads)
            {
                NodeSetConfig      nodeSet     = cc.GetNodeSetConfigForShardId(shardId);
                ClusterNodeAddress nodeAddress = nodeSet.ResolveNodeAddress(shardId.NodeIndex);

                if (!CanScheduleWorkOnNode(nodeAddress, scalableNodeSets))
                    continue;

                if (checkHardLimit)
                {
                    if (!workload.IsWithinHardLimit(nodeSet, nodeAddress))
                        continue;
                }
                else
                {
                    if (!workload.IsWithinSoftLimit(nodeSet, nodeAddress))
                        continue;
                }

                if (cc.ResolveNodeShardId(kind, nodeAddress, out var specializedShardId))
                    return specializedShardId;
            }

            return new EntityShardId();
        }
    }
}
