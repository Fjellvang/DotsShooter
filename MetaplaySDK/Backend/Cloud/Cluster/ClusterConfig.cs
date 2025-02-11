// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using static System.FormattableString;

namespace Metaplay.Cloud.Cluster
{
    public enum ClusteringMode
    {
        /// <summary>
        /// Clustering is not in use.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Static clustering with shard names/addresses of the form: '{hostName}:{port+shardNdx}', eg, '127.0.0.1:6000'
        /// </summary>
        Static,

        /// <summary>
        /// Kubernetes clustering with shard names of the form: '{shardType}-{shardNdx}.{shardType}:{port}', eg, 'logic-0.logic:6000'
        /// </summary>
        Kubernetes,
    }

    public enum NodeSetScalingMode
    {
        /// <summary>
        /// NodeSet does not scale. It has a fixed number of node, indexed from 0 to N.
        /// </summary>
        Static = 0,

        /// <summary>
        /// NodeSet scales by increasing/decreasing replica count. Nodes are indexed from 0 to N.
        /// </summary>
        DynamicLinear,
    }

    public class NodeSetConfig
    {
        public readonly ClusteringMode      Mode;

        /// <summary>
        /// The name of this Node Set as defined in the deployment Helm values yaml file.
        /// </summary>
        public readonly string              ShardName;
        public readonly string              HostName;
        public readonly int                 RemotingPort;

        /// <inheritdoc cref="InfrastructureDefinition.NodeSetDefinition.GlobalDnsSuffix"/>
        public readonly string              GlobalDnsSuffix;
        public readonly EntityKindMask      EntityKindMask;
        public readonly NodeSetScalingMode  ScalingMode;

        [JsonProperty("staticNodeCount", NullValueHandling = NullValueHandling.Ignore)]
        readonly int?                       _staticNodeCount;

        [JsonProperty("dynamicLinearMinNodeCount", NullValueHandling = NullValueHandling.Ignore)]
        readonly int?                       _dynamicLinearMinNodeCount;

        [JsonProperty("dynamicLinearMaxNodeCount", NullValueHandling = NullValueHandling.Ignore)]
        readonly int?                       _dynamicLinearMaxNodeCount;

        /// <summary>
        /// The number of nodes in the node set. Only valid when <see cref="ScalingMode"/> is <see cref="NodeSetScalingMode.Static"/>.
        /// </summary>
        [JsonIgnore]
        public int StaticNodeCount
        {
            get
            {
                if (_staticNodeCount is not int staticNodeCount)
                    throw new InvalidOperationException($"Cannot access {nameof(StaticNodeCount)}, the ScalingMode is {ScalingMode}");
                return staticNodeCount;
            }
        }

        /// <summary>
        /// The minimum number of nodes in the nodeset. Only valid when <see cref="ScalingMode"/> is <see cref="NodeSetScalingMode.DynamicLinear"/>.
        /// </summary>
        [JsonIgnore]
        public int DynamicLinearMinNodeCount
        {
            get
            {
                if (_dynamicLinearMinNodeCount is not int dynamicLinearMinNodeCount)
                    throw new InvalidOperationException($"Cannot access {nameof(DynamicLinearMinNodeCount)}, the ScalingMode is {ScalingMode}");
                return dynamicLinearMinNodeCount;
            }
        }

        /// <summary>
        /// The maximum number of nodes in the nodeset. Only valid when <see cref="ScalingMode"/> is <see cref="NodeSetScalingMode.DynamicLinear"/>.
        /// </summary>
        [JsonIgnore]
        public int DynamicLinearMaxNodeCount
        {
            get
            {
                if (_dynamicLinearMaxNodeCount is not int dynamicLinearMaxNodeCount)
                    throw new InvalidOperationException($"Cannot access {nameof(DynamicLinearMaxNodeCount)}, the ScalingMode is {ScalingMode}");
                return dynamicLinearMaxNodeCount;
            }
        }

