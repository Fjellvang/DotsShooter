// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Cluster
{
    /// <summary>
    /// Resolved cluster topology. Resolves on which shards each <see cref="EntityKind"/> should live on. Combined the mappings
    /// from <see cref="EntityConfigRegistry"/> with overrides from <see cref="ClusteringOptions.EntityPlacementOverrides"/>.
    /// </summary>
    public class ShardingTopologySpec
    {
        public string[]                             ValidNodeSetNames           { get; private set; } // All known NodeSet names in the cluster
        public Dictionary<string, EntityKindMask>   NodeSetNameToEntityKinds    { get; private set; } // NodeSetName -> EntityKindMask

        public ShardingTopologySpec(InfrastructureDefinition infraDef, Dictionary<EntityKind, string[]> entityPlacementOverrides)
        {
            // Inputs:
            // - InfraDefinitition: Source for which NodeSets exist in the infrastructure
            // - EntityConfigRegistry: Source for which EntityKinds exist & where should they be placed by default (can override)
            // - EntityPlacementOverrides: Per-EntityKind overrides on which NodeSets they should be placed.

            if (infraDef == null)
                throw new ArgumentNullException(nameof(infraDef));
            if (entityPlacementOverrides == null)
                throw new ArgumentNullException(nameof(entityPlacementOverrides));

            // Resolve which NodeSet names are valid (i.e., the nodeSets that are defined by infra)
            ValidNodeSetNames = infraDef.NodeSets.Select(nodeSet => nodeSet.GetNodeSetNameOrOverride()).ToArray();

            // Check if we're on a singleton (single-node) cluster
            // \todo Make the definition/check more formal?
            bool isSingletonCluster = infraDef.NodeSets.Count() == 1;

            // Resolve mapping: EntityKind -> [NodeSetName]
            Dictionary<EntityKind, string[]> entityKindToNodeSetNames = ResolveEntityKindToNodeSetsMapping(isSingletonCluster, ValidNodeSetNames, entityPlacementOverrides);

            // Resolve mapping: nodeSetName -> EntityKinds
            NodeSetNameToEntityKinds = ResolveNodeSetEntityKinds(ValidNodeSetNames, entityKindToNodeSetNames);
        }

        static Dictionary<EntityKind, string[]> ResolveEntityKindToNodeSetsMapping(bool isSingletonCluster, string[] validNodeSetNames, Dictionary<EntityKind, string[]> placementOverrides)
        {
            // On singleton clusters, there should only be one valid NodeSet name
            string[] singletonNodeSetName = null;
            if (isSingletonCluster)
            {
                if (validNodeSetNames.Length != 1)
                    throw new InvalidOperationException($"Singleton cluster configuration must only have one valid NodeSet, got multiple: {PrettyPrint.Compact(validNodeSetNames)}");
                singletonNodeSetName = [validNodeSetNames[0]];
            }

            Dictionary<EntityKind, string[]> entityKindToNodeSets = new();

            foreach (EntityConfigBase entityConfig in EntityConfigRegistry.Instance.TypeToEntityConfig.Values)
            {
                EntityKind          entityKind  = entityConfig.EntityKind;
                NodeSetPlacement    placement;
                bool                isOverride;

                // If placement override specified, use it, otherwise use the default
                if (placementOverrides.TryGetValue(entityKind, out string[] overrideNodeSetNames))
                {
                    placement = new NodeSetPlacement(overrideNodeSetNames);
                    isOverride = true;
                }
                else
                {
                    placement = entityConfig.NodeSetPlacement;
                    isOverride = false;
                }

                // Check for any invalid NodeSets in default placements.
                if (isSingletonCluster)
                {
                    // In singleton mode, all nodes map to the single one. All overrides and Default placements are ignored.
                }
                else if (placement.PlaceOnAllNodeSets)
                {
                    // Place-on-All is always valid
                }
                else
                {
                    string[] unknownNodeSetNames = placement.NodeSetNames.Where(name => !validNodeSetNames.Contains(name)).ToArray();
                    if (unknownNodeSetNames.Length > 0)
                    {
                        if (isOverride)
                        {
                            throw new InvalidOperationException(
                                $"Unknown NodeSet names specified in ClusteringOptions.EntityPlacementOverrides for EntityKind {entityKind}: {PrettyPrint.Compact(unknownNodeSetNames)}. "
                                + $"This deployment has the following NodeSets: {PrettyPrint.Compact(validNodeSetNames)}.");
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                $"Unknown NodeSet names specified in EntityKindConfig.NodeSetPlacement for EntityKind {entityKind}: {PrettyPrint.Compact(unknownNodeSetNames)}. "
                                + $"This deployment has the following NodeSets: {PrettyPrint.Compact(validNodeSetNames)}.");
                        }
                    }
                }

                // Add entityKind to the per-NodeSet dictionary
                if (isSingletonCluster)
                    entityKindToNodeSets[entityKind] = singletonNodeSetName;
                else if (placement.PlaceOnAllNodeSets)
                    entityKindToNodeSets[entityKind] = validNodeSetNames.ToArray(); // defensive copy, just in case
                else
                    entityKindToNodeSets[entityKind] = placement.NodeSetNames.ToArray(); // defensive copy, just in case
            }

            return entityKindToNodeSets;
        }

        static Dictionary<string, EntityKindMask> ResolveNodeSetEntityKinds(string[] validNodeSetNames, Dictionary<EntityKind, string[]> entityKindToNodeSets)
        {
            // Initialize empty mapping
            Dictionary<string, EntityKindMask> nodeSetEntityKinds = validNodeSetNames.ToDictionary(v => v, _ => EntityKindMask.None);

            // Add all entityKinds to mapping
            foreach ((EntityKind entityKind, string[] nodeSetNames) in entityKindToNodeSets)
            {
                foreach (string nodeSetName in nodeSetNames)
                    nodeSetEntityKinds[nodeSetName] = nodeSetEntityKinds[nodeSetName] | EntityKindMask.FromEntityKind(entityKind);
            }

            return nodeSetEntityKinds;
        }
    }

    [RuntimeOptions("Clustering", isStatic: true, "Configuration options for defining the server cluster.")]
    public class ClusteringOptions : RuntimeOptionsBase
    {
        [MetaDescription("The clustering mode of the server (`Disabled`, `Static` or `Kubernetes`).")]
        public ClusteringMode       Mode                { get; private set; } = IsBotClientApplication ? ClusteringMode.Disabled
                                                                              : IsLocalEnvironment ? ClusteringMode.Static : ClusteringMode.Kubernetes;
        [CommandLineAlias("-ShardingConfig")]
        [MetaDescription("The cluster sharding configuration. This can be a path to a `.json` file or inline JSON.")]
        public string               ShardingConfig      { get; private set; } = null;
        [MetaDescription("The shared identifier the cluster nodes use to identify whether other nodes are part of the same cluster. This is not a secret and is only designed to prevent unintended connections.")]
        public string               Cookie              { get; private set; } = $"{RuntimeEnvironmentInfo.Instance.ApplicationName}-defaultcookie";

        // Akka.Remoting
        [MetaDescription("The hostname of the machine or pod, for example `service-0`.")]
        public string               RemotingHost        { get; private set; } = "127.0.0.1";
        [MetaDescription("Suffix added to `HostName` to form the cluster-local host name. In cloud environments, this is the StatefulSet's name, for example `.service` .")]
        public string               RemotingHostSuffix  { get; private set; } = "";
        [MetaDescription("Suffix added to `HostName + RemotingHostSuffix` to form the fully qualified domain name. In cloud environments, this is the cluster suffix, for example `mygame-develop.svc.cluster.p1-eu.`.")]
        public string               RemotingGlobalSuffix{ get; private set; } = "";

        [CommandLineAlias("-RemotingPort")]
        [MetaDescription("The port to use for Akka.Remote peer connections (set to 0 to use a random available port).")]
        public int                  RemotingPort        { get; private set; } = IsServerApplication ? 6000 : 0;

        [ComputedValue]
        [MetaDescription("The computed address of this cluster node.")]
        public ClusterNodeAddress   SelfAddress         { get; private set; }

        [ComputedValue]
        [MetaDescription("Infrastructure supplied config that describes the infrastructure topology.")]
        public InfrastructureDefinition InfrastructureDefinition { get; private set; } = null;

        [MetaDescription("Specify overrides for EntityKind on which NodeSets they should be placed on in the cluster topology (instead of their default placement).")]
        public Dictionary<EntityKind, string[]> EntityPlacementOverrides { get; private set; } = new();

        [ComputedValue]
        [MetaDescription("Resolved entity sharding topology. Shows which shard(s) each EntityKind is placed on.")]
        public ShardingTopologySpec ResolvedShardingTopology { get; private set; }

        [ComputedValue]
        [MetaDescription("The computed configuration of this cluster node.")]
        public ClusterConfig        ClusterConfig       { get; private set; }

        /// <summary>
        /// True if this node is the cluster's leader node. Cluster leader node is the server instance with the GlobalStateManager
        /// running on it.
        /// </summary>
        [IgnoreDataMember]
        public bool                 IsCurrentNodeClusterLeader  { get; private set; }

        public override async Task OnLoadedAsync()
        {
            if (!string.IsNullOrEmpty(ShardingConfig))
                InfrastructureDefinition = await InfrastructureDefinition.FromDefinitionFileOrLiteralAsync(ShardingConfig);
            else
                InfrastructureDefinition = InfrastructureDefinition.CreateDefaultSingleton();

            // Resolve the final sharding topology: get EntityKind default placements and apply any specified overrides
            ResolvedShardingTopology = new ShardingTopologySpec(InfrastructureDefinition, EntityPlacementOverrides);

            // Construct ClusterConfig
            ClusterConfig = ResolveClusterConfig();

            // Resolve SelfAddress (and check it exists on cluster)
            ClusterNodeAddress selfAddress = new ClusterNodeAddress(RemotingHost + RemotingHostSuffix + MakeAbsoluteDnsSuffix(RemotingGlobalSuffix), RemotingPort);
            if (!ClusterConfig.IsMember(selfAddress))
                throw new InvalidOperationException($"This node ({selfAddress}) is not member of the specified cluster config");
            SelfAddress = selfAddress;

            // Resolve IsCurrentNodeClusterLeader
            bool isCurrentNodeClusterLeader = false;
            if (ClusterConfig.ResolveNodeShardId(EntityKindCloudCore.GlobalStateManager, SelfAddress, out EntityShardId gsmShardId))
            {
                // \note: Singleton node sets are checked to have only one node later in ValidateConfig, but let's check again for clarity.
                if (gsmShardId.NodeSetIndex == 0 && gsmShardId.NodeIndex == 0)
                    isCurrentNodeClusterLeader = true;
            }
            IsCurrentNodeClusterLeader = isCurrentNodeClusterLeader;

            ValidateConfig();
        }

        ClusterConfig ResolveClusterConfig()
        {
            // Resolve Entity Sharding from sharding config and infrastructure definition
            List<NodeSetConfig> nodeSetConfigs = new List<NodeSetConfig>();
            foreach (InfrastructureDefinition.NodeSetDefinition definition in InfrastructureDefinition.NodeSets)
            {
                EntityKindMask shardEntityMask;

                if (definition.IsSingletonNode == true)
                {
                    shardEntityMask = EntityKindMask.All;
                }
                else
                {
                    string nodeSetName = definition.GetNodeSetNameOrOverride();

                    // The EntityKinds for this node set are the ones defined in the topology's matching member.
                    shardEntityMask = ResolvedShardingTopology.NodeSetNameToEntityKinds[nodeSetName];
                }

                // Check the infra config and entity config agree. Some violations can be technically tolerated, but
                // since the resulting system is in wonky state, let's be extra loud about them and log on Error level.

                if (definition.IsPublicNodeSet == true)
                {
                    if (!shardEntityMask.IsSet(EntityKindCloudCore.UdpPassthrough))
                        Log.Error("NodeSet {NodeSet} has a public IP in infra definition, but no UdpPassthrough entity is placed on it. The IP address is unused!", definition.NodeSetName);
                }
                else if (definition.IsPublicNodeSet == false)
                {
                    if (shardEntityMask.IsSet(EntityKindCloudCore.UdpPassthrough))
                        Log.Error("NodeSet {NodeSet} has no public IP in infra definition, but UdpPassthrough entity is placed on it. If UdpPassthrough is in Direct-Connect mode, it will be unreachable.", definition.NodeSetName);
                }

                if (definition.IsAdminApiNodeSet == true)
                {
                    if (!shardEntityMask.IsSet(EntityKindCloudCore.AdminApi))
                        Log.Error("NodeSet {NodeSet} is an AdminApi nodeset in infra definition, but no AdminApi entity is placed on it. Admin Dashboard may not be available!", definition.NodeSetName);
                }
                else if (definition.IsAdminApiNodeSet == false)
                {
                    if (shardEntityMask.IsSet(EntityKindCloudCore.AdminApi))
                        Log.Error("NodeSet {NodeSet} is NOT an AdminApi nodeset in infra definition, but a AdminApi entity is placed on it. Admin Dashboard may not be available!", definition.NodeSetName);
                }

                if (definition.IsConnectionNodeSet == true)
                {
                    if (!shardEntityMask.IsSet(EntityKindCloudCore.Connection))
                        Log.Error("NodeSet {NodeSet} is a Connection nodeset in infra definition, but no Connection entity is placed on it. Clients may not be able to connect!", definition.NodeSetName);
                    if (!shardEntityMask.IsSet(EntityKindCloudCore.WebSocketConnection))
                        Log.Error("NodeSet {NodeSet} is a Connection nodeset in infra definition, but no WebSocketConnection entity is placed on it. Clients may not be able to connect!", definition.NodeSetName);
                }
                else if (definition.IsConnectionNodeSet == false)
                {
                    if (shardEntityMask.IsSet(EntityKindCloudCore.Connection))
                        Log.Error("NodeSet {NodeSet} is NOT a Connection nodeset in infra definition, but a Connection entity is placed on it. Clients may not be able to connect!", definition.NodeSetName);
                    if (shardEntityMask.IsSet(EntityKindCloudCore.WebSocketConnection))
                        Log.Error("NodeSet {NodeSet} is NOT a Connection nodeset in infra definition, but a WebSocketConnection entity is placed on it. Clients may not be able to connect!", definition.NodeSetName);
                }

                NodeSetConfig nodeSetConfig;
                if (definition.GetScalingMode() == NodeSetScalingMode.Static)
                {
                    int nodeCount;
                    if (definition.IsSingletonNode == true)
                        nodeCount = 1;
                    else
                        nodeCount = definition.NodeCount.Value;

                    nodeSetConfig = NodeSetConfig.CreateStaticNodeSet(
                        mode: Mode,
                        shardName: definition.NodeSetName,
                        hostName: definition.HostNameOverride ?? definition.NodeSetName,
                        remotingPort: definition.RemotingPortOverride ?? RemotingPort,
                        globalDnsSuffix: MakeAbsoluteDnsSuffix(definition.GlobalDnsSuffix),
                        nodeCount: nodeCount,
                        entityKindMask: shardEntityMask);
                }
                else if (definition.GetScalingMode() == NodeSetScalingMode.DynamicLinear)
                {
                    nodeSetConfig = NodeSetConfig.CreateDynamicLinearNodeSet(
                        mode: Mode,
                        shardName: definition.NodeSetName,
                        hostName: definition.HostNameOverride ?? definition.NodeSetName,
                        remotingPort: definition.RemotingPortOverride ?? RemotingPort,
                        globalDnsSuffix: MakeAbsoluteDnsSuffix(definition.GlobalDnsSuffix),
                        minNodeCount: definition.DynamicScalingMinNodeCount.Value,
                        maxNodeCount: definition.DynamicScalingMaxNodeCount.Value,
                        entityKindMask: shardEntityMask);
                }
                else
                    throw new InvalidOperationException($"Unsupported scaling mode: {definition.GetScalingMode()}");

                nodeSetConfigs.Add(nodeSetConfig);
            }

            // Sort the nodesets to the following order:
            // 1. Service node
            // 2. Static nodes
            // 3. Dynamically scalable nodes
            //
            // Within each group, keep nodesets in the order they were defined. Since enitity location
            // is often encoded in the EntityId, this order attempts to make service node entities to
            // have entityId = 0.
            //
            // The separation of dynamic and static nodesets attempts to keep the static at lower indices
            // which are expected to be more stable, as in less likely to be added more. This should
            // result in more "stable" entityIDs.

            List<NodeSetConfig> originalOrder = new List<NodeSetConfig>(nodeSetConfigs);
            nodeSetConfigs.Sort((NodeSetConfig a, NodeSetConfig b) =>
            {
                int GetCategory(NodeSetConfig config)
                {
                    if (config.EntityKindMask.IsSet(EntityKindCloudCore.GlobalStateManager))
                    {
                        // Service
                        return 1;
                    }
                    else if (config.ScalingMode == NodeSetScalingMode.Static)
                    {
                        // Static nodeset
                        return 2;
                    }
                    else
                    {
                        // Dynamic nodeset
                        return 3;
                    }
                }

                // Sort by category
                int categoryA = GetCategory(a);
                int categoryB = GetCategory(b);
                if (categoryA != categoryB)
                    return categoryA - categoryB;

                // Within a category, keep existing order
                return originalOrder.IndexOf(a) - originalOrder.IndexOf(b);
            });

            return new ClusterConfig(Mode, nodeSetConfigs);
        }

        void ValidateConfig()
        {
            // Check that all active EntityKinds are be mapped to some NodeSet(s) in the cluster config
            EntityKind[] allEntityKinds =
                EntityConfigRegistry.Instance.TypeToEntityConfig.Values
                .Select(entityConfig => entityConfig.EntityKind)
                .ToArray();
            EntityKindMask requiredEntityKinds = new EntityKindMask(allEntityKinds);

            // Check that all EntityKind shards are included on at least one node of the cluster
            EntityKindMask foundEntityKinds = EntityKindMask.None;
            foreach (NodeSetConfig nodeSetConfig in ClusterConfig.EnumerateNodeSets())
                foundEntityKinds |= nodeSetConfig.EntityKindMask;

            // In case any EntityKinds are missing, throw an error with the list of missing kinds
            if ((foundEntityKinds & requiredEntityKinds) != requiredEntityKinds)
                throw new InvalidOperationException($"Missing EntityKinds from configured shards (usually in Options.base.yaml): {EntityKindMask.Except(requiredEntityKinds, foundEntityKinds)}");

            // Iterate over all active EntityKinds in the active (resolved) sharding topology
            foreach (EntityKind entityKind in allEntityKinds)
            {
                EntityConfigBase entityConfig = EntityConfigRegistry.Instance.GetConfig(entityKind);

                // Validate: Singleton services should only be allowed on a) a single NodeSet, b) a NodeSet with exactly one node
                bool isSingleton = entityConfig.ShardingStrategy is StaticServiceShardingStrategy serviceStrategy && serviceStrategy.IsSingleton;
                if (isSingleton)
                {
                    NodeSetConfig[] entityNodeSets = ClusterConfig.GetNodeSetsForEntityKind(entityKind);

                    if (entityNodeSets.Length > 1)
                        throw new InvalidOperationException($"Singleton service EntityKind.{entityKind} is mapped to multiple NodeSets ({PrettyPrint.Compact(entityNodeSets)}). Singleton services can only be mapped to a single NodeSet.");

                    NodeSetConfig entityNodeSet = entityNodeSets[0];
                    if (entityNodeSet.ScalingMode != NodeSetScalingMode.Static)
                        throw new InvalidOperationException($"Singleton service EntityKind.{entityKind} is mapped to NodeSet that is dynamically scaling. Singleton services can only be mapped to static, fixed sized nodesets.");
                    if (entityNodeSet.StaticNodeCount > 1)
                        throw new InvalidOperationException($"Singleton service EntityKind.{entityKind} is mapped to NodeSet with {entityNodeSet.StaticNodeCount} nodes. Singleton services can only be mapped to NodeSets with one node.");
                }

                // Validate: Static strategies are only allowed on the static part of the cluster
                bool isStaticallySharded = entityConfig.ShardingStrategy is StaticModuloShardingStrategy || entityConfig.ShardingStrategy is StaticServiceShardingStrategy;
                if (isStaticallySharded)
                {
                    NodeSetConfig[] entityNodeSets = ClusterConfig.GetNodeSetsForEntityKind(entityKind);
                    foreach (NodeSetConfig nodeSet in entityNodeSets)
                    {
                        if (nodeSet.ScalingMode != NodeSetScalingMode.Static)
                        {
                            throw new InvalidOperationException(
                                $"EntityKind.{entityKind} has '{entityConfig.ShardingStrategy.GetType().Name}' sharding strategy in its [EntityConfig] which requires NodeSets to have Static scaling mode. "
                                + $"The entity kind is mapped to NodeSet {nodeSet.ShardName} which has scaling mode '{nodeSet.ScalingMode}'. "
                                + "Check the shard config in helm values, the NodeSetPlacement in the [EntityConfig] and potential overrides in Clustering:EntityPlacementOverrides in Runtime Options (Options.<env>.yaml).");
                        }
                    }
                }
            }

            // \todo Validate: NodeSets with no real payloads should cause an error (ie, there must be something else running besides the various Daemons using NodeSetPlacement.All)
        }

        /// <summary>
        /// Makes a DNS component into a string suffix such that by appending it it into a relative dns name, the result
        /// into an absolute dns name. If no suffix component is given, then returns empty string. Note that by appending
        /// empty string, the initial relative DNS path remains relative.
        /// <para>
        /// In concrete terms, makes "mycluster.svc.game.io" into ".mycluster.svc.game.io."
        /// </para>
        /// </summary>
        static string MakeAbsoluteDnsSuffix(string suffixDns)
        {
            if (string.IsNullOrEmpty(suffixDns))
            {
                return string.Empty;
            }
            else
            {
                string globalDnsSuffix = suffixDns;

                // Ensure appendable
                if (!globalDnsSuffix.StartsWith('.'))
                    globalDnsSuffix = '.' + globalDnsSuffix;

                // Ensure global
                if (!globalDnsSuffix.EndsWith('.'))
                    globalDnsSuffix = globalDnsSuffix + '.';

                return globalDnsSuffix;
            }
        }
    }
}
