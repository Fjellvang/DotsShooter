// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Entity.PubSub;
using Metaplay.Core;
using Metaplay.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Metaplay.Cloud.Entity.PubSub.EntitySubscribeE2SUnsubscribeRequest;

namespace Metaplay.Cloud.Sharding
{
    public partial class EntityShard
    {
        protected partial class EntityState
        {
            public class Subscription
            {
                public readonly EntityShardId                                                PeerShardId;
                public readonly EntityId                                                     PeerEntityId;
                public readonly int                                                          LocalChannelId;
                public TaskCompletionSource<EntitySubscribeE2SSubscribeRequest.SubscribeAck> OpeningPromise;
                public bool                                                                  TargetHasAcceptedSubscriber;
                public int                                                                   PeerIncarnationId;
                public int                                                                   PeerPubSubId;
                public TaskCompletionSource<UnsubscribeResult>                               PendingUnsubscribe;

                public Subscription(EntityShardId peerShardId, EntityId peerEntityId, int localChannelId, TaskCompletionSource<EntitySubscribeE2SSubscribeRequest.SubscribeAck> openingPromise)
                {
                    PeerShardId = peerShardId;
                    PeerEntityId = peerEntityId;
                    LocalChannelId = localChannelId;
                    OpeningPromise = openingPromise;
                }
            }
            public class Subscriber
            {
                public readonly IActorRef       PeerShard;
                public readonly EntityShardId   PeerShardId;
                public readonly EntityId        PeerEntityId;
                public readonly int             PeerIncarnationId;
                public readonly int             PeerPubSubId;
                public bool                     TargetHasAcceptedSubscriber;
                public int                      LocalChannelId;

                public Subscriber(IActorRef peerShard, EntityShardId peerShardId, EntityId peerEntityId, int peerIncarnationId, int peerPubSubId)
                {
                    PeerShard = peerShard;
                    PeerShardId = peerShardId;
                    PeerEntityId = peerEntityId;
                    PeerIncarnationId = peerIncarnationId;
                    PeerPubSubId = peerPubSubId;
                }
            }

            public Dictionary<int, Subscription> Subscriptions = new Dictionary<int, Subscription>(); // pubSubId -> State
            public Dictionary<int, Subscriber>   Subscribers   = new Dictionary<int, Subscriber>(); // pubSubId -> State
        }

        class LocalWatcherSet
        {
            /// <summary>
            /// Number of watches per local entity.
            /// These entities will be notified when the watched entity terminates.
            /// </summary>
            public readonly Dictionary<EntityId, int> NotifiedEntities = new Dictionary<EntityId, int>();

            /// <summary>
            /// The shard of the watched entity.
            /// Note that that notified entities are always local.
            /// </summary>
            public readonly EntityShardId WatchedShardId;

            public LocalWatcherSet(EntityShardId watchedShardId)
            {
                WatchedShardId = watchedShardId;
            }
        }

        /// <summary>
        /// Mapping from any watched EntityId to all local entities that are watching it.
        /// The entity in key is notified if any of the entities in the value is terminated.
        /// </summary>
        readonly Dictionary<EntityId, LocalWatcherSet> _localWatchers = new Dictionary<EntityId, LocalWatcherSet>();

        int _entityPubSubRunningId = 1;
        PeriodThrottle _pubsubConsistencyErrorThrottle = new PeriodThrottle(TimeSpan.FromSeconds(5), maxEventsPerDuration: 5, DateTime.UtcNow);

        void RegisterPubSubHandlers()
        {
            Receive<EntitySubscribeE2SSubscribeRequest>(ReceiveEntitySubscribeE2SSubscribeRequest);
            Receive<EntitySubscribeS2SSubscribeRequest>(ReceiveEntitySubscribeS2SSubscribeRequest);
            Receive<EntitySubscribeS2ESubscribeResponse>(ReceiveEntitySubscribeS2ESubscribeResponse);
            Receive<EntitySubscribeS2SSubscribeResponse>(ReceiveEntitySubscribeS2SSubscribeResponse);

            Receive<EntitySubscribeE2SUnsubscribeRequest>(ReceiveEntitySubscribeE2SUnsubscribeRequest);
            Receive<EntitySubscribeS2SUnsubscribeRequest>(ReceiveEntitySubscribeS2SUnsubscribeRequest);
            Receive<EntitySubscribeS2EUnsubscribeResponse>(ReceiveEntitySubscribeS2EUnsubscribeResponse);
            Receive<EntitySubscribeS2SUnsubscribeResponse>(ReceiveEntitySubscribeS2SUnsubscribeResponse);

            Receive<EntitySubscribeE2SKickMessage>(ReceiveEntitySubscribeE2SKickMessage);
            Receive<EntitySubscribeS2SKickMessage>(ReceiveEntitySubscribeS2SKickMessage);
        }

        void ReceiveEntitySubscribeE2SSubscribeRequest(EntitySubscribeE2SSubscribeRequest request)
        {
            if (!_entityStates.TryGetValue(request.SubscriberEntityId, out EntityState entity))
            {
                _log.Warning("Got EntitySubscribeE2SSubscribeRequest for unknown entity: {EntityId}", request.SubscriberEntityId);
                return;
            }

            // Resolve the destination shard.
            ShardActorResolutionResult targetShard = TryGetShardForEntity(request.TargetEntityId);
            if (targetShard.ShardActor == null)
            {
                // Complete request immediately
                request.ReplyPromise.SetException(new EntityUnreachableError(request.TargetEntityId));
                return;
            }

            // Create subscription state
            int pubSubId = _entityPubSubRunningId++;
            EntityState.Subscription state = new EntityState.Subscription(
                peerShardId:        targetShard.ShardId,
                peerEntityId:       request.TargetEntityId,
                localChannelId:     request.ChannelId,
                openingPromise:     request.ReplyPromise
                );
            entity.Subscriptions.Add(pubSubId, state);

            // Forward to the shard responsible for this
            Tell(targetShard.ShardActor, new EntitySubscribeS2SSubscribeRequest(
                subscriberPubSubId:      pubSubId,
                targetEntityId:          request.TargetEntityId,
                subscriberEntityId:      request.SubscriberEntityId,
                subscriberActor:         request.SubscriberActor,
                subscriberIncarnationId: entity.IncarnationId,
                topic:                   request.Topic,
                subscriberChannelId:     request.ChannelId,
                message:                 request.Message
                ));
        }

