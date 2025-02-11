// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using System;
using static System.FormattableString;

namespace Metaplay.Cloud.Sharding
{
    /// <summary>
    /// Identifies an <see cref="EntityShard"/> instance in the cluster. Identified by combination
    /// of <see cref="EntityKind"/>, <see cref="NodeSetIndex"/>, and <see cref="NodeIndex"/>.
    /// </summary>
    [MetaSerializable]
    public struct EntityShardId : IEquatable<EntityShardId>
    {
        /// <summary>
        /// The maximum value for <see cref="NodeSetIndex"/>
        /// </summary>
        public const int MaxNodeSetIndex = 63;

        /// <summary>
        /// The maximum value for <see cref="NodeIndex"/>
        /// </summary>
        public const int MaxNodeIndex = 4095;

        [MetaMember(1)]
        public EntityKind   Kind         { get; private set; }

        /// <summary>
        /// The index of the nodeset, this is local to the entity kind, therefore you can't use this to look up the nodeset in ClusterConfig.NodeSets without first converting it to a global index.
        /// </summary>
        [MetaMember(2)]
        public int          NodeSetIndex { get; private set; }

        /// <summary>
        /// The index of the node in the nodeset.
        /// </summary>
        [MetaMember(3)]
        public int          NodeIndex   { get; private set; }

        public bool         IsValid => Kind != EntityKind.None;

        [MetaDeserializationConstructor]
        public EntityShardId(EntityKind kind, int nodeSetIndex, int nodeIndex)
        {
            if (kind == EntityKind.None && (nodeIndex != 0 || nodeSetIndex != 0))
                throw new ArgumentException("NodeSet & Shard must be zero for EntityKind.None");
            if (nodeSetIndex < 0 || nodeSetIndex > MaxNodeSetIndex)
                throw new ArgumentOutOfRangeException(nameof(nodeSetIndex), $"nodeSetIndex {nodeSetIndex} exceeds the maximum of {MaxNodeSetIndex}");
            if (nodeIndex < 0 || nodeIndex > MaxNodeIndex)
                throw new ArgumentOutOfRangeException(nameof(nodeIndex), $"nodeIndex {nodeIndex} exceeds the maximum of {MaxNodeIndex}");

            Kind         = kind;
            NodeSetIndex = nodeSetIndex;
            NodeIndex    = nodeIndex;
        }

        public static bool operator ==(EntityShardId a, EntityShardId b) => (a.Kind == b.Kind) && (a.NodeSetIndex == b.NodeSetIndex) && (a.NodeIndex == b.NodeIndex);
        public static bool operator !=(EntityShardId a, EntityShardId b) => (a.Kind != b.Kind) || (a.NodeSetIndex != b.NodeSetIndex) || (a.NodeIndex != b.NodeIndex);

        public bool             Equals      (EntityShardId other) => this == other;
        public override bool    Equals      (object obj) => (obj is EntityShardId other) ? (this == other) : false;

        public bool             IsOfKind(EntityKind kind) => Kind == kind;

        public override int     GetHashCode() => Util.CombineHashCode(Kind.GetHashCode(), NodeSetIndex, NodeIndex);

        public override string  ToString() => Invariant($"EntityShard.{Kind}#{NodeSetIndex}.{NodeIndex}");
    }
}
