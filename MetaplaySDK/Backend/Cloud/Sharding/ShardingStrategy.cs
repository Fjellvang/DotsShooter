// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using System;
using System.Collections.Generic;

namespace Metaplay.Cloud.Sharding
{
    /// <summary>
    /// Provides mapping from a <see cref="EntityId"/> to the responsible <see cref="EntityShard"/> and the responsible <see cref="EntityId"/>
    /// within it. The concept of "responsible" is dependent on vantage point -- for example, EntityId of a service could be mapped to the
    /// Local shard and the Local service within it.
    /// </summary>
    public interface IShardingStrategy
    {
        /// <summary>
        /// For a given entity, returns the responsible <see cref="EntityShard"/> and the responsible <see cref="EntityId"/>
        /// within it. If entity has no shard, returns <c>default</c>. The <paramref name="entityId"/> should be generated with
        /// the corresponding strategy's factory methods.
        /// </summary>
        /// <exception cref="ArgumentException">if the supplied <paramref name="entityId"/> is invalid for the strategy.</exception>
        EntityShardId ResolveShardId(EntityId entityId);

        /// <summary>
        /// Resolve which <see cref="EntityId"/>s should be auto-spawned on a given <see cref="EntityShardId"/>.
        /// </summary>
        /// <param name="shardId"></param>
        /// <returns></returns>
        IReadOnlyList<EntityId> GetAutoSpawnEntities(EntityShardId shardId);
    }

    /// <summary>
    /// Distributes entities uniformly to shards based on <c>EntityId</c>. The entities are sharded across the pods/nodes
    /// in round-robin fashion using modulo arithmetic: <c>shardId = entityId.Value % numShards</c>.
    /// </summary>
    public class StaticModuloShardingStrategy : IShardingStrategy
    {
        public StaticModuloShardingStrategy()
        {
        }

        public EntityShardId ResolveShardId(EntityId entityId)
        {
            ClusterConfig clusterConfig = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>().ClusterConfig;
#pragma warning disable CS0618 // Type or member is obsolete
            int numShards = clusterConfig.GetNodeCountForEntityKind(entityId.Kind);
#pragma warning restore CS0618 // Type or member is obsolete

            if (numShards <= 0)
                throw new ArgumentException($"Invalid amount ({numShards}) of shard instances defined for {entityId.Kind} shard in ClusterConfig. There must be at least one instance.");

            int linearShardIndex = (int)(entityId.Value % (uint)numShards);
            clusterConfig.GetNodeSetAndNodeForLinearShardIndex(entityId.Kind, linearShardIndex, out int nodeSetIndex, out int nodeIndex);

            return new EntityShardId(entityId.Kind, nodeSetIndex, nodeIndex);
        }

        public IReadOnlyList<EntityId> GetAutoSpawnEntities(EntityShardId shardId) => null;
    }

    /// <summary>
    /// Automatically spawns an entity on each <c>EntityShard</c> matching the <c>EntityKind</c>.
    /// Only one instance is created if <see cref="IsSingleton"/> is <c>True</c>.
    /// </summary>
    public class StaticServiceShardingStrategy : IShardingStrategy
    {
        public readonly bool IsSingleton;

        public StaticServiceShardingStrategy(bool isSingleton)
        {
            IsSingleton = isSingleton;
        }

        public EntityShardId ResolveShardId(EntityId entityId)
        {
            ClusterConfig clusterConfig = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>().ClusterConfig;
#pragma warning disable CS0618 // Type or member is obsolete
            int numShards = clusterConfig.GetNodeCountForEntityKind(entityId.Kind);
#pragma warning restore CS0618 // Type or member is obsolete

            if (numShards <= 0)
                throw new ArgumentException($"Invalid amount ({numShards}) of shard instances defined for {entityId.Kind} shard in ClusterConfig. There must be at least one instance.");

            // \todo Consider using this instead of modulo logic in the future -- we should aim for a more direct mapping from EntityId to ShardId
            //if (entityId.Value >= (ulong)numShards)
            //    throw new ArgumentException($"EntityId.Value ({entityId.Value}) is greater-or-equal to numShards ({numShards}).");
            //return new EntityShardId(entityId.Kind, entityId.Value);

            int linearShardIndex = (int)(entityId.Value % (uint)numShards);
            clusterConfig.GetNodeSetAndNodeForLinearShardIndex(entityId.Kind, linearShardIndex, out int nodeSetIndex, out int nodeIndex);

            return new EntityShardId(entityId.Kind, nodeSetIndex, nodeIndex);
        }

