// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Options;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Json;
using Metaplay.Server.AutoScaling;
using Metaplay.Server.WorkloadBalancing;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using static System.FormattableString;

namespace Metaplay.Server.AdminApi.Controllers
{
    public class ClusterController : GameAdminApiController
    {
        public class GetClusterResponse
        {
            public required NodeSetInfo[] NodeSets    { get; init; }
        }

        [HttpGet("cluster/status")]
        [RequirePermission(MetaplayPermissions.ApiSystemViewClusterNodes)]
        public async Task<ActionResult<GetClusterResponse>> GetClusterStatus()
        {
            WorkloadTrackingSnapshot                            workloadTrackingSnapshot = await EntityAskAsync(WorkloadTrackingServiceActor.LeaderEntityId, new WorkloadTrackingSnapshotRequest());
            NodeAutoScalingSnapshot                             autoScalingSnapshot      = await EntityAskAsync(NodeAutoScalingServiceActor.EntityId, new NodeAutoScalingSnapshotRequest());
            ClusterConnectionManager.ClusterDebugStatusResponse clusterStatus            = await ClusterConnectionManager.GetClusterStatusAsync();
            StatsCollectorLiveEntityCountPerNodeResponse        entityCounts             = await EntityAskAsync(StatsCollectorManager.EntityId, new StatsCollectorLiveEntityCountPerNodeRequest());

            ClusterConfig clusterConfig = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>().ClusterConfig;

            List<NodeSetInfo> nodeSetInfos = new List<NodeSetInfo>();

            foreach (NodeSetConfig nodeSet in clusterConfig.EnumerateNodeSets())
            {
                autoScalingSnapshot.NodeSets.TryGetValue(nodeSet.ShardName, out var nodeSetScaling);

                NodeSetInfo nodeSetInfo = new NodeSetInfo();
                nodeSetInfos.Add(nodeSetInfo);

                // If not part of scaling, fallback to maxNodeCount
                nodeSetInfo.DesiredNodeCount = nodeSetScaling?.DesiredNodeCount ?? nodeSet.GetMaxNodeCount();
                nodeSetInfo.NodeCount        = nodeSetScaling?.CurrentNodeCount ?? nodeSet.GetMaxNodeCount();

                nodeSetInfo.MinNodeCount = nodeSet.ScalingMode == NodeSetScalingMode.DynamicLinear ? nodeSet.DynamicLinearMinNodeCount : nodeSet.GetMaxNodeCount();
                nodeSetInfo.MaxNodeCount = nodeSet.GetMaxNodeCount();
                nodeSetInfo.ScalingMode  = nodeSet.ScalingMode;

                nodeSetInfo.Name     = nodeSet.ShardName;
                nodeSetInfo.HostName = nodeSet.HostName;

                nodeSetInfo.GlobalDnsSuffix = nodeSet.GlobalDnsSuffix;

                nodeSetInfo.RemotingPort = nodeSet.RemotingPort;

                if (nodeSet.ScalingMode == NodeSetScalingMode.DynamicLinear)
                {
                    if (nodeSetInfo.DesiredNodeCount < nodeSetInfo.NodeCount)
                        nodeSetInfo.ScalingState = ScalingState.ScalingDown;
                    else if (nodeSetInfo.DesiredNodeCount > nodeSetInfo.NodeCount)
                        nodeSetInfo.ScalingState = ScalingState.ScalingUp;
                    else if (nodeSetInfo.NodeCount == nodeSet.GetMaxNodeCount())
                        nodeSetInfo.ScalingState = ScalingState.AtMaxNodeCount;
                }

                nodeSetInfo.EntityKinds                = nodeSet.EntityKindMask.GetKinds().Select(x => x.Name).ToArray();
                nodeSetInfo.AggregatedLiveEntityCounts = new Dictionary<EntityKind, int>();

                for (int j = 0; j < nodeSet.GetMaxNodeCount(); j++)
                {
                    _ = clusterConfig.ResolveNodeShardId(EntityKindCloudCore.LoadTracker, nodeSet, nodeIndex: j, out EntityShardId loadTrackerId);

                    ClusterNodeAddress nodeAddress  = nodeSet.ResolveNodeAddress(j);
                    workloadTrackingSnapshot.Workloads.TryGetValue(new DynamicServiceShardingStrategy().ResolveShardId(DynamicServiceShardingStrategy.CreatePlacedEntityId(loadTrackerId)), out WorkloadBase workloadBase);
                    NodeSetScaling.NodeInfo nodeInfo = null;
                    nodeSetScaling?.Nodes.TryGetValue(nodeAddress, out nodeInfo);
                    ClusterConnectionManager.ClusterChangedEvent.ClusterMember member = clusterStatus.CurrentCluster.Members.FirstOrDefault(x => x.Address == nodeAddress);

                    NodeInfo info = new NodeInfo();
                    nodeSetInfo.Nodes.Add(info);

                    info.Name        = Invariant($"{nodeSet.ShardName}-{j}");
                    info.NodeAddress = nodeAddress.ToString();

                    RawNodeData rawNodeData = new RawNodeData();
                    rawNodeData.LastWorkAt  = nodeInfo?.LastWorkAt;

                    info.NodeStatus = NodeStatus.Perfect;

                    rawNodeData.ClusterLocalPhase = member.Address != null ? member.Info.LocalPhase : null;
                    rawNodeData.IsConnected       = member.IsConnected;

                    rawNodeData.LiveEntityCounts = entityCounts.LiveEntityCounts.GetValueOrDefault(nodeAddress);
                    if (rawNodeData.LiveEntityCounts != null)
                    {
                        foreach ((EntityKind key, int value) in rawNodeData.LiveEntityCounts)
                        {
                            if (!nodeSetInfo.AggregatedLiveEntityCounts.ContainsKey(key))
                                nodeSetInfo.AggregatedLiveEntityCounts[key] = 0;

                            nodeSetInfo.AggregatedLiveEntityCounts[key] += value;
                        }
                    }

                    Dictionary<EntityShardGroup, EntityGroupPhase> entityGroupPhases = member.Info.EntityGroupPhases?
                        .Select((phase, index) => new KeyValuePair<EntityShardGroup, EntityGroupPhase>((EntityShardGroup)index, phase))
                        .ToDictionary(
                            x => x.Key,
                            x => x.Value);

                    rawNodeData.EntityGroupPhases = entityGroupPhases;

                    info.IsWithinHardLimit = workloadBase?.IsWithinHardLimit(nodeSet, nodeAddress) ?? true;
                    info.IsWithinSoftLimit = workloadBase?.IsWithinHardLimit(nodeSet, nodeAddress) ?? true;

                    if (!info.IsWithinHardLimit)
                        info.NodeStatus = NodeStatus.HardLimit;
                    else if (!info.IsWithinSoftLimit)
                        info.NodeStatus = NodeStatus.SoftLimit;

                    if (member.Address != null)
                    {
                        if (!member.IsConnected)
                            info.NodeStatus = NodeStatus.NotConnected;
                    }
                    else
                        info.NodeStatus = NodeStatus.ExpectedNotConnected;

                    rawNodeData.RawWorkload = workloadBase;

                    if (nodeInfo != null)
                    {
                        rawNodeData.ScalingNodeState = nodeInfo.State;
                        switch (rawNodeData.ScalingNodeState)
                        {
                            case NodeSetScaling.NodeState.Dead:
                                info.NodeScalingState = NodeScalingState.UnknownState;
                                break;
                            case NodeSetScaling.NodeState.Working:
                                info.NodeScalingState = NodeScalingState.Idle;
                                break;
                            case NodeSetScaling.NodeState.Draining:
                                info.NodeScalingState = NodeScalingState.Draining;
                                break;
                            case NodeSetScaling.NodeState.Killing:
                                info.NodeScalingState = NodeScalingState.Killing;
                                break;
                        }
                    }
                    else
                    {
                        info.NodeScalingState = NodeScalingState.UnknownState;
                    }

                    info.PublicIp        = member.Info.PublicIpV4Address;
                    info.ServerStartedAt = member.Info.ProcessStartedAt;
                    info.RawNodeData     = rawNodeData;

                    info.GrafanaLogUrl = GetGrafanaUrlForNode(clusterConfig, nodeAddress);
                }

                // Always take the most severe status
                nodeSetInfo.MostSevereStatus          = nodeSetInfo.Nodes.Min(x => x.NodeStatus);

                if (nodeSetInfo.MostSevereStatus == NodeStatus.ExpectedNotConnected)
                    nodeSetInfo.MostSevereStatus = NodeStatus.Perfect;

                nodeSetInfo.ConnectedNodes             = nodeSetInfo.Nodes.Count(x => x.RawNodeData.IsConnected);
                nodeSetInfo.NodeCountOutsideHardLimit  = nodeSetInfo.Nodes.Count(x => !x.IsWithinHardLimit);
                nodeSetInfo.NodeCountOutsideSoftLimit  = nodeSetInfo.Nodes.Count(x => !x.IsWithinSoftLimit);
            }

            return new GetClusterResponse
            {
                NodeSets = nodeSetInfos.ToArray(),
            };
        }