        void ReceiveEntitySubscribeS2SSubscribeRequest(EntitySubscribeS2SSubscribeRequest request)
        {
            EntityId subscriberEntityId = request.SubscriberEntityId;

            // Check target is valid.
            EntityId targetEntityId = request.TargetEntityId;
            if (TryGetShardIdForEntity(targetEntityId) != _selfShardId)
            {
                _log.Error("Invalid shard for EntitySubscribeS2SSubscribeRequest, target {TargetEntityId} not allocated for this shard", targetEntityId);
                return;
            }

            // Check the source shard is routable. A shard become routable-from in Initialize, but
            // becomes routable-to already before Initialize.
            ShardActorResolutionResult subscriberShard = TryGetShardForEntity(request.SubscriberEntityId);
            if (subscriberShard.ShardActor == null)
            {
                // We cannot route the answer to the shard traditionally. But we can reply to the Sender.
                _log.Warning("Received EntitySubscribeS2SSubscribeRequest from {FromEntityId} which is on a non-routable-to shard. Refusing.", subscriberEntityId);
                Tell(Sender, new EntitySubscribeS2SSubscribeResponse(
                    subscriberPubSubId:     request.SubscriberPubSubId,
                    targetPubSubId:         -1,
                    subscriberEntityId:     request.SubscriberEntityId,
                    subscriberIncarnationId:request.SubscriberIncarnationId,
                    targetIncarnationId:    -1,
                    targetChannelId:        -1,
                    targetEntityActor:      default,
                    reply:                  default,
                    error:                  new MetaSerialized<EntityAskExceptionBase>(new EntityUnreachableError(targetEntityId), MetaSerializationFlags.IncludeAll, null)
                    ));
                return;
            }

            EntityState entity = GetOrSpawnEntity(targetEntityId, out SpawnFailure failureReason);
            if (entity == null)
            {
                _log.Warning("Failed to spawn entity {EntityId} to receive EntitySubscribe: {FailureReason}", targetEntityId, failureReason);
                Tell(Sender, new EntitySubscribeS2SSubscribeResponse(
                    subscriberPubSubId:     request.SubscriberPubSubId,
                    targetPubSubId:         -1,
                    subscriberEntityId:     request.SubscriberEntityId,
                    subscriberIncarnationId:request.SubscriberIncarnationId,
                    targetIncarnationId:    -1,
                    targetChannelId:        -1,
                    targetEntityActor:      default,
                    reply:                  default,
                    error:                  new MetaSerialized<EntityAskExceptionBase>(new EntityUnreachableError(targetEntityId), MetaSerializationFlags.IncludeAll, null)
                    ));
                return;
            }

            // Update internal state
            int pubSubId = _entityPubSubRunningId++;
            EntityState.Subscriber state = new EntityState.Subscriber(
                peerShard:          Sender,
                peerShardId:        subscriberShard.ShardId,
                peerEntityId:       subscriberEntityId,
                peerIncarnationId:  request.SubscriberIncarnationId,
                peerPubSubId:       request.SubscriberPubSubId
                );
            entity.Subscribers.Add(pubSubId, state);

            // Watch the subscribing entity. If is crashes, we must inform the target entity
            // since it may have accepted the subscription. Extra notifications in case the
            // subscription was rejected do not hurt.
            RegisterPubsubWatch(watchedShardId: subscriberShard.ShardId, watchedEntity: subscriberEntityId, notifiedLocalEntity: targetEntityId);

            // Forward to the entity
            EntitySubscribeS2ESubscribeRequest entityRequest = new EntitySubscribeS2ESubscribeRequest(
                pubSubId:                   pubSubId,
                subscriberEntityId:         subscriberEntityId,
                subscriberActor:            request.SubscriberActor,
                subscriberIncarnationId:    request.SubscriberIncarnationId,
                topic:                      request.Topic,
                subscriberChannelId:        request.SubscriberChannelId,
                message:                    request.Message
                );

            // Deliver message to the entity (may also buffer)
            RouteMessageToLocalEntity(entity, entityRequest);
        }

