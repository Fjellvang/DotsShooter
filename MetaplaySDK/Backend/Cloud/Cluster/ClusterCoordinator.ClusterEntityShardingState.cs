// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Cloud.Entity;
using Metaplay.Core;
using System.Collections.Generic;

namespace Metaplay.Cloud.Cluster
{
    public partial class ClusterCoordinatorActor
    {
        class NodeEntityShardingState
        {
            public readonly ClusterNodeAddress              Address;
            public readonly bool                            IsSelf;

            public bool                                     IsConnected;
            public ClusterPhase                             ClusterPhase;
            public EntityGroupPhase[]                       EntityGroupPhases;

            public NodeEntityShardingState(ClusterNodeAddress address, bool isSelf)
            {
                // Configuration
                Address         = address;
                IsSelf          = isSelf;

                // Runtime state
                IsConnected         = isSelf;
                ClusterPhase        = ClusterPhase.Connecting;
                EntityGroupPhases   = new EntityGroupPhase[(int)EntityShardGroup.Last];
            }
        }

        class ClusterEntityShardingState
        {
            public readonly List<NodeEntityShardingState> Nodes = new List<NodeEntityShardingState>();

            public ClusterEntityShardingState(List<NodeEntityShardingState> nodes)
            {
                Nodes = nodes;
            }

            public NodeEntityShardingState TryGetNodeState(ClusterNodeAddress address)
            {
                foreach (NodeEntityShardingState node in Nodes)
                {
                    if (node.Address == address)
                        return node;
                }
                return null;
            }
        }
    }
}
