// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Core;
using System.Linq;

namespace Metaplay.Cloud.Cluster;

partial class ClusterConnectionManager
{
    /// <summary>
    /// The node information available after connection handshake has been complete. As long as two
    /// nodes are connected, they will inform the other when their node information changes.
    /// <para>
    /// To update the current node's information, use <see cref="ClusterConnectionManager.SetSelfClusterNodeInfo(ClusterNodeInformation)"/>
    /// </para>
    /// <para>
    /// To receive updates when any connected node's (or self's) information changes, use <see cref="ClusterConnectionManager.SubscribeToClusterEvents(IActorRef)"/>
    /// </para>
    /// <para>
    /// <b>Warning:</b> Change to the local node's info will cause ALL handlers of <see cref="ClusterConnectionManager.ClusterChangedEvent"/> on ALL connected nodes
    /// to be invoked to receive the update. This includes ALL EntityShard actors. As updates are relatively expensive, you should not store any frequently changing data here.
    /// </para>
    /// </summary>
    public struct ClusterNodeInformation
    {
        /// <summary>
        /// The public IPv4 address of the node, or <c>null</c> if the node has no public IP.
        /// </summary>
        public string                                   PublicIpV4Address           { get; private set; }

        /// <summary>
        /// Lifecycle phase of the specific cluster member
        /// </summary>
        public ClusterPhase                             LocalPhase                  { get; private set; }

        /// <summary>
        /// Is cluster shutting down.
        /// </summary>
        public bool                                     ClusterShutdownRequested    { get; private set; }

        /// <summary>
        /// Phase of the EntityGroups
        /// </summary>
        public EntityGroupPhase[]                       EntityGroupPhases           { get; private set; }

        /// <summary>
        /// The boot time of the server process.
        /// </summary>
        public MetaTime                                 ProcessStartedAt            { get; private set; }

        /// <summary>
        /// Actors of the entity shards on this node.
        /// </summary>
        public MetaDictionary<EntityKind, IActorRef> EntityShardActors           { get; private set; }

        public ClusterNodeInformation(string publicIpV4Address, ClusterPhase localPhase, bool clusterShutdownRequested, EntityGroupPhase[] entityGroupPhases, MetaTime processStartedAt, MetaDictionary<EntityKind, IActorRef> entityShardActors)
        {
            // \note: All reference objects must be cloned. Otherwise the caller of this constructor
            //        could spookily mutate the values from a distance. This struct is expected to
            //        be immutable.
            PublicIpV4Address = publicIpV4Address;
            LocalPhase = localPhase;
            ClusterShutdownRequested = clusterShutdownRequested;
            EntityGroupPhases = (EntityGroupPhase[])entityGroupPhases.Clone();
            ProcessStartedAt = processStartedAt;
            EntityShardActors = entityShardActors != null ? new (entityShardActors) : null;
        }

        /// <summary>
        /// True if the node has received a shutdown signal and gracefully shutting down.
        /// </summary>
        public bool IsNodeShuttingDown()
        {
            return LocalPhase > ClusterPhase.Running;
        }

        public static bool operator ==(ClusterNodeInformation left, ClusterNodeInformation right)
        {
            if (left.PublicIpV4Address != right.PublicIpV4Address)
                return false;
            if (left.LocalPhase != right.LocalPhase)
                return false;
            if (left.ClusterShutdownRequested != right.ClusterShutdownRequested)
                return false;

            if (left.EntityGroupPhases == null && right.EntityGroupPhases == null)
            {
            }
            else if (left.EntityGroupPhases != null && right.EntityGroupPhases != null)
            {
                if (!left.EntityGroupPhases.SequenceEqual(right.EntityGroupPhases))
                    return false;
            }
            else
                return false;

            if (left.ProcessStartedAt != right.ProcessStartedAt)
                return false;

            if (left.EntityShardActors == null && right.EntityShardActors == null)
            {
            }
            else if (left.EntityShardActors != null && right.EntityShardActors != null)
            {
                if (!left.EntityShardActors.SequenceEqual(right.EntityShardActors))
                    return false;
            }
            else
                return false;

            return true;
        }

        public static bool operator !=(ClusterNodeInformation left, ClusterNodeInformation right)
        {
            return !(left == right);
        }

        public override readonly bool Equals(object obj)
        {
            return obj is ClusterNodeInformation info && (this == info);
        }

        public override readonly int GetHashCode()
        {
            uint hash = 0;
            hash += (uint)(PublicIpV4Address?.GetHashCode() ?? 0);
            hash *= 13;
            hash += (uint)LocalPhase.GetHashCode();
            hash *= 13;
            hash += (uint)ClusterShutdownRequested.GetHashCode();
            hash *= 13;
            if (EntityGroupPhases != null)
            {
                foreach (EntityGroupPhase phase in EntityGroupPhases)
                {
                    hash += (uint)phase.GetHashCode();
                    hash *= 13;
                }
            }
            hash += (uint)ProcessStartedAt.GetHashCode();
            hash *= 13;
            if (EntityShardActors != null)
            {
                foreach ((EntityKind key, IActorRef value) in EntityShardActors)
                {
                    hash += (uint)key.GetHashCode();
                    hash *= 13;
                    hash += (uint)(value?.GetHashCode() ?? 0);
                    hash *= 13;
                }
            }
            return (int)hash;
        }
    }
}
