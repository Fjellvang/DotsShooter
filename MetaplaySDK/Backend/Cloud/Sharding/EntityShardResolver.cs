// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;

namespace Metaplay.Cloud.Sharding
{
    /// <summary>
    /// Utility for resolving current ActorRefs of EntityShard, given the <see cref="ClusterConnectionManager.ClusterChangedEvent"/>s.
    /// The resolver is shared across the actor system, avoiding duplicated work.
    /// </summary>
    public class EntityShardResolver : IExtension
    {
        class Provider : ExtensionIdProvider<EntityShardResolver>
        {
            public override EntityShardResolver CreateExtension(ExtendedActorSystem system)
            {
                return new EntityShardResolver();
            }
        }

        readonly object                             _lock           = new object();
        readonly ClusterConfig                      _clusterConfig;
        long                                        _latestUpdateId = long.MinValue;

        /// <summary>
        /// Immutable, readable from any thread. Updating (and publishing) is protected with _lock.
        /// </summary>
        volatile MetaDictionary<EntityShardId, IActorRef> _shardActors;

        EntityShardResolver()
        {
            _clusterConfig = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>().ClusterConfig;
        }

        public static EntityShardResolver Get(ActorSystem system)
        {
            return system.WithExtension<EntityShardResolver, Provider>();
        }

        /// <summary>
        /// Applies the ClusterChanged event to the shared state. After this call, calls to <see cref="TryGetShardActor(EntityShardId)"/>
        /// are guaranteed to return shards of that update or any newer update.
        /// </summary>
        public void OnClusterChangedEvent(ClusterConnectionManager.ClusterChangedEvent clusterChangedEvent)
        {
            lock (_lock)
            {
                // Many call sites may independently report the same event. Skip if we have already done this.
                // We must also skip older events, as some caller might be behind in reading the updates.
                if (clusterChangedEvent.EventTimestampId <= _latestUpdateId)
                    return;

                // Rebuild the lookup. By rebuilding and not mutating we can avoid locking on read ops.
                // Speculate the size to be the same as previously
                MetaDictionary<EntityShardId, IActorRef> newActors = new MetaDictionary<EntityShardId, IActorRef>(capacity: _shardActors?.Count ?? 0);
                foreach (ClusterConnectionManager.ClusterChangedEvent.ClusterMember member in clusterChangedEvent.Members)
                {
                    if (!member.IsConnected)
                        continue;
                    foreach ((EntityKind kind, IActorRef actor) in member.Info.EntityShardActors)
                    {
                        if (_clusterConfig.ResolveNodeShardId(kind, member.Address, out EntityShardId entityShardId))
                            newActors.Add(entityShardId, actor);
                    }
                }

                _latestUpdateId = clusterChangedEvent.EventTimestampId;
                _shardActors = newActors;
            }
        }

        /// <summary>
        /// Returns the EntityShard actor ref for the given entity shard. Returns null if no such
        /// entity shard exists.
        /// </summary>
        public IActorRef TryGetShardActor(EntityShardId shardId)
        {
            return _shardActors?.GetValueOrDefault(shardId);
        }
    }
}