        static string GetGrafanaUrlForNode(ClusterConfig clusterConfig, ClusterNodeAddress address)
        {
            DeploymentOptions deployment = RuntimeOptionsRegistry.Instance.GetCurrent<DeploymentOptions>();
            if (deployment.GrafanaUri == null)
                return null;

            string podName = address.HostName.Split('.')[0];
            string datasource;
            string exprPrefix;

            // For edge regions, the datasource is region specific.
            NodeSetConfig nodeSetConfig     = clusterConfig.GetNodeSetConfigForAddress(address);
            NodeSetConfig mainRegionConfig  = clusterConfig.GetNodeSetConfigForShardId(new EntityShardId(EntityKindCloudCore.GlobalStateManager, nodeSetIndex: 0, nodeIndex: 0));
            if (nodeSetConfig.GlobalDnsSuffix == mainRegionConfig.GlobalDnsSuffix)
            {
                // Main region
                datasource = "Loki";
                exprPrefix = "";
            }
            else
            {
                // Edge region
                // \todo: figure out datasource for edge clusters. We just encode a warning in query.
                datasource = "Loki";
                exprPrefix = "!!SET THE DATA SOURCE TO THE CORRECT REGION (hint: top left)!!";
            }

            JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.None,
            };
            string query = JsonSerialization.SerializeToString(new
                {
                    datasource,
                    queries = new object[]
                    {
                        new
                        {
                            expr =
                                exprPrefix +
                                """{app="metaplay-server",namespace="NAMESPACE",pod="PODNAME"}"""
                                .Replace("NAMESPACE", deployment.KubernetesNamespace)
                                .Replace("PODNAME", podName),
                        }
                    },
                    range = new
                    {
                        from = "now-1h",
                        to = "now"
                    }
                },
                JsonSerializer.Create(jsonSettings));