        public IReadOnlyList<EntityId> GetAutoSpawnEntities(EntityShardId shardId)
        {
            // For singleton services, only allow spawning on the first shard
            if (IsSingleton && (shardId.NodeSetIndex != 0 || shardId.NodeIndex != 0))
                return null;

            ClusterConfig clusterConfig = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>().ClusterConfig;
            int linearShardIndex = clusterConfig.GetLinearShardIndexForNodeSetAndNode(shardId.Kind, shardId.NodeSetIndex, shardId.NodeIndex);

            // Otherwise, spawn a service on this shard
            return new EntityId[] { EntityId.Create(shardId.Kind, (uint)linearShardIndex) };
        }
    }

    /// <summary>
    /// Spawn a preset number of entities as services distributed across the assigned <c>EntityShard</c>s.
    /// </summary>
    public class StaticMultiServiceShardingStrategy : IShardingStrategy
    {
        public readonly int NumServices;

        public StaticMultiServiceShardingStrategy(int numServices)
        {
            NumServices = numServices;
        }

        /// <inheritdoc />
        public EntityShardId ResolveShardId(EntityId entityId)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            ClusterConfig clusterConfig = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>().ClusterConfig;
            int           numShards     = clusterConfig.GetNodeCountForEntityKind(entityId.Kind);
#pragma warning restore CS0618 // Type or member is obsolete

            if (numShards <= 0)
                throw new ArgumentException($"Invalid amount ({numShards}) of shard instances defined for {entityId.Kind} shard in ClusterConfig. There must be at least one instance.");
            if (entityId.Value >= (ulong)NumServices)
                throw new ArgumentException($"Invalid EntityId.Value ({entityId.Value}). Must be less than NumServices ({NumServices}).");

            int linearShardIndex = (int)(entityId.Value % (uint)numShards);
            clusterConfig.GetNodeSetAndNodeForLinearShardIndex(entityId.Kind, linearShardIndex, out int nodeSetIndex, out int nodeIndex);

            return new EntityShardId(entityId.Kind, nodeSetIndex, nodeIndex);
        }

        /// <inheritdoc />
        public IReadOnlyList<EntityId> GetAutoSpawnEntities(EntityShardId shardId)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            ClusterConfig  clusterConfig     = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>().ClusterConfig;
            int            linearShardIndex  = clusterConfig.GetLinearShardIndexForNodeSetAndNode(shardId.Kind, shardId.NodeSetIndex, shardId.NodeIndex);
            int            numShards         = clusterConfig.GetNodeCountForEntityKind(shardId.Kind);
#pragma warning restore CS0618 // Type or member is obsolete

            List<EntityId> autoSpawnEntities = new List<EntityId>();
            int            serviceIndex      = NumServices - linearShardIndex;

            while (serviceIndex > 0)
            {
                autoSpawnEntities.Add(EntityId.Create(shardId.Kind, (uint)(NumServices - serviceIndex)));
                serviceIndex -= numShards;
            }