        void ReceiveEntitySubscribeS2ESubscribeResponse(EntitySubscribeS2ESubscribeResponse response)
        {
            if (!_entityStates.TryGetValue(response.SubscribedEntityId, out EntityState entity))
            {
                _log.Warning("Got EntitySubscribeS2ESubscribeResponse for unknown entity: {EntityId}", response.SubscribedEntityId);
                return;
            }

            if (!entity.Subscribers.TryGetValue(response.PubSubId, out EntityState.Subscriber subscriber))
            {
                // No (pending) subscriber. This can happen if the subscriber has been lost. In that case,
                // the entity has been notified of the entity terminating.
                // A simple example: Entity A subscribes to B, but B takes too long and A timeouts and
                // crashes. When B replies, this subscription has already been removed.
                _log.Debug("Entity {EntityId} completed a subscription handshake but there was no such pending subscription (did subscriber die?).", response.SubscribedEntityId);
                return;
            }

            if (response.Error.IsEmpty)
            {
                // If subscription was success, mark subscription as ongoing
                subscriber.TargetHasAcceptedSubscriber = true;
                subscriber.LocalChannelId = response.SubscribedChannelId;
            }
            else
            {
                // If this was a failure, remove the subscriber state.
                entity.Subscribers.Remove(response.PubSubId);

                // No longer interested in watching the source entity
                UnregisterPubsubWatch(watchedEntity: subscriber.PeerEntityId, notifiedLocalEntity: response.SubscribedEntityId);
            }

            Tell(subscriber.PeerShard, new EntitySubscribeS2SSubscribeResponse(
                subscriberPubSubId:     subscriber.PeerPubSubId,
                targetPubSubId:         response.PubSubId,
                subscriberEntityId:     subscriber.PeerEntityId,
                subscriberIncarnationId:subscriber.PeerIncarnationId,
                targetIncarnationId:    entity.IncarnationId,
                targetChannelId:        response.SubscribedChannelId,
                targetEntityActor:      response.SubscribedActor,
                reply:                  response.Reply,
                error:                  response.Error
                ));
        }

        void ReceiveEntitySubscribeS2SSubscribeResponse(EntitySubscribeS2SSubscribeResponse response)
        {
            if (!_entityStates.TryGetValue(response.SubscriberEntityId, out EntityState entity))
            {
                // Source entity has died. The target entity has been informed of the termination. Nothing to do.
                return;
            }
            if (entity.IncarnationId != response.SubscriberIncarnationId)
            {
                // Source entity has died and restarted. The target entity has been informed of the termination. Nothing to do.
                return;
            }
            if (!entity.Subscriptions.TryGetValue(response.SubscriberPubSubId, out EntityState.Subscription subscription))
            {
                // Should not happen
                _log.Warning("Entity {EntityId} received EntitySubscribeS2SSubscribeResponse response, but there is no such pubsub request", response.SubscriberEntityId);
                return;
            }

            if (response.Error.IsEmpty)
            {
                EntitySubscribeE2SSubscribeRequest.SubscribeAck ack = new EntitySubscribeE2SSubscribeRequest.SubscribeAck(
                    targetActor:    response.TargetEntityActor,
                    incarnationId:  response.TargetIncarnationId,
                    pubSubId:       response.SubscriberPubSubId,
                    channelId:      response.TargetChannelId,
                    response:       response.Reply);
                subscription.OpeningPromise.SetResult(ack);
                subscription.OpeningPromise = null;
                subscription.TargetHasAcceptedSubscriber = true;
                subscription.PeerIncarnationId = response.TargetIncarnationId;
                subscription.PeerPubSubId = response.TargetPubSubId;

                // Begin listening for subscription target crashes. Note that if the target entity:
                //  1) crashes before the request was delivered, the request is still in buffer and all is good
                //  2) crashes in init when request was buffered, the peer shard will send EntitySubscribeS2SSubscribeResponse as expected
                //  3) crashes when request was in entity inbox, the peer shard will send EntitySubscribeS2SSubscribeResponse as expected
                RegisterPubsubWatch(watchedShardId: subscription.PeerShardId, watchedEntity: subscription.PeerEntityId, notifiedLocalEntity: response.SubscriberEntityId);
            }
            else
            {
                entity.Subscriptions.Remove(response.SubscriberPubSubId);

                EntityAskExceptionBase ex = response.Error.Deserialize(null, null);
                subscription.OpeningPromise.SetException(ex);
            }
        }

        void ReceiveEntitySubscribeE2SUnsubscribeRequest(EntitySubscribeE2SUnsubscribeRequest request)
        {
            if (!_entityStates.TryGetValue(request.SubscriberEntityId, out EntityState entity))
            {
                _log.Warning("Got EntitySubscribeE2SUnsubscribeRequest for unknown entity: {EntityId}", request.SubscriberEntityId);
                return;
            }
            if (!entity.Subscriptions.TryGetValue(request.PubSubId, out EntityState.Subscription subscription))
            {
                // Already kicked. Reply immediately.
                request.ReplyPromise.SetResult(EntitySubscribeE2SUnsubscribeRequest.UnsubscribeResult.UnknownSubscriber);
                return;
            }

            // Resolve the destination shard.
            ShardActorResolutionResult targetShard = TryGetShardForEntity(subscription.PeerEntityId);
            if (targetShard.ShardActor == null)
            {
                // Cannot route. Complete request immediately
                request.ReplyPromise.SetResult(EntitySubscribeE2SUnsubscribeRequest.UnsubscribeResult.UnknownSubscriber);
                return;
            }

            // Mark subscription as to-be-unsubscribed.
            // Any WatchedEntityTerminated are converted to replies.
            subscription.PendingUnsubscribe = request.ReplyPromise;

            // Forward to the target shard.
            Tell(targetShard.ShardActor, new EntitySubscribeS2SUnsubscribeRequest(
                targetEntityId:         subscription.PeerEntityId,
                targetIncarnationId:    subscription.PeerIncarnationId,
                targetPubSubId:         subscription.PeerPubSubId,
                goodbyeMessage:         request.GoodbyeMessage));
        }