        /// <summary>
        /// Returns the maximum node count of a nodeset. For static nodesets, this is the fixed size. For dynamic nodesets, this is the maximum size.
        /// <para>
        /// <b>Warning:</b> This does NOT return the current number of nodes. To inspect the currently present nodes, you should subscribe to cluster
        /// change events.
        /// </para>
        /// </summary>
        public int GetMaxNodeCount()
        {
            if (ScalingMode == NodeSetScalingMode.Static)
                return _staticNodeCount.Value;
            else if (ScalingMode == NodeSetScalingMode.DynamicLinear)
                return _dynamicLinearMaxNodeCount.Value;
            throw new InvalidOperationException($"Invalid ScalingMode {ScalingMode}");
        }

        NodeSetConfig(ClusteringMode mode, string shardName, string hostName, int remotingPort, string globalDnsSuffix, EntityKindMask entityKindMask, NodeSetScalingMode scalingMode, int? staticNodeCount, int? dynamicLinearMinNodeCount, int? dynamicLinearMaxNodeCount)
        {
            Mode                        = mode;
            ShardName                   = shardName;
            HostName                    = hostName;
            RemotingPort                = remotingPort;
            GlobalDnsSuffix             = globalDnsSuffix;
            EntityKindMask              = entityKindMask;
            ScalingMode                 = scalingMode;
            _staticNodeCount            = staticNodeCount;
            _dynamicLinearMinNodeCount  = dynamicLinearMinNodeCount;
            _dynamicLinearMaxNodeCount  = dynamicLinearMaxNodeCount;
        }

        public static NodeSetConfig CreateStaticNodeSet(ClusteringMode mode, string shardName, string hostName, int remotingPort, string globalDnsSuffix, EntityKindMask entityKindMask, int nodeCount)
        {
            return new NodeSetConfig(
                mode,
                shardName,
                hostName,
                remotingPort,
                globalDnsSuffix,
                entityKindMask,
                scalingMode: NodeSetScalingMode.Static,
                staticNodeCount: nodeCount,
                dynamicLinearMinNodeCount: null,
                dynamicLinearMaxNodeCount: null);
        }

        public static NodeSetConfig CreateDynamicLinearNodeSet(ClusteringMode mode, string shardName, string hostName, int remotingPort, string globalDnsSuffix, EntityKindMask entityKindMask, int minNodeCount, int maxNodeCount)
        {
            return new NodeSetConfig(
                mode,
                shardName,
                hostName,
                remotingPort,
                globalDnsSuffix,
                entityKindMask,
                scalingMode: NodeSetScalingMode.DynamicLinear,
                staticNodeCount: null,
                dynamicLinearMinNodeCount: minNodeCount,
                dynamicLinearMaxNodeCount: maxNodeCount);
        }

        public ClusterNodeAddress ResolveNodeAddress(int nodeIndex)
        {
            switch (Mode)
            {
                case ClusteringMode.Disabled:
                    return new ClusterNodeAddress(HostName + GlobalDnsSuffix, RemotingPort);

                case ClusteringMode.Static:
                    return new ClusterNodeAddress(HostName + GlobalDnsSuffix, RemotingPort + nodeIndex);

                case ClusteringMode.Kubernetes:
                    return new ClusterNodeAddress(Invariant($"{HostName}-{nodeIndex}.{HostName}{GlobalDnsSuffix}"), RemotingPort);

                default:
                    throw new InvalidOperationException($"Unknown ClusteringMode: {Mode}");
            }
        }

        public bool IsAddressShardOwner(ClusterNodeAddress address)
        {
            return ResolveNodeIndex(address, out int _);
        }

