// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Cluster;
using Metaplay.Core;
using System;

namespace Metaplay.Server.UdpPassthrough
{
    /// <summary>
    /// Utility for retrieving the public gateways of the UDP passthrough.
    /// </summary>
    public static class UdpPassthroughGateways
    {
        public readonly struct Gateway
        {
            /// <summary>
            /// The publicly accessible domain or address of the gateway to the passthrough listener.
            /// </summary>
            public readonly string FullyQualifiedDomainNameOrAddress;

            /// <summary>
            /// The publicly accessible port of the gateway to the passthrough listener.
            /// </summary>
            public readonly int Port;

            /// <summary>
            /// The EntityId of the passthrough server host entity.
            /// </summary>
            public readonly EntityId AssociatedEntityId;

            /// <summary>
            /// The name of the NodeSet on which this actor resides on.
            /// </summary>
            public readonly string NodeSetName;

            /// <summary>
            /// The address of the node where the entity resides.
            /// </summary>
            public readonly ClusterNodeAddress NodeAddress;

            public Gateway(string fullyQualifiedDomainNameOrAddress, int port, EntityId associatedEntityId, string nodeSetName, ClusterNodeAddress nodeAddress)
            {
                FullyQualifiedDomainNameOrAddress = fullyQualifiedDomainNameOrAddress;
                Port = port;
                AssociatedEntityId = associatedEntityId;
                NodeSetName = nodeSetName;
                NodeAddress = nodeAddress;
            }
        }

        internal static Gateway[] _gateways = Array.Empty<Gateway>();
        internal static Gateway? _localGateway = null;

        /// <summary>
        /// Returns the public gateways of the UDP Passthrough. Returns an empty set if UDP passthrough is not enabled.
        /// </summary>
        public static Gateway[] GetPublicGateways() => _gateways;

        /// <summary>
        /// Returns the public gateway of the current node.
        /// </summary>
        public static Gateway? TryGetGatewayOnThisNode() => _localGateway;

        public static bool TryGetUdpGateway(ClusterNodeAddress nodeAddress, out Gateway outGateway)
        {
            Gateway[] gateways = _gateways;
            foreach (UdpPassthroughGateways.Gateway gateway in gateways)
            {
                if (gateway.NodeAddress == nodeAddress)
                {
                    outGateway = gateway;
                    return true;
                }
            }
            outGateway = default;
            return false;
        }
    }
}