        void ReceiveEntitySubscribeS2SUnsubscribeRequest(EntitySubscribeS2SUnsubscribeRequest request)
        {
            if (!_entityStates.TryGetValue(request.TargetEntityId, out EntityState entity))
            {
                // Target entity has died. The subscriber entity has been informed of the termination. Nothing to do.
                return;
            }
            if (entity.IncarnationId != request.TargetIncarnationId)
            {
                // Target entity has died and restarted. The subscriber entity has been informed of the termination. Nothing to do.
                return;
            }
            if (!entity.Subscribers.TryGetValue(request.TargetPubSubId, out EntityState.Subscriber subscriber))
            {
                // Should not happen
                _log.Warning("Entity {EntityId} received EntitySubscribeS2SUnsubscribeRequest response, but there is no such pubsub request", request.TargetEntityId);
                return;
            }

            // \note: No need to check state. If entity is shuts down, we will send the termination messages to the initiator
            //        shard, and they will handle the termination as a reply.
            RouteMessageToLocalEntity(entity, new EntitySubscribeS2EUnsubscribeRequest(
                inChannelId:    subscriber.LocalChannelId,
                pubSubId:       request.TargetPubSubId,
                subscriberId:   subscriber.PeerEntityId,
                goodbyeMessage: request.GoodbyeMessage));
        }

        void ReceiveEntitySubscribeS2EUnsubscribeResponse(EntitySubscribeS2EUnsubscribeResponse response)
        {
            if (!_entityStates.TryGetValue(response.SubscribedEntityId, out EntityState entity))
            {
                _log.Warning("Got EntitySubscribeS2EUnsubscribeResponse for unknown entity: {EntityId}", response.SubscribedEntityId);
                return;
            }
            if (!entity.Subscribers.Remove(response.PubSubId, out EntityState.Subscriber subscriber))
            {
                // No such subscriber. This can happen if the subscriber has been lost or kicked. In both
                // cases, the subscriber has been informed.
                return;
            }

            // The entity no longer watches the removed subscriber
            UnregisterPubsubWatch(subscriber.PeerEntityId, entity.EntityId);

            Tell(subscriber.PeerShard, new EntitySubscribeS2SUnsubscribeResponse(
                subscriberEntityId:         subscriber.PeerEntityId,
                subscriberIncarnationId:    subscriber.PeerIncarnationId,
                subscriberPubSubId:         subscriber.PeerPubSubId,
                wasSuccess:                 response.WasSuccess));
        }

        void ReceiveEntitySubscribeS2SUnsubscribeResponse(EntitySubscribeS2SUnsubscribeResponse response)
        {
            if (!_entityStates.TryGetValue(response.SubscriberEntityId, out EntityState entity))
            {
                // Subscriber has died already. Nothing to do.
                return;
            }
            if (entity.IncarnationId != response.SubscriberIncarnationId)
            {
                // Subscriber has died and restarted already. Nothing to do.
                return;
            }
            if (!entity.Subscriptions.Remove(response.SubscriberPubSubId, out EntityState.Subscription subscription))
            {
                // Should not happen
                _log.Warning("Entity {EntityId} received EntitySubscribeS2SUnsubscribeResponse response, but there is no such pubsub request", response.SubscriberEntityId);
                return;
            }

            // The entity no longer watches the subscribed
            UnregisterPubsubWatch(subscription.PeerEntityId, entity.EntityId);

            // Complete the request.
            subscription.PendingUnsubscribe.SetResult(response.WasSuccess ? UnsubscribeResult.Success : UnsubscribeResult.UnknownSubscriber);
        }

        void ReceiveEntitySubscribeE2SKickMessage(EntitySubscribeE2SKickMessage kicked)
        {
            if (!_entityStates.TryGetValue(kicked.KickerEntityId, out EntityState entity))
            {
                _log.Warning("Got EntitySubscribeE2SKickMessage for unknown entity: {EntityId}", kicked.KickerEntityId);
                return;
            }
            if (!entity.Subscribers.Remove(kicked.PubSubId, out EntityState.Subscriber subscriber))
            {
                // Already gone. Kicking done.
                return;
            }

            // No longer interested in the subscriber state
            UnregisterPubsubWatch(watchedEntity: subscriber.PeerEntityId, notifiedLocalEntity: entity.EntityId);

            // Inform to the target shard.
            Tell(subscriber.PeerShard, new EntitySubscribeS2SKickMessage(
                targetEntityId:         subscriber.PeerEntityId,
                targetIncarnationId:    subscriber.PeerIncarnationId,
                targetPubSubId:         subscriber.PeerPubSubId,
                goodbyeMessage:         kicked.GoodbyeMessage));
        }

        void ReceiveEntitySubscribeS2SKickMessage(EntitySubscribeS2SKickMessage kicked)
        {
            if (!_entityStates.TryGetValue(kicked.TargetEntityId, out EntityState entity))
            {
                // Kicked entity has died already. Nothing to do.
                return;
            }
            if (entity.IncarnationId != kicked.TargetIncarnationId)
            {
                // Kicked entity has died and restarted already. Nothing to do.
                return;
            }
            if (!entity.Subscriptions.Remove(kicked.TargetPubSubId, out EntityState.Subscription subscription))
            {
                // Should not happen
                _log.Warning("Entity {EntityId} received ReceiveEntitySubscribeS2SKickMessage, but there is no such pubsub request", kicked.TargetEntityId);
                return;
            }

            // No longer interested in the status of the subscriber
            // \note: Subscription has been established before we can get kick, so no need to check if watch was added
            UnregisterPubsubWatch(watchedEntity: subscription.PeerEntityId, notifiedLocalEntity: entity.EntityId);

            // Inform entity.
            // \note: No need to check state. If entity is shuts down, we will clear the message from PendingMessages
            RouteMessageToLocalEntity(entity, new EntitySubscribeS2EKickMessage(
                channelId:      subscription.LocalChannelId,
                goodbyeMessage: kicked.GoodbyeMessage));

            // If entity was unsubscribing, fail it. The subscribed entity will not be replying.
            if (subscription.PendingUnsubscribe != null)
                subscription.PendingUnsubscribe.SetResult(UnsubscribeResult.UnknownSubscriber);
        }

