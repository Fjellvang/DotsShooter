// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Akka.Configuration;
using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity;
using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Cloud.Sharding
{
    public class EntityShardingCoordinator : MetaReceiveActor
    {
        public class CreateShard
        {
            public readonly EntityShardConfig ShardConfig;

            public CreateShard(EntityShardConfig shardConfig)
            {
                ShardConfig = shardConfig;
            }
        }
        public class CreateShardResponse
        {
            public readonly IActorRef   ShardActor;
            public readonly string      ErrorString;

            public CreateShardResponse(IActorRef shardActor, string errorString)
            {
                ShardActor = shardActor;
                ErrorString = errorString;
            }
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(ex =>
            {
                string kindName = Sender.Path.Elements[Sender.Path.Elements.Count - 1];

                string stackTrace = EntityStackTraceUtil.ExceptionToEntityStackTrace(ex);
                _log.Error("EntityShard {EntityKind} crashed due to {ExceptionType} '{ExceptionMessage}':\n{ExceptionStackTrace}", kindName, ex.GetType().Name, ex.Message, stackTrace);

                // Don't auto-restart any actors
                return Directive.Stop;
            }, loggingEnabled: false);
        }

        public EntityShardingCoordinator()
        {
            Receive<CreateShard>(ReceiveCreateShard);
            Receive<ShutdownSync>(ReceiveShutdownSync);
        }

        void ReceiveCreateShard(CreateShard createShard)
        {
            // Start the EntityShard actor
            EntityShardId       shardId         = createShard.ShardConfig.Id;
            EntityConfigBase    entityConfig    = EntityConfigRegistry.Instance.GetConfig(shardId.Kind);
            string              childName       = shardId.Kind.ToString();
            Type                entityShardType = entityConfig.EntityShardType;
            Props               shardProps      = Props.Create(entityShardType, createShard.ShardConfig);
            CreateShardResponse response;

            if (Context.Child(childName) != ActorRefs.Nobody)
            {
                // The shard already exists, cannot proceed. Return failure
                response = new CreateShardResponse(null, $"Actor {childName} already exists, cannot spawn new with the same name");
            }
            else
            {
                // Create shard
                IActorRef shardActor = Context.ActorOf(shardProps, childName);
                Tell(shardActor, EntityShard.InitializeShard.Instance);
                response = new CreateShardResponse(shardActor, null);
            }

            Sender.Tell(response);
        }

        void ReceiveShutdownSync(ShutdownSync shutdown)
        {
            _self.Tell(PoisonPill.Instance);
            Sender.Tell(ShutdownComplete.Instance);
        }
    }

    /// <summary>
    /// Akka.NET extension for managing <see cref="EntityShard"/>.
    /// </summary>
    public class EntitySharding : IExtension
    {
        ExtendedActorSystem                             _actorSystem;
        IActorRef                                       _shardingCoordinator;

        public static Config DefaultConfiguration()
        {
            // \note Not using HOCON configuration
            return new Config();
        }

        public static EntitySharding Get(ActorSystem system)
        {
            return system.WithExtension<EntitySharding, EntityShardingProvider>();
        }

        public EntitySharding(ExtendedActorSystem system)
        {
            _actorSystem = system ?? throw new ArgumentNullException(nameof(system));
            _shardingCoordinator = system.ActorOf(Props.Create<EntityShardingCoordinator>(), "shard");
        }

        /// <summary>
        /// Create the <see cref="EntityShard"/> instance, but don't yet initialize it. Throws on failure.
        /// </summary>
        /// <returns>The actorref to the create shard actor</returns>
        public async Task<IActorRef> CreateShardAsync(EntityShardConfig shardConfig)
        {
            // Spawn the EntityShard actor, initialization happens later
            EntityShardingCoordinator.CreateShardResponse response = await _shardingCoordinator.Ask<EntityShardingCoordinator.CreateShardResponse>(new EntityShardingCoordinator.CreateShard(shardConfig));
            if (response.ShardActor != null)
                return response.ShardActor;
            throw new InvalidOperationException(response.ErrorString);
        }

        public async Task ShutdownAsync(TimeSpan timeout)
        {
            await _shardingCoordinator.Ask<ShutdownComplete>(ShutdownSync.Instance, timeout);
        }
    }
}