            return $"{deployment.GrafanaUri}explore?orgId=1&left={HttpUtility.UrlEncode(query)}";
        }

        public class RawNodeData
        {
            // Autoscaling
            public NodeSetScaling.NodeState?                      ScalingNodeState { get; set; }
            public DateTimeOffset?                                LastWorkAt       { get; set; }

            // ClusterConnectionManger
            public ClusterPhase?                                  ClusterLocalPhase { get; set; }
            public bool                                           IsConnected       { get; set; }
            public Dictionary<EntityShardGroup, EntityGroupPhase> EntityGroupPhases { get; set; }

            // Workload tracking
            public WorkloadBase                                   RawWorkload { get; set; }

            // Stats collector
            public MetaDictionary<EntityKind, int>             LiveEntityCounts { get; set; }
        }

        public class NodeInfo
        {
            public string           Name { get; set; }

            /// <summary>
            /// The current status of the nodes, based on data gathered from multiple systems
            /// </summary>
            public NodeStatus       NodeStatus       { get; set; }
            public NodeScalingState NodeScalingState { get; set; }
            public string           NodeAddress      { get; set; }

            public bool             IsWithinHardLimit { get; set; }
            public bool             IsWithinSoftLimit { get; set; }

            public string           PublicIp { get; set; }

            public MetaTime         ServerStartedAt { get; set; }

            /// <summary>
            /// This is internal data gathered from different systems, and thus it might have differing opinions on what the node is currently doing
            /// </summary>
            public RawNodeData      RawNodeData { get; set; }

            /// <summary>
            /// Url to the grafana log view filtering only logs from this node. Null if not available
            /// </summary>
            public string           GrafanaLogUrl { get; set; }
        }

        public enum NodeScalingState
        {
            /// <summary>
            /// We don't know the state, most likely because it's not connected, or it's not a dynamic node
            /// </summary>
            UnknownState,
            /// <summary>
            /// Everything is fine and the node is likely doing work
            /// </summary>
            Idle,
            /// <summary>
            /// The node is being drained as preparation for shutting down, no new work should be scheduled
            /// </summary>
            Draining,
            /// <summary>
            /// The node is being killed, this should end quite quickly.
            /// </summary>
            Killing,
        }

        public class NodeSetInfo
        {
            public List<NodeInfo>              Nodes            { get; private set; } = new List<NodeInfo>();
            public int                         DesiredNodeCount { get; set; }
            public int                         NodeCount        { get; set; }

            public string[]                    EntityKinds { get; set; }

            public NodeStatus                  MostSevereStatus { get; set; }

            public ScalingState                ScalingState              { get; set; }

            public int                         MinNodeCount { get; set; }
            public int                         MaxNodeCount { get; set; }

            public NodeSetScalingMode          ScalingMode                { get; set; }
            public string                      Name                       { get; set; }
            public int                         RemotingPort               { get; set; }
            public string                      HostName                   { get; set; }
            public string                      GlobalDnsSuffix            { get; set; }
            public int                         ConnectedNodes             { get; set; }
            public int                         NodeCountOutsideHardLimit  { get; set; }
            public int                         NodeCountOutsideSoftLimit  { get; set; }
            public Dictionary<EntityKind,int> AggregatedLiveEntityCounts { get; set; }
        }

        public enum ScalingState
        {
            Idle,
            ScalingUp,
            ScalingDown,
            AtMaxNodeCount
        }

        public enum NodeStatus
        {
            /// <summary>
            /// A node is disconnected that we expect to be online, this is very bad
            /// </summary>
            NotConnected         = 1,
            /// <summary>
            /// 1 or more nodes are at the hard limit, nodes at hard limit are likely near the limit of their capability and might have performance issues
            /// </summary>
            HardLimit            = 5,
            /// <summary>
            /// 1 or more nodes are at the soft limit, this is considered the capability limit in normal conditions, we try to not schedule work on this node anymore by default
            /// </summary>
            SoftLimit            = 10,
            /// <summary>
            /// A node is offline, and we do not expect it to be online currently.
            /// </summary>
            ExpectedNotConnected = 20,
            /// <summary>
            /// All good
            /// </summary>
            Perfect              = 30,
        }
    }
}