        void ReplyToAllPubsubRequestsOnActorTerminate(EntityState entityState, EntityAskExceptionBase error)
        {
            foreach (object routed in entityState.PendingMessages)
            {
                if (routed is EntitySubscribeS2ESubscribeRequest request)
                {
                    // End all pending subscribers and inform subscriber of the failure.
                    if (entityState.Subscribers.Remove(request.PubSubId, out EntityState.Subscriber subscriber))
                    {
                        Tell(subscriber.PeerShard, new EntitySubscribeS2SSubscribeResponse(
                            subscriberPubSubId:     subscriber.PeerPubSubId,
                            targetPubSubId:         -1,
                            subscriberEntityId:     subscriber.PeerEntityId,
                            subscriberIncarnationId:subscriber.PeerIncarnationId,
                            targetIncarnationId:    -1,
                            targetChannelId:        -1,
                            targetEntityActor:      default,
                            reply:                  default,
                            error:                  new MetaSerialized<EntityAskExceptionBase>(error, MetaSerializationFlags.IncludeAll, null)
                            ));

                        // When subscriber was added, we registered the entity as a listener of the subscriber entity. Remove it.
                        UnregisterPubsubWatch(watchedEntity: subscriber.PeerEntityId, notifiedLocalEntity: entityState.EntityId);
                    }
                }
            }
        }

        void RemoveWeakPubsubMessagesOnActorTerminate(EntityState entityState)
        {
            entityState.PendingMessages.RemoveAll((message) =>
            {
                switch (message)
                {
                    case WatchedEntityTerminated:
                        // If the actor is dead, no point informing watches.
                        return true;

                    case EntitySubscribeS2EUnsubscribeRequest:
                        // If the actor is dead, no point informing of unsubscribes.
                        // WatchedEntityTerminated is automatically converted to reply
                        return true;

                    case EntitySubscribeS2EKickMessage:
                        // No point informing of being kicked.
                        return true;
                }
                return false;
            });
        }

        /// <remarks>
        /// This method does not perform consistency checks. This allows this method to be
        /// used in (temporarily) inconsistent state.
        /// </remarks>
        void InformTerminatedEntityPubSubListeners(EntityState entityState)
        {
            // Skip if no pubsub listeners
            if (entityState.Subscriptions.Count == 0 && entityState.Subscribers.Count == 0)
                return;

            WatchedEntityTerminated terminated = new WatchedEntityTerminated(entityState.EntityId, entityState.IncarnationId);

            // Collect per-shard list of local entities and remote shards that should be notified
            HashSet<EntityShardId> notifyShards = new HashSet<EntityShardId>();
            HashSet<EntityId> localEntitiesToNotify = new HashSet<EntityId>();

            // Inform all subscription listener local entities and remote shards. It doesn't matter
            // if the subscription has been replied to or not. If target hasn't replied to it yet,
            // it still might and if it does accept the subscription, we'd need to inform it anyway.
            HashSet<int> subscriptionsToRemove = new HashSet<int>();
            foreach ((int pubsubId, EntityState.Subscription subscription) in entityState.Subscriptions)
            {
                subscriptionsToRemove.Add(pubsubId);

                EntityShardId shardId = subscription.PeerShardId;
                if (shardId != _selfShardId)
                {
                    // Send WatchedEntityTerminated to the remote shard
                    notifyShards.Add(shardId);
                }
                else
                {
                    // If there is a pending unsubscibe (from local to local entity), be consistent
                    // and convert WatchedEntityTerminated to reply.
                    if (subscription.PendingUnsubscribe != null)
                        subscription.PendingUnsubscribe.SetResult(UnsubscribeResult.UnknownSubscriber);
                    else
                        localEntitiesToNotify.Add(subscription.PeerEntityId);
                }
            }
            foreach (int pubsubId in subscriptionsToRemove)
            {
                entityState.Subscriptions.Remove(pubsubId, out EntityState.Subscription subscription);

                // Remove listener if we have added it.
                // \note: we skip the consistency check since
                //        a) the entity is already removed so, we are inconsistent until the end of this method
                //        b) this methods may be called from already inconsistent state
                if (subscription.TargetHasAcceptedSubscriber)
                    UnregisterPubsubWatch(watchedEntity: subscription.PeerEntityId, notifiedLocalEntity: entityState.EntityId, skipConsistencyCheck: true);
            }

            // Walk through all subscribers.
            // * All established subscribers are notified and removed.
            // * All buffered requests remain.
            // * All requests delivered to the actor (but not replied to) are notified and removed.
            HashSet<int> subscribersToRemove = new HashSet<int>();
            foreach ((int pubsubId, EntityState.Subscriber subscriber) in entityState.Subscribers)
            {
                if (subscriber.TargetHasAcceptedSubscriber)
                {
                    // Established subscription.
                    subscribersToRemove.Add(pubsubId);

                    EntityShardId shardId = subscriber.PeerShardId;
                    if (shardId != _selfShardId)
                    {
                        // Send WatchedEntityTerminated to the remote shard
                        notifyShards.Add(shardId);
                    }
                    else
                    {
                        // For local entities, we WatchedEntityTerminated
                        localEntitiesToNotify.Add(subscriber.PeerEntityId);
                    }
                }
                else
                {
                    bool isBuffered = false;
                    foreach (object message in entityState.PendingMessages)
                    {
                        if (message is EntitySubscribeS2ESubscribeRequest request && request.PubSubId == pubsubId)
                        {
                            isBuffered = true;
                            break;
                        }
                    }

                    if (isBuffered)
                        continue;

                    // Was delivered to the actor, but actor didn't reply yet.
                    // We generate a EntitySubscribeS2SSubscribeResponse to the shard.
                    subscribersToRemove.Add(pubsubId);

                    EntityAskExceptionBase error;
                    if (entityState.TerminationReason != null)
                        error = new EntityCrashedError(entityState.TerminationReason);
                    else
                        error = new EntityUnreachableError(entityState.EntityId);

                    Tell(subscriber.PeerShard, new EntitySubscribeS2SSubscribeResponse(
                        subscriberPubSubId:     subscriber.PeerPubSubId,
                        targetPubSubId:         -1,
                        subscriberEntityId:     subscriber.PeerEntityId,
                        subscriberIncarnationId:subscriber.PeerIncarnationId,
                        targetIncarnationId:    -1,
                        targetChannelId:        -1,
                        targetEntityActor:      default,
                        reply:                  default,
                        error:                  new MetaSerialized<EntityAskExceptionBase>(error, MetaSerializationFlags.IncludeAll, null)
                        ));
                }
            }
            foreach (int pubsubId in subscribersToRemove)
            {
                entityState.Subscribers.Remove(pubsubId, out EntityState.Subscriber subscriber);

                // When subscriber was added, we registered the entity as a listener of the subscriber entity. Remove it.
                // \note: we skip the consistency check since
                //        a) the entity is already removed so, we are inconsistent until the end of this method
                //        b) this methods may be called from already inconsistent state
                UnregisterPubsubWatch(watchedEntity: subscriber.PeerEntityId, notifiedLocalEntity: entityState.EntityId, skipConsistencyCheck: true);
            }

            // Send notification to all remote shards.
            if (notifyShards.Count > 0)
            {
                // Inform all shards of lost entity
                foreach (EntityShardId shardId in notifyShards)
                {
                    IActorRef shardActorRef = _shardResolver.TryGetShardActor(shardId);
                    if (shardActorRef != null)
                        shardActorRef.Tell(terminated);
                    else
                        _log.Warning("Trying to route termination notification to unreachable EntityShard {ShardId}", shardId);
                }
            }

            // Notify local entities
            foreach (EntityId localNotifiedId in localEntitiesToNotify)
            {
                if (!_entityStates.TryGetValue(localNotifiedId, out EntityState watcherState))
                {
                    _log.Warning("Local watcher {WatcherId} of entity {EntityId} not found!", localNotifiedId, entityState.EntityId);
                    continue;
                }

                _log.Verbose("Inform local watcher {WatcherId} (status={WatcherState}) of entity {EntityId} termination", localNotifiedId, watcherState.Status, entityState.EntityId);
                RouteMessageToLocalEntity(watcherState, terminated);
            }
        }