        public bool ResolveNodeIndex(ClusterNodeAddress address, out int nodeIndex)
        {
            if (address is null)
                throw new ArgumentNullException(nameof(address));

            switch (Mode)
            {
                case ClusteringMode.Disabled:
                    // In Disabled mode, there is only a singleton
                    if (address.HostName != $"{HostName}{GlobalDnsSuffix}")
                        break;
                    if (address.Port != RemotingPort)
                        break;
                    nodeIndex = 0;
                    return true;

                case ClusteringMode.Static:
                    // In Static mode, shard index is chosen by remoting port
                    if (address.HostName != $"{HostName}{GlobalDnsSuffix}")
                        break;

                    bool isPortInValidRange;
                    switch (ScalingMode)
                    {
                        case NodeSetScalingMode.Static:
                        {
                            isPortInValidRange = (RemotingPort <= address.Port && address.Port < RemotingPort + StaticNodeCount);
                            break;
                        }
                        case NodeSetScalingMode.DynamicLinear:
                        {
                            isPortInValidRange = (RemotingPort <= address.Port && address.Port < RemotingPort + DynamicLinearMaxNodeCount);
                            break;
                        }
                        default:
                            throw new ArgumentException($"Unknown scaling mode: {ScalingMode}");
                    }
                    if (!isPortInValidRange)
                        break;

                    nodeIndex = address.Port - RemotingPort;
                    return true;

                case ClusteringMode.Kubernetes:
                    string prefix = $"{HostName}-";
                    if (!address.HostName.StartsWith(prefix, StringComparison.Ordinal))
                        break;
                    if (!address.HostName.EndsWith(GlobalDnsSuffix, StringComparison.Ordinal))
                        break;
                    if (address.Port != RemotingPort)
                        break;

                    // Parse shard index from self hostname: 'logic-3.logic' gives 3
                    int parsedShardIndex = int.Parse(address.HostName.Substring(prefix.Length).Split('.')[0], CultureInfo.InvariantCulture);
                    bool isShardIndexInRange;
                    switch (ScalingMode)
                    {
                        case NodeSetScalingMode.Static:
                        {
                            isShardIndexInRange = (parsedShardIndex < StaticNodeCount);
                            break;
                        }
                        case NodeSetScalingMode.DynamicLinear:
                        {
                            isShardIndexInRange = (parsedShardIndex < DynamicLinearMaxNodeCount);
                            break;
                        }
                        default:
                            throw new ArgumentException($"Unknown scaling mode: {ScalingMode}");
                    }
                    if (!isShardIndexInRange)
                        break;

                    nodeIndex = parsedShardIndex;
                    return true;

                default:
                    throw new InvalidOperationException($"Unknown ClusteringMode: {Mode}");
            }

            nodeIndex = -1;
            return false;
        }
    }

    public class ClusterConfig
    {
        readonly ClusteringMode      Mode;
        readonly List<NodeSetConfig> NodeSets;

        static ClusterConfig()
        {
            // Custom printer for clarity
            PrettyPrinter.RegisterFormatter<ClusterConfig>((ClusterConfig cc, bool verbose) =>
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("ClusterConfig {");
                sb.AppendLine($"  Mode = {cc.Mode}");
                foreach (NodeSetConfig nodeset in cc.NodeSets)
                {
                    sb.AppendLine($"  {nodeset.ShardName} {{");
                    sb.AppendLine($"    HostName = {nodeset.HostName}");
                    sb.AppendLine(Invariant($"    RemotingPort = {nodeset.RemotingPort}"));
                    sb.AppendLine($"    GlobalDnsSuffix = {nodeset.GlobalDnsSuffix}");
                    sb.AppendLine($"    EntityKindMask = {nodeset.EntityKindMask}");
                    sb.AppendLine($"    ScalingMode = {nodeset.ScalingMode}");
                    if (nodeset.ScalingMode == NodeSetScalingMode.Static)
                    {
                        sb.AppendLine(Invariant($"    StaticNodeCount = {nodeset.StaticNodeCount}"));
                    }
                    else if (nodeset.ScalingMode == NodeSetScalingMode.DynamicLinear)
                    {
                        sb.AppendLine(Invariant($"    DynamicLinearMinNodeCount = {nodeset.DynamicLinearMinNodeCount}"));
                        sb.AppendLine(Invariant($"    DynamicLinearMaxNodeCount = {nodeset.DynamicLinearMaxNodeCount}"));
                    }
                    sb.AppendLine("  }");
                }
                sb.AppendLine("}");

                return sb.ToString();
            });
        }

        /// <summary>
        /// Enumerate NodeSets of the cluster in arbitrary order.
        /// </summary>
        public IEnumerable<NodeSetConfig> EnumerateNodeSets() => NodeSets;

        public ClusterConfig(ClusteringMode mode, List<NodeSetConfig> nodeSets)
        {
            Mode = mode;
            NodeSets = nodeSets;
        }

