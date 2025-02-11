// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Server.WorkloadBalancing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Metaplay.Server.AutoScaling
{
    public interface IAutoScalingStrategy
    {
        /// <summary>
        /// Determines whether a node can be killed, generally this means that the node has had no work for x time.
        /// </summary>
        public bool ShouldKillNode(NodeSetScaling nodeSetScaling, NodeSetConfig nodeSetConfig, NodeSetScaling.NodeInfo nodeInfo, WorkloadBase workload);

        /// <summary>
        /// Determines whether we should try to spin up a new node, generally this should happen before we reach max capacity.
        /// Note that the auto-scaling already considers whether we're at the max node capacity,  i.e. any checks of nodeCount &lt; maxNodeCount are therefore not necessary
        /// </summary>
        public bool ShouldScaleUp(NodeSetScaling nodeSetScaling, NodeSetConfig nodeSetConfig, IEnumerable<(ClusterNodeAddress nodeAddress, NodeSetScaling.NodeInfo nodeInfo, WorkloadBase workload)> nodes);

        /// <summary>
        /// Determines whether we should start draining a node to be shut down, generally this should happen if the total workload is less than what the current node count -1 can handle.
        /// This will mark a node to be drained, before then being considered to be shut down.
        /// Note that the auto-scaling already considers whether we're at the min node capacity, i.e. any checks of nodeCount > minNodeCount are therefore not necessary
        /// </summary>
        public bool ShouldScaleDown(NodeSetScaling nodeSetScaling, NodeSetConfig nodeSetConfig, IEnumerable<(ClusterNodeAddress nodeAddress, NodeSetScaling.NodeInfo nodeInfo, WorkloadBase workload)> nodes);

        /// <summary>
        /// Determines which node should be drained if <see cref="ShouldScaleDown"/> is true, currently this is used to select the last running node as we can only kill the last node available.
        /// </summary>
        /// <returns>If no node was found, null should be returned.</returns>
        public NodeSetScaling.NodeInfo FindNodeToDrain(NodeSetConfig nodeSetConfig, NodeSetScaling nodeSetScaling);
    }

    public class DefaultAutoScalingStrategy : IAutoScalingStrategy
    {
        public bool ShouldKillNode(NodeSetScaling nodeSetScaling, NodeSetConfig nodeSetConfig, NodeSetScaling.NodeInfo nodeInfo, WorkloadBase workload)
        {
            var options = RuntimeOptionsRegistry.Instance.GetCurrent<AutoScalingRuntimeOptions>();
            if (DateTimeOffset.UtcNow > nodeInfo.LastWorkAt + options.MinimumDrainDurationToShutdownNode)
            {
                if (workload?.IsEmpty() ?? true)
                    return true;
            }

            return false;
        }

        public bool ShouldScaleUp(NodeSetScaling nodeSetScaling, NodeSetConfig nodeSetConfig, IEnumerable<(ClusterNodeAddress nodeAddress, NodeSetScaling.NodeInfo nodeInfo, WorkloadBase workload)> nodes)
        {
            return nodes.Where(x => x.nodeInfo.State == NodeSetScaling.NodeState.Working)
                    .All(x => !x.workload?.IsWithinSoftLimit(nodeSetConfig, x.nodeAddress) ?? false) &&
                !nodeSetScaling.IsBeingUpscaled();
        }

        public bool ShouldScaleDown(NodeSetScaling nodeSetScaling, NodeSetConfig nodeSetConfig, IEnumerable<(ClusterNodeAddress nodeAddress, NodeSetScaling.NodeInfo nodeInfo, WorkloadBase workload)> nodes)
        {
            var options = RuntimeOptionsRegistry.Instance.GetCurrent<AutoScalingRuntimeOptions>();
            return nodes.Any(
                x =>
                    !x.nodeInfo.IsInGracePeriod() &&
                    x.nodeInfo.State == NodeSetScaling.NodeState.Working &&
                    x.nodeInfo.LastWorkAt != null &&
                    DateTimeOffset.UtcNow > (x.nodeInfo.LastWorkAt + options.MinimumNotEnoughWorkDurationToDrainNode) &&
                    (x.workload?.IsEmpty() ?? true));
        }

        public NodeSetScaling.NodeInfo FindNodeToDrain(NodeSetConfig nodeSetConfig, NodeSetScaling nodeSetScaling)
        {
            return nodeSetScaling.Nodes.OrderBy(x =>
            {
                if (nodeSetConfig.ResolveNodeIndex(x.Key, out int shardIndex))
                    return shardIndex;

                return 0;
            }).LastOrDefault(x => x.Value.State == NodeSetScaling.NodeState.Working).Value;
        }
    }
}