        void InformLocalPubSubEntitiesOnClusterUpdate(ClusterConnectionManager.ClusterChangedEvent cluster)
        {
            // Inform watches for entities for disconnected nodes and for nodes outside the cluster.
            // Compute the set of connected shard ids and check all watches are within this set.
            HashSet<EntityShardId> connectedClusterShards = new HashSet<EntityShardId>();
            foreach (ClusterConnectionManager.ClusterChangedEvent.ClusterMember member in cluster.Members)
            {
                if (!member.IsConnected)
                    continue;

                // Include all existing shards on the node.
                foreach (EntityKind kind in member.Info.EntityShardActors.Keys)
                {
                    if (_clusterConfig.ResolveNodeShardId(kind, member.Address, out EntityShardId shardId))
                        connectedClusterShards.Add(shardId);
                }
            }

            // Collect the EntityIds on the nodes that are no longer connected (disconnected or not in cluster anymore)
            // Note that the detection is per-shard and not per-node. This handles normal shard lifecycle events.
            List<EntityId> terminatedEntityIds = new List<EntityId>();
            foreach ((EntityId watchedEntity, LocalWatcherSet watcherSet) in _localWatchers)
            {
                bool entityShardExists = connectedClusterShards.Contains(watcherSet.WatchedShardId);
                if (!entityShardExists)
                    terminatedEntityIds.Add(watchedEntity);
            }

            // Early exit if nothing to be done
            if (terminatedEntityIds.Count == 0)
                return;

            _log.Warning("Lost nodes with watched entities. {NumRemoved} watched entities terminated.", terminatedEntityIds.Count);
            _log.Debug("Entities lost in cluster update: {TerminatedEntityIds}", string.Join(", ", terminatedEntityIds));

            // Handle all terminated entities
            foreach (EntityId terminatedEntity in terminatedEntityIds)
            {
                LocalWatcherSet             localWatchers           = _localWatchers[terminatedEntity];
                HashSet<int>                incarnationsToNotify    = new HashSet<int>();
                Dictionary<EntityId, int>   watcherRefs             = new Dictionary<EntityId, int>();
                HashSet<int>                pubsubsToRemove         = new HashSet<int>();

                foreach (EntityId notifiedId in localWatchers.NotifiedEntities.Keys)
                {
                    if (!_entityStates.TryGetValue(notifiedId, out EntityState notifiedEntityState))
                    {
                        _log.Warning("Entity for local watcher {WatcherEntityId} not found!", notifiedId);
                        continue;
                    }

                    int refsToRemove = 0;
                    incarnationsToNotify.Clear();

                    // \note: Linear complexity w.r.t. the number of total subscriptions.
                    // \note: No Incarnation check. All incarnations die when node is lost
                    pubsubsToRemove.Clear();
                    foreach ((int pubSubId, EntityState.Subscription subscription) in notifiedEntityState.Subscriptions)
                    {
                        if (subscription.PeerEntityId != terminatedEntity)
                            continue;

                        pubsubsToRemove.Add(pubSubId);

                        if (subscription.TargetHasAcceptedSubscriber)
                        {
                            // Only completed subscriptions have watch and get notified.
                            refsToRemove++;

                            // If watched entitity disconnects, pending unsubscribes complete with a failure.
                            // Otherwise we notify the actor
                            if (subscription.PendingUnsubscribe != null)
                                subscription.PendingUnsubscribe.SetResult(UnsubscribeResult.UnknownSubscriber);
                            else
                                incarnationsToNotify.Add(subscription.PeerIncarnationId);
                        }
                        else
                        {
                            // Normally remote shard would always reply to all subscriptions requests. But in the case
                            // the remote shard dies, we need to reply on their behalf.
                            subscription.OpeningPromise.SetException(new EntityUnreachableError(terminatedEntity));
                        }
                    }
                    foreach (int pubsubId in pubsubsToRemove)
                        notifiedEntityState.Subscriptions.Remove(pubsubId);

                    // \note: Linear complexity w.r.t. the number of total subscribers.
                    // \note: No Incarnation check. All incarnations die when node is lost
                    pubsubsToRemove.Clear();
                    foreach ((int pubsubId, EntityState.Subscriber subscriber) in notifiedEntityState.Subscribers)
                    {
                        if (subscriber.PeerEntityId != terminatedEntity)
                            continue;

                        pubsubsToRemove.Add(pubsubId);
                        refsToRemove++;
                        incarnationsToNotify.Add(subscriber.PeerIncarnationId);
                    }
                    foreach (int pubsubId in pubsubsToRemove)
                        notifiedEntityState.Subscribers.Remove(pubsubId);

                    if (refsToRemove > 0)
                    {
                        // Got notification for something we did watch. Stray messages happen for example a subscribing entity has
                        // crashed before receiving reply, but this shard's entity has already rejected the subscription. That is
                        // safe to ignore.
                        watcherRefs.Add(notifiedId, refsToRemove);
                    }

                    if (incarnationsToNotify.Count > 0)
                    {
                        // All incarnations die
                        _log.Verbose("Informing local watcher {WatcherEntityId} (status={EntityStatus}) of death of {TerminatedEntityId}", notifiedId, notifiedEntityState.Status, terminatedEntity);
                        foreach (int incarnationId in incarnationsToNotify)
                        {
                            WatchedEntityTerminated terminated = new WatchedEntityTerminated(terminatedEntity, incarnationId);
                            RouteMessageToLocalEntity(notifiedEntityState, terminated);
                        }
                    }
                }

                UnregisterManyPubSubWatches(localWatchers, terminatedEntity, watcherRefs);
            }

            CheckWatcherSetConsistency();
        }