        public NodeSetConfig GetNodeSetConfigForShardId(EntityShardId shardId)
        {
            NodeSetConfig[] nodeSets = GetNodeSetsForEntityKind(shardId.Kind);
            if (shardId.NodeSetIndex >= nodeSets.Length)
                throw new InvalidOperationException($"NodeSetConfig not found for {shardId}");

            return nodeSets[shardId.NodeSetIndex];
        }

        public ClusterNodeAddress GetNodeAddressForShardId(EntityShardId shardId)
        {
            if (TryGetNodeAddressForShardId(shardId, out ClusterNodeAddress address))
                return address;
            throw new InvalidOperationException($"NodeAddress not found for {shardId}");
        }

        public bool TryGetNodeAddressForShardId(EntityShardId shardId, out ClusterNodeAddress address)
        {
            int nodeSetsRemaining = shardId.NodeSetIndex;
            foreach (NodeSetConfig nodeSet in NodeSets)
            {
                if (!nodeSet.EntityKindMask.IsSet(shardId.Kind))
                    continue;

                if (nodeSetsRemaining != 0)
                {
                    nodeSetsRemaining--;
                    continue;
                }

                int maxNodeCount = nodeSet.ScalingMode switch
                {
                    NodeSetScalingMode.Static => nodeSet.StaticNodeCount,
                    NodeSetScalingMode.DynamicLinear => nodeSet.DynamicLinearMaxNodeCount,
                    _ => throw new InvalidOperationException($"Invalid ScalingMode: {nodeSet.ScalingMode}")
                };

                if (shardId.NodeIndex < 0 || shardId.NodeIndex >= maxNodeCount)
                    break;

                address = nodeSet.ResolveNodeAddress(shardId.NodeIndex);
                return true;
            }

            address = null;
            return false;
        }

        public NodeSetConfig GetNodeSetConfigForAddress(ClusterNodeAddress address)
        {
            foreach (NodeSetConfig nodeSet in NodeSets)
            {
                if (nodeSet.IsAddressShardOwner(address))
                    return nodeSet;
            }

            throw new InvalidOperationException($"NodeSetConfig not found for {address}");
        }

        public NodeSetConfig GetNodeSetConfigByName(string nodeSetName)
        {
            foreach (NodeSetConfig nodeSet in NodeSets)
            {
                if (nodeSet.ShardName == nodeSetName)
                    return nodeSet;
            }

            throw new InvalidOperationException($"NodeSetConfig not found for {nodeSetName}");
        }

        public NodeSetConfig[] GetNodeSetsForEntityKind(EntityKind entityKind)
        {
            return NodeSets
                .Where(nodeSet => nodeSet.EntityKindMask.IsSet(entityKind))
                .ToArray();
        }

        /// <summary>
        /// Count the number of nodes in the cluster which have the specified <see cref="EntityKind"/>
        /// placed on them.
        /// </summary>
        [Obsolete("No statically known node count")]
        public int GetNodeCountForEntityKind(EntityKind entityKind)
        {
            int numNodesTotal = 0;
            foreach (NodeSetConfig nodeSet in NodeSets)
            {
                if (nodeSet.EntityKindMask.IsSet(entityKind))
                    numNodesTotal += nodeSet.GetMaxNodeCount();
            }
            return numNodesTotal;
        }

        /// <summary>
        /// Return the index of the NodeSet and Node for a given linear shard address. Only allowed for entities with on NodeSets with ScalingMode=Static.
        /// </summary>
        /// <param name="entityKind">The entity kind to lookup</param>
        /// <param name="shardIndex">The shard index in the linear address space</param>
        public void GetNodeSetAndNodeForLinearShardIndex(EntityKind entityKind, int shardIndex, out int nodeSetIndex, out int nodeIndex)
        {
            nodeSetIndex = 0;
            nodeIndex    = 0;
            foreach (NodeSetConfig nodeSet in NodeSets)
            {
                if (!nodeSet.EntityKindMask.IsSet(entityKind))
                    continue;

                if (nodeSet.ScalingMode != NodeSetScalingMode.Static)
                    throw new InvalidOperationException($"Cannot use linear shard index for entity kind {entityKind} with node set {nodeSet.ShardName}. ScalingMode must be Static.");

                if (shardIndex < nodeSet.StaticNodeCount)
                {
                    nodeIndex = shardIndex;
                    return;
                }

                shardIndex -= nodeSet.StaticNodeCount;
                nodeSetIndex++;
            }

            throw new InvalidOperationException($"Linear shard index {shardIndex} out of range for {entityKind}");
        }

