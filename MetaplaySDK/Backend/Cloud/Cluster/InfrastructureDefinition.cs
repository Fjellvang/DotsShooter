// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Cluster
{
    /// <summary>
    /// Defines the layout of nodes and their related infrastucture of the whole cluster.
    /// </summary>
    public class InfrastructureDefinition
    {
        /// <summary>
        /// Defines a single nodeset in the cluster. Nodeset can be seen as a StatefulSet (kubernetes) or/and an Autoscaling Group (aws).
        /// </summary>
        public class NodeSetDefinition
        {
            /// <summary>
            /// The name of this nodeset. All nodes on this nodeset will have this name as a component of the hostname.
            /// </summary>
            [MetaDescription("The name of this nodeset. All nodes on this nodeset will have this name as a component of the hostname.")]
            [JsonProperty("name", Required = Required.Always)]
            public string NodeSetName { get; private set; }

            /// <summary>
            /// The global DNS suffix for nodes of this nodeset. For example in a kubernetes environment this could be `idler-develop.svc.cluster.p1-eu.`. If this is not set, relative domain names are used for discovery.
            /// </summary>
            [MetaDescription("The global DNS suffix for nodes of this nodeset. For example in a kubernetes environment this could be `idler-develop.svc.cluster.p1-eu.`. If this is not set, relative domain names are used for discovery.")]
            [JsonProperty("globalSuffix", NullValueHandling = NullValueHandling.Ignore)]
            public string GlobalDnsSuffix { get; private set; }

            [MetaDescription("Declares the Entity workload in the sharding configuration in server config. If not given, `name` is used instead.")]
            [JsonProperty("topologyKey", NullValueHandling = NullValueHandling.Ignore)]
            public string TopologyNameOverride { get; private set; }

            [MetaDescription("If true, this nodeset is the only nodeset and there is only one node. The only node runs all Entities and hence does not require defining sharding configuration in server config.")]
            [JsonProperty("singleton", NullValueHandling = NullValueHandling.Ignore)]
            public bool? IsSingletonNode { get; private set; }

            [MetaDescription("The count of nodes on this nodecount. In singleton mode, must be 1 or omitted.")]
            [JsonProperty("nodeCount", NullValueHandling = NullValueHandling.Ignore)]
            public int? NodeCount { get; private set; }

            [MetaDescription("If true, nodes on this nodeset have a public IP address")]
            [JsonProperty("public", NullValueHandling = NullValueHandling.Ignore)]
            public bool? IsPublicNodeSet { get; private set; }

            [MetaDescription("If true, infra expects AdminAPI to be hosted on these nodes. Specifically, infrastructure routes AdminApi requests to these nodes.")]
            [JsonProperty("adminApi", NullValueHandling = NullValueHandling.Ignore)]
            public bool? IsAdminApiNodeSet { get; private set; }

            [MetaDescription("If true, infra expects ClientConnection to be hosted on these node. Specifically, infrastructure routes client connections to these nodes.")]
            [JsonProperty("connection", NullValueHandling = NullValueHandling.Ignore)]
            public bool? IsConnectionNodeSet { get; private set; }

            // \note: Only used in local settings
            [MetaDescription("Declares the hostname (component) for the node. If not given, `name` is used instead.")]
            [JsonProperty("hostName", NullValueHandling = NullValueHandling.Ignore)]
            public string HostNameOverride { get; private set; }

            // \note: Only used in local settings
            [MetaDescription("Declares the remoting port for this nodeset. Note that in Static clustering mode, nodes get sequential ports. If not given, the port in server config is used instead.")]
            [JsonProperty("remotingPort", NullValueHandling = NullValueHandling.Ignore)]
            public int? RemotingPortOverride { get; private set; }

            [MetaDescription("Defines how the nodeset scales. 'static' for fixed size stateful set, 'dynamic' for scalable stateful set.")]
            [JsonProperty("scaling", NullValueHandling = NullValueHandling.Ignore)]
            public string ScalingMode { get; private set; }

            [MetaDescription("For a dynamically scaling nodeset, the minimum number of nodes.")]
            [JsonProperty("minNodeCount", NullValueHandling = NullValueHandling.Ignore)]
            public int? DynamicScalingMinNodeCount { get; private set; }

            [MetaDescription("For a dynamically scaling nodeset, the maximum number of nodes.")]
            [JsonProperty("maxNodeCount", NullValueHandling = NullValueHandling.Ignore)]
            public int? DynamicScalingMaxNodeCount { get; private set; }

            /// <summary>
            /// Creates a definition for the Default full node. This is used when no definition is given.
            /// </summary>
            public static NodeSetDefinition CreateFullSingletonDefinition()
            {
                // \note: In a singleton setup, we don't need to specify infra flags. This format also
                //        matches legacy infra definition, ensuring backwards compatibility.
                return new NodeSetDefinition()
                {
                    NodeSetName = "singleton",
                    HostNameOverride = "127.0.0.1",
                    IsSingletonNode = true,
                };
            }

            /// <summary>
            /// Returns the name of this node set. For legacy reasons, overriding the name with <see cref="TopologyNameOverride"/>
            /// is supported.
            /// </summary>
            /// <returns></returns>
            public string GetNodeSetNameOrOverride()
            {
                if (!string.IsNullOrEmpty(TopologyNameOverride))
                    return TopologyNameOverride;

                return NodeSetName;
            }

            public NodeSetScalingMode GetScalingMode()
            {
                if (ScalingMode == null || ScalingMode == "static")
                    return NodeSetScalingMode.Static;
                else if (ScalingMode == "dynamic")
                    return NodeSetScalingMode.DynamicLinear;

                throw new InvalidDataException($"Nodeset '{NodeSetName}' has unknown 'scaling' mode: {ScalingMode}");
            }
        }

        public readonly NodeSetDefinition[] NodeSets;

        InfrastructureDefinition(NodeSetDefinition[] nodeSets)
        {
            NodeSets = nodeSets;
        }

        /// <summary>
        /// Check if provided string is a valid path. Used to distinguish between direct json config
        /// payloads and paths to .json files.
        /// </summary>
        static bool IsValidPath(string str)
        {
            if (str.Contains('[') || str.Contains(']') || str.Contains('}') || str.Contains('{') || str.Contains('\"') || str.Contains(':'))
                return false;
            return true;
        }

        public static async Task<InfrastructureDefinition> FromDefinitionFileOrLiteralAsync(string pathOrLiteral)
        {
            try
            {
                string contentJson;
                if (IsValidPath(pathOrLiteral))
                    contentJson = await File.ReadAllTextAsync(pathOrLiteral);
                else
                    contentJson = pathOrLiteral;

                NodeSetDefinition[] nodeSetDefinitions = JsonConvert.DeserializeObject<NodeSetDefinition[]>(contentJson);

                if (nodeSetDefinitions == null || nodeSetDefinitions.Length == 0)
                    throw new InvalidDataException("Must define at least one nodeset object in definition file");

                foreach (NodeSetDefinition nodeSet in nodeSetDefinitions)
                {
                    if (string.IsNullOrEmpty(nodeSet.NodeSetName))
                        throw new InvalidDataException("Missing 'name' for nodeset");

                    if (nodeSet.RemotingPortOverride.HasValue && (nodeSet.RemotingPortOverride.Value <= 0 || nodeSet.RemotingPortOverride.Value >= 65536))
                        throw new InvalidDataException($"Must have valid 'remotingPort' for nodeset '{nodeSet.NodeSetName}', got {nodeSet.RemotingPortOverride.Value}");
                    if (nodeSet.NodeCount <= 0 || nodeSet.NodeCount >= 10_000)
                        throw new InvalidDataException($"Invalid nodeCount for nodeset '{nodeSet.NodeSetName}', got {nodeSet.NodeCount}");
                    if (nodeSet.DynamicScalingMinNodeCount <= 0 || nodeSet.DynamicScalingMinNodeCount >= 10_000)
                        throw new InvalidDataException($"Invalid minNodeCount for nodeset '{nodeSet.NodeSetName}', got {nodeSet.DynamicScalingMinNodeCount}");
                    if (nodeSet.DynamicScalingMaxNodeCount <= 0 || nodeSet.DynamicScalingMaxNodeCount >= 10_000)
                        throw new InvalidDataException($"Invalid maxNodeCount for nodeset '{nodeSet.NodeSetName}', got {nodeSet.DynamicScalingMaxNodeCount}");

                    if (nodeSet.IsSingletonNode == true)
                    {
                        if (nodeSetDefinitions.Length > 1)
                            throw new InvalidDataException($"Nodeset '{nodeSet.NodeSetName}' is configured as singleton, which is not allowed when multiple shard types are defined");
                        if (nodeSet.NodeCount.HasValue && nodeSet.NodeCount.Value != 1)
                            throw new InvalidDataException($"Nodeset '{nodeSet.NodeSetName}' is configured as singleton. 'nodeCount' must be 1 or omitted, got {nodeSet.NodeCount.Value}");
                        if (nodeSet.GetScalingMode() != NodeSetScalingMode.Static)
                            throw new InvalidDataException($"Nodeset '{nodeSet.NodeSetName}' is configured as singleton. 'scaling' must be 'static' or omitted, got {nodeSet.ScalingMode}");
                    }
                    else if (nodeSet.GetScalingMode() == NodeSetScalingMode.Static)
                    {
                        if (!nodeSet.NodeCount.HasValue)
                            throw new InvalidDataException($"Must have 'nodeCount' for non-singleton nodeset '{nodeSet.NodeSetName}'");
                    }

                    if (nodeSet.GetScalingMode() == NodeSetScalingMode.Static)
                    {
                        if (nodeSet.DynamicScalingMinNodeCount.HasValue)
                            throw new InvalidDataException($"Nodeset '{nodeSet.NodeSetName}' is statically sized, but has 'minNodeCount'. This field is only used for dynamically scaling nodesets.");
                        if (nodeSet.DynamicScalingMaxNodeCount.HasValue)
                            throw new InvalidDataException($"Nodeset '{nodeSet.NodeSetName}' is statically sized, but has 'maxNodeCount'. This field is only used for dynamically scaling nodesets.");
                    }
                    else if (nodeSet.GetScalingMode() == NodeSetScalingMode.DynamicLinear)
                    {
                        if (!nodeSet.DynamicScalingMinNodeCount.HasValue)
                            throw new InvalidDataException($"Nodeset '{nodeSet.NodeSetName}' is dynamically scaling, but is missing 'minNodeCount'.");
                        if (!nodeSet.DynamicScalingMaxNodeCount.HasValue)
                            throw new InvalidDataException($"Nodeset '{nodeSet.NodeSetName}' is dynamically scaling, but is missing 'maxNodeCount'.");

                        if (nodeSet.DynamicScalingMaxNodeCount < nodeSet.DynamicScalingMinNodeCount)
                            throw new InvalidDataException($"Nodeset '{nodeSet.NodeSetName}' 'maxNodeCount' must be greater or equal to 'minNodeCount'.");

                        if (nodeSet.NodeCount.HasValue)
                            throw new InvalidDataException($"Nodeset '{nodeSet.NodeSetName}' is dynamically scalingbut is has 'nodeCount'. This field is only used for statically sized nodesets.");
                    }
                }

                return new InfrastructureDefinition(nodeSetDefinitions);
            }
            catch (Exception ex)
            {
                string source;
                if (IsValidPath(pathOrLiteral))
                    source = $"file {pathOrLiteral}";
                else
                    source = "argument";

                throw new InvalidDataException($"Infrastructure definition {source} is not valid.", ex);
            }
        }

        public static InfrastructureDefinition CreateDefaultSingleton()
        {
            NodeSetDefinition[] nodeSetDefinitions = { NodeSetDefinition.CreateFullSingletonDefinition() };
            return new InfrastructureDefinition(nodeSetDefinitions);
        }
    }
}
