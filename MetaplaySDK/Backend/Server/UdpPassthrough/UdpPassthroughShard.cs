// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Server.UdpPassthrough
{
    [EntityConfig]
    public class UdpPassthroughShardConfig : EphemeralEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindCloudCore.UdpPassthrough;
        public override bool                AllowEntitySpawn        => true;
        public override Type                EntityShardType         => typeof(UdpPassthroughShard);
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.All;
        public override IShardingStrategy   ShardingStrategy        => ShardingStrategies.CreateDynamicService();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(60);

        // \note: If disabled, use base actor to trick Registry error checks to pass. This shard will be disabled anyway so it's harmless.
        public override Type                EntityActorType         => UdpPassthroughShard.TryGetEntityActorTypeForConfig() ?? typeof(UdpPassthroughHostActorBase);
    }

    class UdpPassthroughShard : EntityShard
    {
        /// <summary>
        /// Returns the only concrete listener type if it is available. Otherwise null.
        /// </summary>
        internal static Type TryGetEntityActorTypeForConfig()
        {
            UdpPassthroughOptions opts = RuntimeOptionsRegistry.Instance.GetCurrent<UdpPassthroughOptions>();
            if (opts.UseDebugServer)
            {
                return typeof(UdpPassthroughDebugServerActor);
            }

            List<Type> listenerTypes = GetListenerTypes();
            if (listenerTypes.Count == 0)
                return null; // no implementation.
            if (listenerTypes.Count > 1)
                return null; // ambiguous implementation.
            return listenerTypes[0];
        }

        static List<Type> GetListenerTypes()
        {
            return new List<Type>(IntegrationRegistry.IntegrationClasses<UdpPassthroughHostActorBase>().Where(type => type != typeof(UdpPassthroughDebugServerActor)));
        }

        public UdpPassthroughShard(EntityShardConfig shardConfig) : base(shardConfig)
        {
            // \hack: mutating directly to clear default
            _alwaysRunningEntities.Clear();

            UdpPassthroughOptions options = RuntimeOptionsRegistry.Instance.GetCurrent<UdpPassthroughOptions>();

            List<Type> listenerTypes = GetListenerTypes();
            Type listenerType;

            // Resolve the listener. This must be unambiguous.
            if (options.UseDebugServer)
            {
                listenerType = typeof(UdpPassthroughDebugServerActor);
            }
            else if (listenerTypes.Count > 1)
            {
                throw new InvalidOperationException($"Ambiguous implementation for UdpPassthroughHostActorBase. Could be any of {string.Join(" ,", listenerTypes.Select(type => type.ToNamespaceQualifiedTypeString()))}.");
            }
            else if (!options.Enabled)
            {
                if (listenerTypes.Count != 0)
                    _log.Warning("UDP passthrough listener is defined as {ActorName} but passthrough is disabled.", listenerTypes[0].ToNamespaceQualifiedTypeString());
                return;
            }
            else if (listenerTypes.Count == 0)
            {
                // Passthrough is enabled, but there is no implementation.
                // Since `Enabled` can be set in infra, we want to tolerate (but warn loudly) cases where the old non-supporting server could be deployed on udp-enabled infra.
                _log.Error("UDP passthrough is enabled but there is no implementation for UdpPassthroughHostActorBase. UDP passthrough is disabled.");
                return;
            }
            else
            {
                // Success
                listenerType = listenerTypes[0];
            }

            // Sanity checks
            EntityConfigBase entityConfig = EntityConfigRegistry.Instance.GetConfig(_selfShardId.Kind);
            if (listenerType != entityConfig.EntityActorType)
                throw new InvalidOperationException("Internal error. Listener type resolution resolved into an inconsistent type.");
        }

        protected override async Task InitializeAsync()
        {
            UdpPassthroughOptions options = RuntimeOptionsRegistry.Instance.GetCurrent<UdpPassthroughOptions>();

            // Setup gateways. We inspect these on the following lines.
            UpdateGateways(CurrentClusterState);

            // Spawn the host actor if this shard be active. Otherwise, do nothing.
            UdpPassthroughGateways.Gateway? currentNodeGateway = UdpPassthroughGateways.TryGetGatewayOnThisNode();
            if (currentNodeGateway.HasValue)
            {
                // Ensure that the relevant Entity is running
                EntityId localEntityId = currentNodeGateway.Value.AssociatedEntityId;

                // \hack: mutating directly
                _alwaysRunningEntities.Add(localEntityId);

                _log.Debug("Starting UDP passthrough for public port {PublicPort}", currentNodeGateway.Value.Port);
            }
            else
            {
                if (options.UseCloudPublicIp)
                {
                    _log.Debug("UDP passthrough not enabled on this node due to the missing public IP: {ShardId}", _selfShardId);
                }
                else if (UdpPassthroughOptions.IsCloudEnvironment)
                {
                    _log.Debug("UDP passthrough not enabled. No external port for this pod: {ShardId}", _selfShardId);
                }
                else
                {
                    _log.Debug("UDP passthrough not enabled. No local port for this shard: {ShardId}", _selfShardId);
                }
            }

            await base.InitializeAsync();
        }

        protected override void OnClusterChanged(ClusterConnectionManager.ClusterChangedEvent cluster)
        {
            UpdateGateways(cluster);
        }

        void UpdateGateways(ClusterConnectionManager.ClusterChangedEvent cluster)
        {
            (UdpPassthroughGateways.Gateway[] gatewaysArray, UdpPassthroughGateways.Gateway? currentNodeGateway) = GetActiveGateways(cluster);
            Thread.MemoryBarrier();
            UdpPassthroughGateways._gateways = gatewaysArray;
            UdpPassthroughGateways._localGateway = currentNodeGateway;
        }

        (UdpPassthroughGateways.Gateway[], UdpPassthroughGateways.Gateway?) GetActiveGateways(ClusterConnectionManager.ClusterChangedEvent cluster)
        {
            (UdpPassthroughOptions udpOptions, ClusteringOptions clusterOpts) = RuntimeOptionsRegistry.Instance.GetCurrent<UdpPassthroughOptions, ClusteringOptions>();

            if (!udpOptions.Enabled)
                return (Array.Empty<UdpPassthroughGateways.Gateway>(), null);

            if (udpOptions.UseCloudPublicIp)
            {
                // In CloudPublicIp-mode (the only non-legacy mode), the gateways are the public IPs of the running nodes.

                List<UdpPassthroughGateways.Gateway> gatewayList = new List<UdpPassthroughGateways.Gateway>();
                UdpPassthroughGateways.Gateway? localGateway = null;

                foreach (ClusterConnectionManager.ClusterChangedEvent.ClusterMember member in cluster.Members)
                {
                    // Is public node?
                    if (member.Info.PublicIpV4Address == null)
                        continue;

                    // Is udp passthrough placed on the node?
                    if (!clusterOpts.ClusterConfig.ResolveNodeShardId(EntityKindCloudCore.UdpPassthrough, member.Address, out EntityShardId shardId))
                        continue;

                    // Is UDP shard running?
                    if (member.Info.EntityGroupPhases[(int)EntityShardGroup.Workloads] != EntityGroupPhase.Running)
                        continue;

                    EntityId                        actor       = DynamicServiceShardingStrategy.CreatePlacedEntityId(shardId);
                    NodeSetConfig                   nodeset     = clusterOpts.ClusterConfig.GetNodeSetConfigForShardId(shardId);
                    UdpPassthroughGateways.Gateway  nodeGateway = new UdpPassthroughGateways.Gateway(member.Info.PublicIpV4Address, udpOptions.CloudPublicIpv4Port, actor, nodeset.ShardName, member.Address);

                    gatewayList.Add(nodeGateway);
                    if (member.Address == clusterOpts.SelfAddress)
                        localGateway = nodeGateway;
                }

                _log.Info("Detected the UDP listeners: [{Listeners}]", string.Join(";", System.Linq.Enumerable.Select(gatewayList, gw => System.FormattableString.Invariant($"{gw.FullyQualifiedDomainNameOrAddress}:{gw.Port}//{gw.AssociatedEntityId}"))));
                return (gatewayList.ToArray(), localGateway);
            }

            if (UdpPassthroughOptions.IsCloudEnvironment)
            {
                // \todo: Remove this loadbalancer-driven mode of operation

                // On Cloud, the gateways are on Gateway domain in the gateway port range.
                int loadbalancerPort = udpOptions.GatewayPortRangeStart;
                List<UdpPassthroughGateways.Gateway> gateways = new List<UdpPassthroughGateways.Gateway>();
                UdpPassthroughGateways.Gateway? localGateway = null;

                EntityShardId? localShardId = null;
                if (clusterOpts.ClusterConfig.ResolveNodeShardId(EntityKindCloudCore.UdpPassthrough, clusterOpts.SelfAddress, out EntityShardId localShardId_))
                {
                    localShardId = localShardId_;
                }

                foreach ((NodeSetConfig nodeSet, int kindNodeIndex) in clusterOpts.ClusterConfig.GetNodeSetsForEntityKind(EntityKindCloudCore.UdpPassthrough).ZipWithIndex())
                {
                    for (int nodeIndex = 0; nodeIndex < nodeSet.GetMaxNodeCount(); ++nodeIndex)
                    {
                        EntityShardId shardId = new EntityShardId(EntityKindCloudCore.UdpPassthrough, nodeSetIndex: kindNodeIndex, nodeIndex: nodeIndex);

                        // For each node that has a corresponding port, add it to the list
                        // Note the inclusive range.
                        if (loadbalancerPort <= udpOptions.GatewayPortRangeEnd)
                        {
                            EntityId entityId = DynamicServiceShardingStrategy.CreatePlacedEntityId(EntityKindCloudCore.UdpPassthrough, nodeSetIndex: shardId.NodeSetIndex, nodeIndex: shardId.NodeIndex);
                            UdpPassthroughGateways.Gateway gateway = new UdpPassthroughGateways.Gateway(udpOptions.PublicFullyQualifiedDomainName, loadbalancerPort, entityId, nodeSet.ShardName, nodeSet.ResolveNodeAddress(nodeIndex));

                            gateways.Add(gateway);

                            if (localShardId.HasValue && localShardId == shardId)
                                localGateway = gateway;
                        }

                        loadbalancerPort++;
                    }
                }

                return (gateways.ToArray(), localGateway);
            }
            else
            {
                // Local environment, i.e. no gateway. Since local environment can have only one listener on the Local port, allocate that to the first shard.
                // Using the Local port as the public port.
                EntityId entityId = DynamicServiceShardingStrategy.CreatePlacedEntityId(EntityKindCloudCore.UdpPassthrough, nodeSetIndex: 0, nodeIndex: 0);
                NodeSetConfig firstNodeset = clusterOpts.ClusterConfig.GetNodeSetsForEntityKind(EntityKindCloudCore.UdpPassthrough)[0];
                ClusterNodeAddress firstNodeAddress = firstNodeset.ResolveNodeAddress(nodeIndex: 0);
                UdpPassthroughGateways.Gateway[] gateways = new UdpPassthroughGateways.Gateway[1]
                {
                    new UdpPassthroughGateways.Gateway(udpOptions.PublicFullyQualifiedDomainName, udpOptions.LocalServerPort, entityId, firstNodeset.ShardName, firstNodeAddress)
                };

                // If this node contributes to UdpPassthrough, and is the first one, then let's have this be the local implementation
                UdpPassthroughGateways.Gateway? localGateway = null;
                if (clusterOpts.ClusterConfig.ResolveNodeShardId(EntityKindCloudCore.UdpPassthrough, clusterOpts.SelfAddress, out EntityShardId selfShardId))
                {
                    if (selfShardId.NodeSetIndex == 0 && selfShardId.NodeIndex == 0)
                        localGateway = gateways[0];
                }

                return (gateways, localGateway);
            }
        }
    }
}