        /// <summary>
        /// Return the linear shard index for a given NodeSet and Node. Only allowed for entities with on NodeSets with ScalingMode=Static.
        /// </summary>
        /// <param name="entityKind">The entity kind to lookup</param>
        /// <param name="nodeSetIndex">The nodeset index</param>
        /// <param name="nodeIndex">The node index</param>
        public int GetLinearShardIndexForNodeSetAndNode(EntityKind entityKind, int nodeSetIndex, int nodeIndex)
        {
            int shardIndex = 0;
            foreach (NodeSetConfig nodeSet in NodeSets)
            {
                if (!nodeSet.EntityKindMask.IsSet(entityKind))
                    continue;

                if (nodeSet.ScalingMode != NodeSetScalingMode.Static)
                    throw new InvalidOperationException($"Cannot use linear shard index for entity kind {entityKind} with node set {nodeSet.ShardName}. ScalingMode must be Static.");

                if (nodeSetIndex == 0)
                {
                    if (nodeIndex < nodeSet.StaticNodeCount)
                    {
                        shardIndex += nodeIndex;
                        return shardIndex;
                    }
                    else
                        throw new InvalidOperationException($"Node index {nodeIndex} out of range for {nodeSet}");
                }

                shardIndex += nodeSet.StaticNodeCount;
                nodeSetIndex--;
            }

            throw new InvalidOperationException($"NodeSet index {nodeSetIndex} out of range for {entityKind}");
        }

        /// <summary>
        /// Resolves the <see cref="EntityShardId"/> of a given <paramref name="entityKind"/> on a given server node <paramref name="address"/>.
        /// If the given node is not a part of the cluster or the node does not host the EntityKind, returns <c>false</c>.
        /// </summary>
        public bool ResolveNodeShardId(EntityKind entityKind, ClusterNodeAddress address, out EntityShardId shardId)
        {
            int kindNodeSetIndex = 0;
            foreach (NodeSetConfig nodeSet in NodeSets)
            {
                 if (!nodeSet.EntityKindMask.IsSet(entityKind))
                    continue;

                if (nodeSet.ResolveNodeIndex(address, out int shardIndex))
                {
                    shardId = new EntityShardId(entityKind, kindNodeSetIndex, shardIndex);
                    return true;
                }

                 // Count how many NodeSets of the Kind we have seen so far
                 kindNodeSetIndex++;
            }
            shardId = default;
            return false;
        }

        /// <summary>
        /// Resolves the <see cref="EntityShardId"/> of a given <paramref name="entityKind"/> on a given <paramref name="nodeSet"/>
        /// node idenfitied by its <paramref name="nodeIndex"/>.
        /// If the given node is not a part of the cluster or the node does not host the EntityKind, returns <c>false</c>.
        /// </summary>
        public bool ResolveNodeShardId(EntityKind entityKind, NodeSetConfig nodeSet, int nodeIndex, out EntityShardId shardId)
        {
            int kindNodeSetIndex = 0;
            foreach (NodeSetConfig cursor in NodeSets)
            {
                if (!cursor.EntityKindMask.IsSet(entityKind))
                    continue;

                if (ReferenceEquals(cursor, nodeSet))
                {
                    if (nodeIndex >= 0 && nodeIndex < nodeSet.GetMaxNodeCount())
                    {
                        shardId = new EntityShardId(entityKind, kindNodeSetIndex, nodeIndex);
                        return true;
                    }
                }

                 // Count how many NodeSets of the Kind we have seen so far
                 kindNodeSetIndex++;
            }
            shardId = default;
            return false;
        }

        public bool IsMember(ClusterNodeAddress address)
        {
            return NodeSets.Any(nodeSet => nodeSet.ResolveNodeIndex(address, out int _));
        }
    }
}