        void ReceiveWatchedEntityTerminated(WatchedEntityTerminated terminated)
        {
            if (!_localWatchers.TryGetValue(terminated.EntityId, out LocalWatcherSet localWatchers))
            {
                _log.Verbose("Watched entity {EntityId} terminated, no watchers", terminated.EntityId);
                return;
            }

            if (_log.IsDebugEnabled)
                _log.Debug("Watched entity {EntityId} terminated, watched by: {Watchers}", terminated.EntityId, string.Join(", ", localWatchers.NotifiedEntities.Keys));

            Dictionary<EntityId, int> watcherRefs = new Dictionary<EntityId, int>();
            HashSet<int> pubsubsToRemove = new HashSet<int>();
            foreach (EntityId notifiedId in localWatchers.NotifiedEntities.Keys)
            {
                if (!_entityStates.TryGetValue(notifiedId, out EntityState notifiedEntityState))
                {
                    _log.Warning("Entity for local watcher {WatcherEntityId} not found!", notifiedId);
                    continue;
                }

                int refsToRemove = 0;
                int refsToNotify = 0;

                // \note: Linear complexity w.r.t. the number of total subscriptions.
                pubsubsToRemove.Clear();
                foreach ((int pubsubId, EntityState.Subscription subscription) in notifiedEntityState.Subscriptions)
                {
                    if (subscription.PeerEntityId == terminated.EntityId
                        && subscription.TargetHasAcceptedSubscriber
                        && subscription.PeerIncarnationId == terminated.IncarnationId)
                    {
                        pubsubsToRemove.Add(pubsubId);
                        refsToRemove++;

                        // If watched entitity dies, pending unsubscribes complete with a failure.
                        // Otherwise we notify the actor
                        if (subscription.PendingUnsubscribe != null)
                            subscription.PendingUnsubscribe.SetResult(UnsubscribeResult.UnknownSubscriber);
                        else
                            refsToNotify++;
                    }
                }
                foreach (int pubsubId in pubsubsToRemove)
                    notifiedEntityState.Subscriptions.Remove(pubsubId);

                // \note: Linear complexity w.r.t. the number of total subscribers.
                pubsubsToRemove.Clear();
                foreach ((int pubsubId, EntityState.Subscriber subscriber) in notifiedEntityState.Subscribers)
                {
                    if (subscriber.PeerEntityId == terminated.EntityId
                        && subscriber.PeerIncarnationId == terminated.IncarnationId)
                    {
                        pubsubsToRemove.Add(pubsubId);
                        refsToRemove++;
                        refsToNotify++;
                    }
                }
                foreach (int pubsubId in pubsubsToRemove)
                    notifiedEntityState.Subscribers.Remove(pubsubId);

                if (refsToRemove > 0)
                {
                    // Got notification for something we did watch. Stray messages happen for example a subscribing entity has
                    // crashed before receiving reply, but this shard's entity has already rejected the subscription. That is
                    // safe to ignore.
                    watcherRefs.Add(notifiedId, refsToRemove);
                }

                if (refsToNotify > 0)
                {
                    _log.Verbose("Informing local watcher {WatcherEntityId} (status={EntityStatus}) of death of {TerminatedEntityId}", notifiedId, notifiedEntityState.Status, terminated.EntityId);
                    RouteMessageToLocalEntity(notifiedEntityState, terminated);
                }
            }

            UnregisterManyPubSubWatches(localWatchers, terminated.EntityId, watcherRefs);
            CheckWatcherSetConsistency();
        }

