// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Entity.PubSub
{
    /// <summary>
    /// Entity -> Shard node-local message for starting entity subscribe. Shard replies by completing the promise.
    /// </summary>
    public class EntitySubscribeE2SSubscribeRequest
    {
        public class SubscribeAck
        {
            public IActorRef                    TargetActor     { get; private set; }
            public int                          IncarnationId   { get; private set; }
            public int                          PubSubId        { get; private set; }
            public int                          ChannelId       { get; private set; }
            public MetaSerialized<MetaMessage>  Response        { get; private set; }

            public SubscribeAck(IActorRef targetActor, int incarnationId, int pubSubId, int channelId, MetaSerialized<MetaMessage> response)
            {
                TargetActor = targetActor;
                IncarnationId = incarnationId;
                PubSubId = pubSubId;
                ChannelId = channelId;
                Response = response;
            }
        }

        public EntityId                                     TargetEntityId      { get; private set; }
        public EntityId                                     SubscriberEntityId  { get; private set; }
        public IActorRef                                    SubscriberActor     { get; private set; }
        public EntityTopic                                  Topic               { get; private set; }
        public int                                          ChannelId           { get; private set; }
        public MetaSerialized<MetaMessage>                  Message             { get; private set; }

        /// <summary>
        /// Completes with success, or with <see cref="EntityAskExceptionBase"/>.
        /// </summary>
        public TaskCompletionSource<SubscribeAck>           ReplyPromise    { get; private set; }

        EntitySubscribeE2SSubscribeRequest() { }
        public EntitySubscribeE2SSubscribeRequest(EntityId targetEntityId, EntityId subscriberEntityId, IActorRef subscriberActor, EntityTopic topic, int channelId, MetaSerialized<MetaMessage> message, TaskCompletionSource<SubscribeAck> replyPromise)
        {
            TargetEntityId = targetEntityId;
            SubscriberEntityId = subscriberEntityId;
            SubscriberActor = subscriberActor;
            Topic = topic;
            ChannelId = channelId;
            Message = message;
            ReplyPromise = replyPromise;
        }
    }

    /// <summary>
    /// Shard -> Shard inter-node message for starting entity subscribe. Replied with EntitySubscribeS2SSubscribeResponse.
    /// </summary>
    [MetaMessage(MessageCodesCore.EntitySubscribeS2SSubscribeRequest, MessageDirection.ServerInternal)]
    public class EntitySubscribeS2SSubscribeRequest : MetaMessage
    {
        public int                                          SubscriberPubSubId      { get; private set; }
        public EntityId                                     TargetEntityId          { get; private set; }
        public EntityId                                     SubscriberEntityId      { get; private set; }
        public IActorRef                                    SubscriberActor         { get; private set; }
        public int                                          SubscriberIncarnationId { get; private set; }
        public EntityTopic                                  Topic                   { get; private set; }
        public int                                          SubscriberChannelId     { get; private set; }
        public MetaSerialized<MetaMessage>                  Message                 { get; private set; }

        EntitySubscribeS2SSubscribeRequest() { }
        public EntitySubscribeS2SSubscribeRequest(int subscriberPubSubId, EntityId targetEntityId, EntityId subscriberEntityId, IActorRef subscriberActor, int subscriberIncarnationId, EntityTopic topic, int subscriberChannelId, MetaSerialized<MetaMessage> message)
        {
            SubscriberPubSubId = subscriberPubSubId;
            TargetEntityId = targetEntityId;
            SubscriberEntityId = subscriberEntityId;
            SubscriberActor = subscriberActor;
            SubscriberIncarnationId = subscriberIncarnationId;
            Topic = topic;
            SubscriberChannelId = subscriberChannelId;
            Message = message;
        }
    }
    [MetaMessage(MessageCodesCore.EntitySubscribeS2SSubscribeResponse, MessageDirection.ServerInternal)]
    public class EntitySubscribeS2SSubscribeResponse : MetaMessage
    {
        public int                                          SubscriberPubSubId      { get; private set; }
        public int                                          TargetPubSubId          { get; private set; }
        public EntityId                                     SubscriberEntityId      { get; private set; }
        public int                                          SubscriberIncarnationId { get; private set; }
        public int                                          TargetIncarnationId     { get; private set; }
        public int                                          TargetChannelId         { get; private set; }
        public IActorRef                                    TargetEntityActor       { get; private set; }
        public MetaSerialized<MetaMessage>                  Reply                   { get; private set; }
        public MetaSerialized<EntityAskExceptionBase>       Error                   { get; private set; }

        EntitySubscribeS2SSubscribeResponse() { }
        public EntitySubscribeS2SSubscribeResponse(int subscriberPubSubId, int targetPubSubId, EntityId subscriberEntityId, int subscriberIncarnationId, int targetIncarnationId, int targetChannelId, IActorRef targetEntityActor, MetaSerialized<MetaMessage> reply, MetaSerialized<EntityAskExceptionBase> error)
        {
            SubscriberPubSubId = subscriberPubSubId;
            TargetPubSubId = targetPubSubId;
            SubscriberEntityId = subscriberEntityId;
            SubscriberIncarnationId = subscriberIncarnationId;
            TargetIncarnationId = targetIncarnationId;
            TargetChannelId = targetChannelId;
            TargetEntityActor = targetEntityActor;
            Reply = reply;
            Error = error;
        }
    }

    /// <summary>
    /// Shard -> Entity node-local message for starting entity subscribe. Entity replies with EntitySubscribeS2ESubscribeResponse.
    /// </summary>
    public class EntitySubscribeS2ESubscribeRequest
    {
        public int                                          PubSubId                { get; private set; }
        public EntityId                                     SubscriberEntityId      { get; private set; }
        public IActorRef                                    SubscriberActor         { get; private set; }
        public int                                          SubscriberIncarnationId { get; private set; }
        public EntityTopic                                  Topic                   { get; private set; }
        public int                                          SubscriberChannelId     { get; private set; }
        public MetaSerialized<MetaMessage>                  Message                 { get; private set; }

        EntitySubscribeS2ESubscribeRequest() { }
        public EntitySubscribeS2ESubscribeRequest(int pubSubId, EntityId subscriberEntityId, IActorRef subscriberActor, int subscriberIncarnationId, EntityTopic topic, int subscriberChannelId, MetaSerialized<MetaMessage> message)
        {
            PubSubId = pubSubId;
            SubscriberEntityId = subscriberEntityId;
            SubscriberActor = subscriberActor;
            SubscriberIncarnationId = subscriberIncarnationId;
            Topic = topic;
            SubscriberChannelId = subscriberChannelId;
            Message = message;
        }
    }
    public class EntitySubscribeS2ESubscribeResponse
    {
        public int                                          PubSubId            { get; private set; }
        public EntityId                                     SubscribedEntityId  { get; private set; }
        public IActorRef                                    SubscribedActor     { get; private set; }
        public int                                          SubscribedChannelId { get; private set; }
        public MetaSerialized<MetaMessage>                  Reply               { get; private set; }
        public MetaSerialized<EntityAskExceptionBase>       Error               { get; private set; }

        EntitySubscribeS2ESubscribeResponse() { }
        public EntitySubscribeS2ESubscribeResponse(int pubSubId, EntityId subscribedEntityId, IActorRef subscribedActor, int subscribedChannelId, MetaSerialized<MetaMessage> reply, MetaSerialized<EntityAskExceptionBase> error)
        {
            PubSubId = pubSubId;
            SubscribedEntityId = subscribedEntityId;
            SubscribedActor = subscribedActor;
            SubscribedChannelId = subscribedChannelId;
            Reply = reply;
            Error = error;
        }

        public static EntitySubscribeS2ESubscribeResponse Success(int pubSubId, EntityId subscribedEntityId, int subscribedChannelId, IActorRef subscribedActor, MetaSerialized<MetaMessage> reply)
        {
            return new EntitySubscribeS2ESubscribeResponse(pubSubId, subscribedEntityId, subscribedActor, subscribedChannelId, reply, error: default);
        }
        public static EntitySubscribeS2ESubscribeResponse Failure(int pubSubId, EntityId subscribedEntityId, EntityAskExceptionBase error)
        {
            MetaSerialized<EntityAskExceptionBase> serializedError = new MetaSerialized<EntityAskExceptionBase>(error, MetaSerializationFlags.IncludeAll, null);
            return new EntitySubscribeS2ESubscribeResponse(pubSubId, subscribedEntityId, subscribedActor: null, subscribedChannelId: -1, reply: default, serializedError);
        }
    }

    /// <summary>
    /// Entity -> Shard node-local message for unsubscribing. Shard replies by completing the promise.
    /// </summary>
    public class EntitySubscribeE2SUnsubscribeRequest
    {
        [MetaSerializable]
        public enum UnsubscribeResult
        {
            Success,            // Successfully unsubscribed
            UnknownSubscriber,  // The subscriber isn't known or has died or connection has been lost. Can happen if was just kicked or connection lost.
        }

        public EntityId                                 SubscriberEntityId  { get; private set; }
        public int                                      PubSubId            { get; private set; }
        public MetaSerialized<MetaMessage>              GoodbyeMessage      { get; private set; }
        public TaskCompletionSource<UnsubscribeResult>  ReplyPromise        { get; private set; }

        EntitySubscribeE2SUnsubscribeRequest() { }
        public EntitySubscribeE2SUnsubscribeRequest(EntityId subscriberEntityId, int pubSubId, MetaSerialized<MetaMessage> goodbyeMessage, TaskCompletionSource<UnsubscribeResult> replyPromise)
        {
            SubscriberEntityId = subscriberEntityId;
            PubSubId = pubSubId;
            GoodbyeMessage = goodbyeMessage;
            ReplyPromise = replyPromise;
        }
    }

    [MetaMessage(MessageCodesCore.EntitySubscribeS2SUnsubscribeRequest, MessageDirection.ServerInternal)]
    public class EntitySubscribeS2SUnsubscribeRequest : MetaMessage
    {
        public EntityId                     TargetEntityId      { get; private set; }
        public int                          TargetIncarnationId { get; private set; }
        public int                          TargetPubSubId      { get; private set; }
        public MetaSerialized<MetaMessage>  GoodbyeMessage      { get; private set; }

        EntitySubscribeS2SUnsubscribeRequest() { }
        public EntitySubscribeS2SUnsubscribeRequest(EntityId targetEntityId, int targetIncarnationId, int targetPubSubId, MetaSerialized<MetaMessage> goodbyeMessage)
        {
            TargetEntityId = targetEntityId;
            TargetIncarnationId = targetIncarnationId;
            TargetPubSubId = targetPubSubId;
            GoodbyeMessage = goodbyeMessage;
        }
    }
    [MetaMessage(MessageCodesCore.EntitySubscribeS2SUnsubscribeResponse, MessageDirection.ServerInternal)]
    public class EntitySubscribeS2SUnsubscribeResponse : MetaMessage
    {
        public EntityId                     SubscriberEntityId      { get; private set; }
        public int                          SubscriberIncarnationId { get; private set; }
        public int                          SubscriberPubSubId      { get; private set; }
        public bool                         WasSuccess              { get; private set; }

        EntitySubscribeS2SUnsubscribeResponse() { }
        public EntitySubscribeS2SUnsubscribeResponse(EntityId subscriberEntityId, int subscriberIncarnationId, int subscriberPubSubId, bool wasSuccess)
        {
            SubscriberEntityId = subscriberEntityId;
            SubscriberIncarnationId = subscriberIncarnationId;
            SubscriberPubSubId = subscriberPubSubId;
            WasSuccess = wasSuccess;
        }
    }

    public class EntitySubscribeS2EUnsubscribeRequest
    {
        public int                          InChannelId     { get; private set; }
        public int                          PubSubId        { get; private set; }
        public EntityId                     SubscriberId    { get; private set; }
        public MetaSerialized<MetaMessage>  GoodbyeMessage  { get; private set; }

        EntitySubscribeS2EUnsubscribeRequest() { }
        public EntitySubscribeS2EUnsubscribeRequest(int inChannelId, int pubSubId, EntityId subscriberId, MetaSerialized<MetaMessage> goodbyeMessage)
        {
            InChannelId = inChannelId;
            PubSubId = pubSubId;
            SubscriberId = subscriberId;
            GoodbyeMessage = goodbyeMessage;
        }
    }
    public class EntitySubscribeS2EUnsubscribeResponse
    {
        public EntityId                     SubscribedEntityId  { get; private set; }
        public int                          PubSubId            { get; private set; }
        public bool                         WasSuccess          { get; private set; }

        EntitySubscribeS2EUnsubscribeResponse() { }
        public EntitySubscribeS2EUnsubscribeResponse(EntityId subscribedEntityId, int pubSubId, bool wasSuccess)
        {
            SubscribedEntityId = subscribedEntityId;
            PubSubId = pubSubId;
            WasSuccess = wasSuccess;
        }
    }

    public class EntitySubscribeE2SKickMessage
    {
        public EntityId                     KickerEntityId  { get; private set; }
        public int                          PubSubId        { get; private set; }
        public MetaSerialized<MetaMessage>  GoodbyeMessage  { get; private set; }

        EntitySubscribeE2SKickMessage() { }
        public EntitySubscribeE2SKickMessage(EntityId kickerEntityId, int pubSubId, MetaSerialized<MetaMessage> goodbyeMessage)
        {
            KickerEntityId = kickerEntityId;
            PubSubId = pubSubId;
            GoodbyeMessage = goodbyeMessage;
        }
    }
    [MetaMessage(MessageCodesCore.EntitySubscribeE2SKickMessage, MessageDirection.ServerInternal)]
    public class EntitySubscribeS2SKickMessage : MetaMessage
    {
        public EntityId                     TargetEntityId      { get; private set; }
        public int                          TargetIncarnationId { get; private set; }
        public int                          TargetPubSubId      { get; private set; }
        public MetaSerialized<MetaMessage>  GoodbyeMessage      { get; private set; }

        EntitySubscribeS2SKickMessage() { }
        public EntitySubscribeS2SKickMessage(EntityId targetEntityId, int targetIncarnationId, int targetPubSubId, MetaSerialized<MetaMessage> goodbyeMessage)
        {
            TargetEntityId = targetEntityId;
            TargetIncarnationId = targetIncarnationId;
            TargetPubSubId = targetPubSubId;
            GoodbyeMessage = goodbyeMessage;
        }
    }
    public class EntitySubscribeS2EKickMessage
    {
        public int                          ChannelId       { get; private set; }
        public MetaSerialized<MetaMessage>  GoodbyeMessage  { get; private set; }

        EntitySubscribeS2EKickMessage() { }
        public EntitySubscribeS2EKickMessage(int channelId, MetaSerialized<MetaMessage> goodbyeMessage)
        {
            ChannelId = channelId;
            GoodbyeMessage = goodbyeMessage;
        }
    }

    /// <summary>
    /// Envelope for messages sent over a PubSub channel (in either direction).
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityPubSubMessage, MessageDirection.ServerInternal)]
    public class PubSubMessage : MetaMessage
    {
        public EntityId                     FromEntityId    { get; private set; }
        public int                          ChannelId       { get; private set; }
        public MetaSerialized<MetaMessage>  Payload         { get; private set; }

        public PubSubMessage() { }
        public PubSubMessage(EntityId fromEntityId, int channelId, MetaSerialized<MetaMessage> payload)
        {
            FromEntityId    = fromEntityId;
            ChannelId       = channelId;
            Payload         = payload;
        }
    }

    /// <summary>
    /// A watched Entity has terminated. Sent to EntityShards and EntityActors
    /// to handle accordingly.
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityWatchedEntityTerminated, MessageDirection.ServerInternal)]
    public class WatchedEntityTerminated : MetaMessage
    {
        public EntityId EntityId { get; private set; }
        public int IncarnationId { get; private set; }

        WatchedEntityTerminated() { }
        public WatchedEntityTerminated(EntityId entityId, int incarnationId)
        {
            EntityId = entityId;
            IncarnationId = incarnationId;
        }
    }
}