            return autoSpawnEntities;
        }
    }

    /// <summary>
    /// Automatically spawns an entity on each <c>EntityShard</c> matching the <c>EntityKind</c>.
    /// <para>
    /// The EntityIds in this strategy are encoding <see cref="EntityShardId"/> into the <see cref="EntityId.Value"/>. This
    /// allows trivial mapping between <see cref="EntityId"/> and <see cref="EntityShardId"/>.
    /// </para>
    /// </summary>
    public class DynamicServiceShardingStrategy : IShardingStrategy
    {
        const int   NodeSetIndexBits  = 6;                                          // Number of bits to reserve for nodeSetIndex
        const int   NodeSetIndexShift = EntityId.NumValueBits - NodeSetIndexBits;   // Use highest N bits for nodeSetIndex
        const uint  NodeSetIndexMask  = (1 << NodeSetIndexBits) - 1;                // Mask for extracting nodeSetIndex
        const ulong NodeIndexMask     = (1ul << 12) - 1;                            // Mask for extracting nodeIndex

        /// <summary>
        /// Equal to <see cref="EntityShardId.MaxNodeSetIndex"/>.
        /// </summary>
        public static int MaxNodeSetIndex => (int)NodeSetIndexMask;

        /// <summary>
        /// Equal to <see cref="EntityShardId.MaxNodeIndex"/>.
        /// </summary>
        public static ulong MaxNodeIndex => NodeIndexMask;

        public DynamicServiceShardingStrategy() { }

        public EntityShardId ResolveShardId(EntityId entityId)
        {
            // Bits in unused region must be 0
            if ((entityId.Value & ~((((ulong)NodeSetIndexMask) << NodeSetIndexShift) | NodeIndexMask)) != 0)
                return default;
            return new EntityShardId(entityId.Kind, (int)((entityId.Value >> NodeSetIndexShift) & NodeSetIndexMask), (int)(entityId.Value & NodeIndexMask));
        }

        public IReadOnlyList<EntityId> GetAutoSpawnEntities(EntityShardId shardId)
        {
            // Return the EntityId for the shard.
            return new EntityId[] { CreatePlacedEntityId(shardId) };
        }

        /// <summary>
        /// Computes the EntityId of the service entity on the given shard, i.e. a given node and a given given kind.
        /// </summary>
        /// <param name="shardId">The shard on which the service is running on.</param>
        public static EntityId CreatePlacedEntityId(EntityShardId shardId)
        {
            if (shardId.NodeSetIndex < 0 || shardId.NodeSetIndex > NodeSetIndexMask)
                throw new ArgumentException($"Invalid nodeSetIndex {shardId.NodeSetIndex} for {shardId.Kind}, must be between 0 and {NodeSetIndexMask}");
            if (shardId.NodeIndex < 0 || (ulong)shardId.NodeIndex > NodeIndexMask)
                throw new ArgumentException($"Invalid nodeIndex {shardId.NodeIndex} for {shardId.Kind}, must be between 0 and {NodeIndexMask}");

            ulong value = ((ulong)(uint)shardId.NodeSetIndex << NodeSetIndexShift) | (uint)shardId.NodeIndex;
            return EntityId.Create(shardId.Kind, value);
        }

        public static EntityId CreatePlacedEntityId(EntityKind kind, int nodeSetIndex, int nodeIndex) =>
            CreatePlacedEntityId(new EntityShardId(kind, nodeSetIndex, nodeIndex));
    }

    /// <summary>
    /// Shards entities based on routing decision payload encoded on EntityId. This allows for setting routing rules at runtime, but
    /// requires that all <see cref="EntityId"/>s are created with <see cref="CreateEntityId"/>.
    /// </summary>
    public class ManualShardingStrategy : IShardingStrategy
    {
        const int   NodeSetIndexBits  = 6;                                     // Number of bits to reserve for nodeSetIndex
        const int   NodeSetIndexShift = EntityId.NumValueBits - NodeSetIndexBits; // Use highest N bits for nodeSetIndex
        const uint  NodeSetIndexMask  = (1 << NodeSetIndexBits) - 1;           // Mask for extracting nodeSetIndex
        const int   NodeIndexBits     = 12;                                    // Number of bits to reserve for nodeIndex
        const int   NodeIndexShift    = NodeSetIndexShift - NodeIndexBits;     // Use the next highest N bits for nodeIndex
        const uint  NodeIndexMask     = (1 << NodeIndexBits) - 1;              // Mask for extracting nodeIndex
        const ulong ValueMask         = (1ul << NodeIndexShift) - 1;           // Mask for extracting runningId

        public static ulong MaxValue => ValueMask;

        /// <summary>
        /// Equal to <see cref="EntityShardId.MaxNodeSetIndex"/>.
        /// </summary>
        public static int MaxNodeSetIndex => (int)NodeSetIndexMask;

        /// <summary>
        /// Equal to <see cref="EntityShardId.MaxNodeIndex"/>.
        /// </summary>
        public static int MaxNodeIndex => (int)NodeIndexMask;

        public ManualShardingStrategy()
        {
        }

        public EntityShardId ResolveShardId(EntityId entityId)
        {
            return new EntityShardId(entityId.Kind, (int)((entityId.Value >> NodeSetIndexShift) & NodeSetIndexMask), (int)((entityId.Value >> NodeIndexShift) & NodeIndexMask));
        }

        /// <summary>
        /// Creates the EntityId of an entity running on the given shard, i.e. a given node and a given given kind.
        /// </summary>
        /// <param name="shardId">The shard on which the entity is running on.</param>
        /// <param name="runningId">
        /// The index of the entity on the given shard. Varying the value allows you to generate different EntityIds
        /// on the same shard. Must be less or equal to <see cref="MaxValue"/>.
        /// </param>
        public static EntityId CreateEntityId(EntityShardId shardId, ulong runningId)
        {
            if (shardId.NodeSetIndex < 0 || shardId.NodeSetIndex > NodeSetIndexMask)
                throw new ArgumentException($"Invalid nodeSetIndex {shardId.NodeSetIndex} for {shardId.Kind}, must be between 0 and {NodeSetIndexMask}");
            if (shardId.NodeIndex < 0 || shardId.NodeIndex > NodeIndexMask)
                throw new ArgumentException($"Invalid nodeIndex {shardId.NodeIndex} for {shardId.Kind}, must be between 0 and {NodeIndexMask}");
            if (runningId < 0 || runningId > ValueMask)
                throw new ArgumentException($"Invalid runningId {runningId} for {shardId.Kind}, must be between 0 and {ValueMask}");

            ulong value = ((ulong)(uint)shardId.NodeSetIndex << NodeSetIndexShift) | ((ulong)(uint)shardId.NodeIndex << NodeIndexShift) | runningId;
            return EntityId.Create(shardId.Kind, value);
        }

        public IReadOnlyList<EntityId> GetAutoSpawnEntities(EntityShardId shardId) => null;
    }

    /// <summary>
    /// Helper class for creating various <see cref="IShardingStrategy"/> instances.
    /// </summary>
    public static class ShardingStrategies
    {
        /// <summary>
        /// Create a <see cref="StaticModuloShardingStrategy"/> for a given <see cref="EntityKind"/>.
        /// <inheritdoc cref="StaticModuloShardingStrategy"/>
        /// </summary>
        public static StaticModuloShardingStrategy CreateStaticSharded() => new StaticModuloShardingStrategy();

        /// <summary>
        /// Create a service strategy. An instance of the entity is automatically spawned on each static pod/node
        /// that matches the <c>EntityKind</c>.
        /// </summary>
        /// <remarks><see cref="CreateDynamicService"/> should be used in most cases instead of <see cref="CreateStaticService"/>, you should only use <see cref="CreateStaticService"/> if you are using <see cref="Metaplay.Cloud.Entity.EntityActor.GetAssociatedServiceEntityId"/> to locate the entity.</remarks>
        /// <returns></returns>
        public static StaticServiceShardingStrategy CreateStaticService() => new StaticServiceShardingStrategy(isSingleton: false);

        /// <summary>
        /// Create a service strategy. An instance of the entity is automatically spawned on each node (even dynamically scaled)
        /// that matches the <c>EntityKind</c>.
        /// </summary>
        /// <returns></returns>
        public static DynamicServiceShardingStrategy CreateDynamicService() => new DynamicServiceShardingStrategy();

        /// <summary>
        /// Global singleton service. One instance of the service is automatically started.
        /// </summary>
        public static StaticServiceShardingStrategy CreateSingletonService() => new StaticServiceShardingStrategy(isSingleton: true);

        /// <summary>
        /// Create a multi-service strategy. A preset number of instances of the entity are automatically spawned distributed
        /// on pods/nodes that match the <c>EntityKind</c>.
        /// </summary>
        public static IShardingStrategy CreateMultiService(int numServices) => new StaticMultiServiceShardingStrategy(numServices);

        /// <summary>
        /// Create <see cref="ManualShardingStrategy"/>. The <c>EntityIds</c> are manually generated with the
        /// <c>NodeSet Index</c> and <c>Node Index</c> embedded into the <c>EntityId</c> such that they can be extracted back
        /// from the <c>EntityId</c>.
        /// </summary>
        /// <returns></returns>
        public static ManualShardingStrategy CreateManual() => new ManualShardingStrategy();
    }
}