        void RegisterPubsubWatch(EntityShardId watchedShardId, EntityId watchedEntity, EntityId notifiedLocalEntity)
        {
            if (!_localWatchers.TryGetValue(watchedEntity, out LocalWatcherSet watcherSet))
            {
                watcherSet = new LocalWatcherSet(watchedShardId);
                _localWatchers.Add(watchedEntity, watcherSet);
            }

            watcherSet.NotifiedEntities[notifiedLocalEntity] = watcherSet.NotifiedEntities.GetValueOrDefault(notifiedLocalEntity, defaultValue: 0) + 1;

            CheckWatcherSetConsistency();
        }

        void UnregisterPubsubWatch(EntityId watchedEntity, EntityId notifiedLocalEntity, bool skipConsistencyCheck = false)
        {
            if (!_localWatchers.TryGetValue(watchedEntity, out LocalWatcherSet watcherSet))
            {
                _log.Debug("Removing non-existent entity watch {FromEntityId} -> {ToEntityId} (no watchSet found for source)", watchedEntity, notifiedLocalEntity);
                return;
            }

            if (!watcherSet.NotifiedEntities.TryGetValue(notifiedLocalEntity, out int refCount))
            {
                _log.Debug("Removing non-existent entity watch {FromEntityId} -> {ToEntityId} (target not found in watchSet)", watchedEntity, notifiedLocalEntity);
                return;
            }

            if (refCount == 1)
            {
                watcherSet.NotifiedEntities.Remove(notifiedLocalEntity);

                if (watcherSet.NotifiedEntities.Count == 0)
                    _localWatchers.Remove(watchedEntity);
            }
            else
                watcherSet.NotifiedEntities[notifiedLocalEntity] = refCount - 1;

            if (!skipConsistencyCheck)
                CheckWatcherSetConsistency();
        }

        void UnregisterManyPubSubWatches(LocalWatcherSet localWatchers, EntityId watchedEntityId, Dictionary<EntityId, int> watchersToRemove)
        {
            foreach ((EntityId watcher, int refsToRemove) in watchersToRemove)
            {
                int refs = localWatchers.NotifiedEntities[watcher];
                refs -= refsToRemove;

                if (refs == 0)
                {
                    localWatchers.NotifiedEntities.Remove(watcher);
                    if (localWatchers.NotifiedEntities.Count == 0)
                        _localWatchers.Remove(watchedEntityId);
                }
                else
                    localWatchers.NotifiedEntities[watcher] = refs;
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        void CheckWatcherSetConsistency()
        {
            DateTime now = DateTime.UtcNow;
            Dictionary<EntityId, Dictionary<EntityId, int>> expected = new Dictionary<EntityId, Dictionary<EntityId, int>>();

            void AddRef(EntityId target, EntityId local)
            {
                if (!expected.TryGetValue(target, out Dictionary<EntityId, int> watcherSet))
                {
                    watcherSet = new Dictionary<EntityId, int>();
                    expected.Add(target, watcherSet);
                }
                watcherSet[local] = watcherSet.GetValueOrDefault(local, defaultValue: 0) + 1;
            }

            foreach (EntityState entity in _entityStates.Values)
            {
                foreach (EntityState.Subscription subscription in entity.Subscriptions.Values)
                {
                    // Subscriptions are watched when remote shard accepts the subscription
                    if (subscription.TargetHasAcceptedSubscriber)
                        AddRef(subscription.PeerEntityId, entity.EntityId);
                }
                foreach (EntityState.Subscriber subscriber in entity.Subscribers.Values)
                {
                    // Subscribers are always watched. They might die during subscribe and
                    // we need to inform the target entity in case the subscription is successful.
                    AddRef(subscriber.PeerEntityId, entity.EntityId);
                }
            }

            foreach (EntityId entityId in _localWatchers.Keys)
            {
                if (!expected.TryGetValue(entityId, out Dictionary<EntityId, int> expectedWatchers))
                    expectedWatchers = new Dictionary<EntityId, int>();

                Dictionary<EntityId, int> localWatchers = _localWatchers[entityId].NotifiedEntities;
                foreach ((EntityId notified, int count) in localWatchers)
                {
                    if (!expectedWatchers.TryGetValue(notified, out int expectedRefCount))
                    {
                        if (_pubsubConsistencyErrorThrottle.TryTrigger(now))
                            _log.Error("Watch set is inconsistent. Stray watcher for {EntityId} into {NotifiedId}", entityId, notified);
                        return;
                    }
                    if (expectedRefCount != count)
                    {
                        if (_pubsubConsistencyErrorThrottle.TryTrigger(now))
                            _log.Error("Watch set is inconsistent. Stray watch count from {EntityId} into {NotifiedId} was {Got}, expected {Expected}", entityId, notified, count, expectedRefCount);
                        return;
                    }
                }

                foreach (EntityId notified in expectedWatchers.Keys)
                {
                    if (!localWatchers.ContainsKey(notified))
                    {
                        if (_pubsubConsistencyErrorThrottle.TryTrigger(now))
                            _log.Error("Watch set is inconsistent. Missing watch for {EntityId} into {NotifiedId}", entityId, notified);
                        return;
                    }
                }
            }
            foreach (EntityId entityId in expected.Keys)
            {
                if (!_localWatchers.ContainsKey(entityId))
                {
                    if (_pubsubConsistencyErrorThrottle.TryTrigger(now))
                        _log.Error("Watch set is inconsistent. Missing watch for {EntityId}", entityId);
                    return;
                }
            }
        }
    }
}
