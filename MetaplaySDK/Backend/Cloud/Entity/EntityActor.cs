// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity.EntityStatusMessages;
using Metaplay.Cloud.Entity.Synchronize;
using Metaplay.Cloud.Entity.Synchronize.Messages;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Message;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using static Metaplay.Cloud.Sharding.EntityShard;

namespace Metaplay.Cloud.Entity
{
    /// <summary>
    /// Optional stronger-typed base class for entity ask requests, an alternative for directly inheriting <see cref="MetaMessage"/>.
    /// This annotates the corresponding response type as a type parameter. Additionally, the response type must inherit <see cref="EntityAskResponse"/>.
    /// <para>
    /// This enables response type checking and deduction at EntityAskAsync call site (at build time), and checking
    /// the reply type of [EntityAskHandler] methods (at init time).
    /// </para>
    /// <para>
    /// <example>
    /// Usage:
    /// <code>
    /// <![CDATA[
    ///
    /// [MetaMessage(...)]
    /// public class MyAskRequest : EntityAskRequest<MyAskResponse>
    /// {
    ///     ...
    /// }
    ///
    /// [MetaMessage(...)]
    /// public class MyAskResponse : EntityAskResponse
    /// {
    ///     ...
    /// }
    ///
    /// ]]>
    /// </code>
    /// </example>
    /// </para>
    /// </summary>
    #pragma warning disable CS0618 // "Obsoletion" of non-generic EntityAskRequest (it's marked obsolete to avoid accidentally deriving from it)
    public abstract class EntityAskRequest<TResponse> : EntityAskRequest
    #pragma warning restore CS0618
        where TResponse : EntityAskResponse
    {
    }

    [Obsolete("Derive from generic EntityAskRequest<TResponse> instead.")]
    public abstract class EntityAskRequest : MetaMessage
    {
    }

    /// <summary>
    /// Optional stronger-typed base class for entity ask responses. See <see cref="EntityAskRequest{TResponse}"/>.
    /// </summary>
    public abstract class EntityAskResponse : MetaMessage
    {
    }

    /// <summary>
    /// The base class for remote exceptions during EntityAsk. Exception must be is MetaSerializable.
    /// </summary>
    [MetaSerializable]
    public abstract class EntityAskExceptionBase : Exception
    {
        protected EntityAskExceptionBase() { }

        public abstract override string Message { get; }
    }

    /// <summary>
    /// Base class for communicating refusal in entity asks via exceptions.
    /// <para>
    /// EntityAskRefusals thrown from entity ask handlers are propagated to the ask source via serializing the contents and
    /// raising the exception at the call site of the source. The contract for the entity ask handler is that when an
    /// EntityAskRefusal is thrown the callee actor must remain in a well-defined state, in particular the actor of the handler
    /// is not terminated when an EntityAskRefusal is thrown.
    /// </para>
    /// <para>
    /// A single <c>EntityAskRefusal</c> instance is tied to the Entity that throws it the first time. If caller Entity
    /// rethrows the same exception instance, or any exception instance that is not tied to itself, the exception is handled as
    /// if any unhandled exception causing actor to terminate. This means that Entity Ask can only be refused by explicitly
    /// throwing a new EntityAskRefusal in the handler. This prevents unintended ask rejection if an entity ask handler performs
    /// another entity ask, which then is rejected. If the outer ask does not handle the rejection, the rejection would get rethrown
    /// causing the outer ask to be rejected too. This is unexpected as unhandled exceptions should terminate the actor.
    /// </para>
    /// </summary>
    public abstract class EntityAskRefusal : EntityAskExceptionBase
    {
        /// <summary>
        /// The Entity that throwed the exception. <c>None</c> if the current entity.
        /// </summary>
        [MetaMember(200)] public EntityId OriginEntity { get; set; }

        protected EntityAskRefusal() { }
    }

    /// <summary>
    /// A simple EntityAskRefusal containing a single error string.
    /// <inheritdoc cref="EntityAskRefusal"/>
    /// </summary>
    [MetaSerializableDerived(MessageCodesCore.InvalidEntityAsk)]
    public class InvalidEntityAsk : EntityAskRefusal
    {
        [MetaMember(1)] public string Error { get; private set; }

        private InvalidEntityAsk() { }
        public InvalidEntityAsk(string s)
        {
            Error = s;
        }

        public override string Message => Error;
    }

    /// <summary>
    /// A EntityAskRefusal for the case where entity has no [EntityAskHandler] for the message.
    /// <inheritdoc cref="EntityAskRefusal"/>
    /// </summary>
    [MetaSerializableDerived(MessageCodesCore.NoHandlerForEntityAsk)]
    public class NoHandlerForEntityAsk : EntityAskRefusal
    {
        [MetaMember(1)] public EntityId EntityId        { get; private set; }
        [MetaMember(2)] public string   AskTypeName     { get; private set; }
        [MetaMember(3)] public string   ActorTypeName   { get; private set; }

        NoHandlerForEntityAsk() { }
        public NoHandlerForEntityAsk(EntityId entityId, string askTypeName, string actorTypeName)
        {
            EntityId = entityId;
            AskTypeName = askTypeName;
            ActorTypeName = actorTypeName;
        }

        public override string Message => $"Entity {EntityId} has no handler for {AskTypeName}. Add a handler to {ActorTypeName}, for example `[EntityAskHandler] T Handle{AskTypeName}({AskTypeName} request) {{ return .. }}`";
    }

    /// <summary>
    /// A generic "ok" response from entity asks with no payload
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityAskOk, MessageDirection.ServerInternal)]
    public class EntityAskOk : EntityAskResponse
    {
        private EntityAskOk() { }
        public static EntityAskOk Instance = new EntityAskOk();
    }

    /// <summary>
    /// Request handling of a message. The message handler itself does not return, but a
    /// <see cref="HandleMessageResponse"/> is returned on successful execution. On failure,
    /// a <see cref="EntityShard.RemoteEntityError"/> is returned instead.
    /// </summary>
    [MetaMessage(MessageCodesCore.HandleMessageRequest, MessageDirection.ServerInternal)]
    public class HandleMessageRequest : EntityAskRequest<HandleMessageResponse>
    {
        public MetaMessage Message { get; private set; }

        private HandleMessageRequest() { }
        public HandleMessageRequest(MetaMessage message) => Message = message;
    }

    [MetaMessage(MessageCodesCore.HandleMessageResponse, MessageDirection.ServerInternal)]
    public class HandleMessageResponse : EntityAskResponse
    {
        public static readonly HandleMessageResponse Instance = new HandleMessageResponse();
    }

    public interface IEntityReceiver
    {
        void ReplyToAsk(EntityShard.EntityAsk ask, MetaMessage reply);
        void ReplyToAsk<TResult>(EntityShard.EntityAsk<TResult> ask, TResult reply) where TResult : EntityAskResponse;
    }

    public interface IEntityMessagingApi
    {
        /// <summary>
        /// Sends <paramref name="request"/> to the given <paramref name="entity"/>, where it is handled by the entity's [EntityAskHandler] for that message type.
        /// Returns the result of ask as computed by the target entity ask handler.
        /// </summary>
        /// <exception cref="EntityAskRefusal">when target entity handler method throws an <see cref="EntityAskRefusal"/></exception>
        /// <exception cref="EntityShard.UnexpectedEntityAskError">when target entity crashes due to handler throwing an unhandled </exception>
        /// <exception cref="TimeoutException">if no response is received within a reasonable time</exception>
        Task<TResult> EntityAskAsync<TResult>(EntityId entity, MetaMessage request) where TResult : MetaMessage;
        /// <inheritdoc cref="EntityAskAsync{TResult}(EntityId, MetaMessage)"/>
        Task<TResult> EntityAskAsync<TResult>(EntityId targetEntityId, EntityAskRequest<TResult> message) where TResult : EntityAskResponse;
        [Obsolete("Type mismatch between TResult type parameter and the annotated response type for the request parameter type.", error: true)]
        Task<TResult> EntityAskAsync<TResult>(EntityId targetEntityId, EntityAskRequest message) where TResult : MetaMessage;

        /// <summary>
        /// Sends a message to the target entity with the given EntityId, the message is handled by the entity's [MessageHandler] for that message type.
        /// </summary>
        /// <exception cref="ArgumentException">If given target is not valid.</exception>
        void CastMessage(EntityId targetEntityId, MetaMessage message);

        /// <summary>
        /// Send a message to the actor itself. The message is handled by the actor's [CommandHandler] for that message type.
        /// </summary>
        void TellSelf(object message);
    }

    /// <summary>
    /// Base actor class for all game entities. Provides various services over pure actors, such as message routing in cluster,
    /// more convenient async command/message handlers, AskAsync(), etc.
    /// </summary>
    // \todo [petri] better docs
    public abstract partial class EntityActor : MetaReceiveActor, IEntityReceiver, IEntityMessagingApi, IEntitySynchronizeCallbacks
    {
        // Metrics

        static readonly Prometheus.HistogramConfiguration c_entityAskDurationConfig = new Prometheus.HistogramConfiguration
        {
            Buckets     = Metaplay.Cloud.Metrics.Defaults.LatencyDurationBuckets,
            LabelNames  = new string[] { "message" },
        };
        static readonly Prometheus.HistogramConfiguration c_entitySyncOpenDurationConfig = c_entityAskDurationConfig;
        static readonly Prometheus.HistogramConfiguration c_entitySyncDurationConfig = new Prometheus.HistogramConfiguration
        {
            Buckets     = Metaplay.Cloud.Metrics.Defaults.LatencyDurationBuckets,
            LabelNames  = new string[] { "message", "peer" },
        };

        static readonly Prometheus.Counter      c_entityAsks                = Prometheus.Metrics.CreateCounter("game_entity_asks_total", "Total amount of EntityAsk or SubscribeTo performed by message type", "message");
        static readonly Prometheus.Counter      c_entityAskErrors           = Prometheus.Metrics.CreateCounter("game_entity_ask_errors_total", "Total amount of errors during EntityAsks or SubscribeTos by message type and reason", "message", "reason");
        static readonly Prometheus.Histogram    c_entityAskDuration         = Prometheus.Metrics.CreateHistogram("game_entity_ask_duration", "Duration of successful EntityAsks by message type", c_entityAskDurationConfig);
        static readonly Prometheus.Counter      c_entitySyncs               = Prometheus.Metrics.CreateCounter("game_entity_syncs_total", "Total amount of EntitySynchronize performed by opening message type", "message");
        static readonly Prometheus.Counter      c_entitySyncErrors          = Prometheus.Metrics.CreateCounter("game_entity_sync_errors_total", "Total amount of errors during EntitySynchronize by opening message type", "message", "reason");
        static readonly Prometheus.Histogram    c_entitySyncOpenDuration    = Prometheus.Metrics.CreateHistogram("game_entity_sync_open_duration", "Duration of successful EntitySynchronize init by opening message type", c_entitySyncOpenDurationConfig);
        static readonly Prometheus.Histogram    c_entitySyncDuration        = Prometheus.Metrics.CreateHistogram("game_entity_sync_duration", "Duration of successful EntitySynchronize from beginning to end by opening message type and peer", c_entitySyncDurationConfig);

        // Members

        protected EntityId                  _entityId { get; }

        protected EntityDispatcher          _dispatcher { get; }

        static Dictionary<EntityKind, List<Type>> s_concreteComponentTypes = new Dictionary<EntityKind, List<Type>>();
        protected EntityComponent[] _components { get; }

        /// <summary>
        /// The IActorRef of the parent <see cref="Metaplay.Cloud.Sharding.EntityShard"/> actor.
        /// </summary>
        protected readonly IActorRef _shard;

        protected EntityActor(EntityId entityId)
        {
            if (!entityId.IsValid)
                throw new ArgumentException($"Invalid EntityId {entityId}", nameof(entityId));

            EntityConfiguration config = MetaplayServices.Get<EntityConfiguration>();

            _entityId = entityId;
            _components = InitializeComponents(entityId.Kind);
            _dispatcher = config.Dispatchers.Get(GetType());

            InitializeContinueTaskOnActorContext();

            // Message handlers
            RegisterHandlers();

            _shard = Context.Parent;
        }

        // Reads the EntityComponentTypes config and translates declared component types into concrete integration implementation types.
        // This assumes that the component implementations always require an integration class, if the integration class is not defined
        // then the corresponding component is ignored.
        [EntityActorRegisterCallback]
        static void RegisterComponentTypes(EntityConfigBase config)
        {
            List<Type> components = new List<Type>();
            foreach (Type componentType in config.EntityComponentTypes)
            {
                if (componentType.ImplementsInterface<IMetaIntegration>())
                {
                    IEnumerable<Type> concreteTypes = IntegrationRegistry.GetIntegrationClasses(componentType);
                    if (concreteTypes.Count() > 1)
                        throw new Exception($"Multiple concrete implementation classes for EntityComponent type {componentType} found: {string.Join(',', concreteTypes.Select(x => x.Name))}");

                    Type concreteType = concreteTypes.SingleOrDefault();
                    if (concreteType != null)
                        components.Add(concreteType);
                }
                else
                {
                    components.Add(componentType);
                }
            }
            if (components.Count > 0)
            {
                s_concreteComponentTypes[config.EntityKind] = components;
            }
        }

        public static bool TryGetComponentTypes(EntityKind kind, out List<Type> types)
        {
            return s_concreteComponentTypes.TryGetValue(kind, out types);
        }

        // Instantiate component implementations for the concrete component classes discovered in `RegisterComponentTypes`.
        // Components instances are assigned to the `_components` array by their index in the `_concreteComponentTypes` list
        // and the message dispatcher uses this index when routing messages to the appropriate receiver component.
        EntityComponent[] InitializeComponents(EntityKind kind)
        {
            if (!s_concreteComponentTypes.TryGetValue(kind, out List<Type> componentTypes))
                return null;
            EntityComponent[] components = new EntityComponent[componentTypes.Count];
            foreach ((Type componentType, int componentIndex) in componentTypes.ZipWithIndex())
            {
                components[componentIndex] = CreateComponent(componentType);
            }
            return components;
        }

        public EntityComponent GetComponentByIndex(int index)
        {
            return _components[index];
        }

        // The integration hook for creating an instance of an EntityComponent as part of EntityActor construction.
        // When a concrete implementation of a supported EntityComponent exists, the integration must implement
        // this method for the integration type. Derived SDK classes may further specialize the creation function for
        // the component types they support.
        protected virtual EntityComponent CreateComponent(Type componentType)
        {
            throw new NotImplementedException($"EntityActor of type {GetType()} does not implement CreateComponent for component type {componentType}");
        }

        protected override void PreStart()
        {
            base.PreStart();
            InitializeExecuteOnActorContext();
            InitializeScheduleExecuteOnActorContext();
        }

        protected override void PostStop()
        {
            base.PostStop();
        }

        protected virtual void RegisterHandlers()
        {
            RegisterShutdownHandlers();
            RegisterPeriodicTimerHandlers();
            RegisterPubSubHandlers();

            ReceiveAsync<ShutdownEntity>(ReceiveShutdownEntity);
            Receive<Terminated>(ReceiveTerminated);
            ReceiveAsync<EntityShard.EntityAsk>(ReceiveEntityAsk);
            ReceiveAsync<EntitySynchronizationS2EBeginRequest>(ReceiveEntitySync);
            ReceiveAsync<EntityShard.IRoutedMessage>(ReceiveRoutedMessage);
            ReceiveAsync<object>(ReceiveObject);
        }

        private async Task ReceiveShutdownEntity(ShutdownEntity shutdown)
        {
            // Cancel any upcoming actor timers already. We are dying, no point continuing.
            _cancelTimers.Cancel();

            await OnShutdown();
            Context.Stop(_self);

            CancelAllPeriodicTimers();
            CancelAllPendingOnActorContextTasksForEntityShutdown();
            CancelAllScheduledExecuteOnActorContextTasksForEntityShutdown();
            CancelAutoShutdownTimerForEntityShutdown();
        }

        void ReceiveTerminated(Terminated terminated)
        {
            //_log.Warning("Terminated: actor={0}, address={1}, existed={2}", terminated.ActorRef, terminated.AddressTerminated, terminated.ExistenceConfirmed);

            // Invoke OnTerminated() for unknown actors
            OnTerminated(terminated);
        }

        async Task ReceiveEntityAsk(EntityShard.EntityAsk ask)
        {
            //_log.Debug("EntityAsk: from={0}, ownerShard={1}, target={2}", ask.FromEntityId, ask.OwnerShard, ask.TargetEntityId);
            try
            {
                MetaMessage message = ask.Message.Deserialize(resolver: null, logicVersion: null);
                try
                {
                    if (_dispatcher.TryGetEntityAskDispatchFunc(message.GetType(), out EntityDispatcher.DispatchEntityAsk dispatchFunc))
                        await dispatchFunc(this, ask, message).ConfigureAwait(false);
                    else
                        await HandleUnknownEntityAsk(ask, message).ConfigureAwait(false);
                }
                catch (EntityAskRefusal error) when (error.OriginEntity == EntityId.None)
                {
                    // Entity ask handler threw a controlled exception, propagate to caller
                    error.OriginEntity = _entityId;
                    Tell(ask.OwnerShard, EntityShard.EntityAskReply.FailWithError(ask.Id, ask.FromEntityId, _entityId, error));
                }
                return;
            }
            catch (Exception ex)
            {
                // Respond with error & propagate exception
                _log.Error("Unhandled exception when handling EntityAsk<{EntityAskType}>: {Exception}", MetaSerializationUtil.PeekMessageName(ask.Message), ex);
                // \note This may double-respond in case the handler responded, but then failed -- should not be a problem, though
                Tell(ask.OwnerShard, EntityShard.EntityAskReply.FailWithError(ask.Id, ask.FromEntityId, _entityId, new UnexpectedEntityAskError(_entityId, "EntityAsk handler", ex)));
                throw;
            }
        }

        async Task ReceiveRoutedMessage(EntityShard.IRoutedMessage routed)
        {
            EntityId fromEntityId = routed.FromEntityId;
            MetaMessage msg = routed.Message.Deserialize(resolver: null, logicVersion: null);

            if (_dispatcher.TryGetMessageDispatchFunc(msg.GetType(), out EntityDispatcher.DispatchMessage dispatchFunc))
                await dispatchFunc(this, fromEntityId, msg).ConfigureAwait(false);
            else
                await HandleUnknownMessage(fromEntityId, msg).ConfigureAwait(false);
        }

        async Task ReceiveEntitySync(EntitySynchronizationS2EBeginRequest syncReq)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MetaMessage message = syncReq.Message.Deserialize(resolver: null, logicVersion: null);

            if (!_dispatcher.TryGetEntitySynchronizeDispatchFunc(message.GetType(), out EntityDispatcher.DispatchEntitySynchronize dispatchFunc))
            {
                _log.Warning("Unknown EntitySynchronize message from {0}: {1}", syncReq.FromEntityId, PrettyPrint.Compact(syncReq.Message));
                return;
            }

            // Reply to shard we have established Sync.
            string openingMessageType = MetaMessageRepository.Instance.GetFromType(message.GetType()).Name;
            Channel<MetaSerialized<MetaMessage>> syncChannel = Channel.CreateUnbounded<MetaSerialized<MetaMessage>>(new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true });
            EntitySynchronizationS2EBeginResponse response = new EntitySynchronizationS2EBeginResponse(
                channelId:      syncReq.ChannelId,
                fromEntityId:   _entityId,
                writerPeer:     syncChannel.Writer
                );
            Tell(_shard, response);

            // Initiate sync processing
            // \note: Dispose() is called manually and only if dispatchFunc completes successfully. This means
            //        that caller will receive EOF only if dispatchFunc completed successfully. If dispatchFunc
            //        throws, caller will not receive EOF (unless dispatchFunc has explicitly Disposed the
            //        EntitySynchronize before throwing).
            EntitySynchronize synchronize = new EntitySynchronize(this, _entityId, syncReq.FromEntityId, syncReq.ChannelId, syncChannel.Reader, sw, label: openingMessageType, isCaller: false);
            await dispatchFunc(this, synchronize, message);
            synchronize.Dispose();

            return;
        }

        async Task ReceiveObject(object command)
        {
            if (_dispatcher.TryGetCommandDispatchFunc(command.GetType(), out EntityDispatcher.DispatchCommand dispatchFunc))
                await dispatchFunc(this, command).ConfigureAwait(false);
            else
                HandleUnknownCommand(command);
        }

        void ReplyToAskImpl(EntityShard.EntityAsk ask, MetaMessage reply)
        {
            //_log.Debug("ReplyToAsk: from={0}, ownerShard={1}, target={2}, reply={3}", ask.FromEntityId, ask.OwnerShard, ask.TargetEntityId, PrettyPrint.Compact(reply));
            MetaSerialized<MetaMessage> serialized = MetaSerialization.ToMetaSerialized(reply, MetaSerializationFlags.IncludeAll, logicVersion: null);
            Tell(ask.OwnerShard, EntityShard.EntityAskReply.Success(ask.Id, ask.FromEntityId, _entityId, serialized));
        }

        /// <summary>
        /// Respond to a given <see cref="EntityShard.EntityAsk"/> message.
        /// </summary>
        /// <param name="ask">Context of original ask that is being responded to</param>
        /// <param name="reply">Reply message to send to asker</param>
        public void ReplyToAsk(EntityShard.EntityAsk ask, MetaMessage reply)
        {
            ReplyToAskImpl(ask, reply);
        }

        public void ReplyToAsk<TResult>(EntityShard.EntityAsk<TResult> ask, TResult reply) where TResult : EntityAskResponse
        {
            ReplyToAskImpl(ask.UntypedAsk, reply);
        }

        /// <summary>
        /// Refuse a given <see cref="EntityShard.EntityAsk"/> message.
        /// This will cause the asker's ask Task to fault with the given error.
        /// </summary>
        /// <param name="ask">Context of original ask that is being refused</param>
        /// <param name="error">The error to send to the asker</param>
        public void RefuseAsk(EntityShard.IEntityAsk ask, EntityAskRefusal error)
        {
            error.OriginEntity = _entityId;
            Tell(ask.OwnerShard, EntityShard.EntityAskReply.FailWithError(ask.Id, ask.FromEntityId, _entityId, error));
        }

        /// <summary>
        /// Returns the <see cref="EntityId"/> of a service associated with this Entity. The associated service <see cref="EntityId"/>
        /// is computed from the Entity's own <see cref="EntityId"/> and the current topology.
        /// </summary>
        protected EntityId GetAssociatedServiceEntityId(EntityKind targetEntityKind)
        {
            EntityConfigBase config = EntityConfigRegistry.Instance.GetConfig(targetEntityKind);

            switch (config.ShardingStrategy)
            {
                case StaticServiceShardingStrategy strategy:
                {
                    if (strategy.IsSingleton)
                        return EntityId.Create(targetEntityKind, 0);

                    // Pick one statically from the maximal set
#pragma warning disable CS0618 // Type or member is obsolete
                    int maxNumberServices = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>().ClusterConfig.GetNodeCountForEntityKind(targetEntityKind);
#pragma warning restore CS0618 // Type or member is obsolete
                    return EntityId.Create(targetEntityKind, _entityId.Value % (uint)maxNumberServices);
                }

                case DynamicServiceShardingStrategy strategy:
                    throw new ArgumentException($"Cannot find service entity for {config.EntityKind}, the entity is not a using StaticServiceShardingStrategy");

                default:
                    throw new ArgumentException($"Cannot find service entity for {config.EntityKind}, the entity is not a service");
            }
        }

        void IEntityMessagingApi.TellSelf(object message)
        {
            Tell(_self, message);
        }

        void IEntityMessagingApi.CastMessage(EntityId targetEntityId, MetaMessage message)
        {
            CastMessage(targetEntityId, message);
        }

        /// <inheritdoc cref="IEntityMessagingApi.CastMessage"/>
        protected void CastMessage(EntityId targetEntityId, MetaMessage message)
        {
            // Check for valid target
            if (!targetEntityId.IsValid)
                throw new ArgumentException($"targetEntityId must be a valid entity (got {targetEntityId})", nameof(targetEntityId));

            //_log.Debug("Casting to {0}: {1}", targetEntityId, message.GetType().Name);
            MetaSerialized<MetaMessage> msg = MetaSerialization.ToMetaSerialized(message, MetaSerializationFlags.IncludeAll, logicVersion: null);
            Tell(_shard, new EntityShard.CastMessage(targetEntityId, msg, _entityId));
        }

        Task<TResult> EntityAskImplAsync<TResult>(EntityId targetEntityId, MetaMessage message) where TResult : MetaMessage
        {
            return InternalEntityAskAsync<TResult>(targetEntityId, message, timeout: TimeSpan.FromMilliseconds(10_000));
        }

        public Task<TResult> EntityAskAsync<TResult>(EntityId targetEntityId, MetaMessage message) where TResult : MetaMessage
        {
            return EntityAskImplAsync<TResult>(targetEntityId, message);
        }

        /// <summary>
        /// Stronger-typed alternative for <see cref="EntityAskAsync{TResult}(EntityId, MetaMessage)"/>
        /// for a request type that explicitly annotates its response type using <see cref="EntityAskRequest{TResponse}"/>.
        /// <para>
        /// Specifying the wrong <typeparamref name="TResult"/> will cause the <see cref="EntityAskAsync{TResult}(EntityId, EntityAskRequest)"/>
        /// to be chosen, and it's marked as Obsolete to produce an error.
        /// </para>
        /// <para>
        /// It is still possible to use plain <see cref="MetaMessage"/>-derived ask requests;
        /// that will end up calling <see cref="EntityAskAsync{TResult}(EntityId, MetaMessage)"/>
        /// where the response type is not staticlly checked.
        /// </para>
        /// </summary>
        public Task<TResult> EntityAskAsync<TResult>(EntityId targetEntityId, EntityAskRequest<TResult> message) where TResult : EntityAskResponse
        {
            return EntityAskImplAsync<TResult>(targetEntityId, message);
        }

        /// <summary>
        /// Failure overload to support <see cref="EntityAskAsync{TResult}(EntityId, EntityAskRequest{TResult})"/>.
        /// </summary>
        [Obsolete("Type mismatch between TResult type parameter and the annotated response type for the request parameter type.", error: true)]
        public Task<TResult> EntityAskAsync<TResult>(EntityId targetEntityId, EntityAskRequest message) where TResult : MetaMessage
        {
            return EntityAskImplAsync<TResult>(targetEntityId, message);
        }

        async Task<TResult> InternalEntityAskAsync<TResult>(EntityId targetEntityId, MetaMessage message, TimeSpan timeout) where TResult : MetaMessage
        {
            // Check for valid target
            if (!targetEntityId.IsValid)
                throw new ArgumentException($"targetEntityId must be a valid entity (got {targetEntityId})", nameof(targetEntityId));
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            // Track metrics
            string metricsLabel = $"Ask-{MetaMessageRepository.Instance.GetFromType(message.GetType()).Name}";
            Stopwatch sw = Stopwatch.StartNew();
            c_entityAsks.WithLabels(metricsLabel).Inc();

            // Create promise for result
            TaskCompletionSource<MetaSerialized<MetaMessage>> promise = new TaskCompletionSource<MetaSerialized<MetaMessage>>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Send EntityAsk via parent shard
            MetaSerialized<MetaMessage> msg = MetaSerialization.ToMetaSerialized(message, MetaSerializationFlags.IncludeAll, logicVersion: null);
            Tell(_shard, new EntityShard.EntityAsk(targetEntityId, msg, _entityId, promise));

            // Wait for response (or timeout)
            Task<MetaSerialized<MetaMessage>> task = promise.Task;
            Task finished = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (finished == task)
            {
                c_entityAskDuration.WithLabels(metricsLabel).Observe(sw.Elapsed.TotalSeconds);

                // Propagate exceptions due to failed routing/handling
                if (task.IsFaulted)
                {
                    // \note This flattening code is duplicated in AdminApiActor.ReceiveForwardAskToEntity

                    AggregateException flattened = task.Exception.Flatten();
                    Exception effectiveException = flattened.InnerException is EntityAskExceptionBase askException ? askException : flattened;

                    // \note EntityAskRefusals are not counted in ask error metrics, because they are "controlled"
                    //       errors and are sometimes used for non-erroneous communication, for example
                    //       InternalPlayerSessionSubscribeRefused.
                    // \todo Even these should be considered erroneous if the asker doesn't actually handle the
                    //       exception. But that's not really detectable here...
                    if (!(effectiveException is EntityAskRefusal))
                        c_entityAskErrors.WithLabels(metricsLabel, "exception").Inc();

                    throw effectiveException;
                }

                // Deserialize and return result
                MetaMessage result = task.GetCompletedResult().Deserialize(resolver: null, logicVersion: null);
                if (result is TResult typed)
                    return typed;
                else
                {
                    c_entityAskErrors.WithLabels(metricsLabel, "invalidResponse").Inc();
                    throw new InvalidOperationException($"Invalid response type for EntityAskAsync<{typeof(TResult).ToGenericTypeString()}>({message.GetType().ToGenericTypeString()}) from {_entityId} to {targetEntityId}: got {result?.GetType().ToGenericTypeString() ?? "null"} (expecting {typeof(TResult).ToGenericTypeString()})");
                }
            }
            else
            {
                c_entityAskErrors.WithLabels(metricsLabel, "timeout").Inc();
                throw new TimeoutException($"Timeout during EntityAskAsync<{typeof(TResult).ToGenericTypeString()}>({message.GetType().ToGenericTypeString()}) from {_entityId} to {targetEntityId}");
            }
        }

        /// <summary>
        /// Establishes a synchronized execution and a message channel between the caller and the target entity handler. Similarly
        /// to EntityAsk, the <paramref name="message"/> is delivered to the <c>Actor.HandleEntitySynchronize(EntitySynchronize sync, MessageType msg)</c>
        /// overload of the matching type.
        ///
        /// <para>
        /// EntitySynchronize is a superset of EntityAsk. Instead of a single query and a single response, EntitySynchronize allows any number of
        /// messages to be sent and received on both sides during a single handler. During an ongoing EntitySynchronize, target entity does not
        /// have any other forward progress than executing the entity synchronize. In particular, any messages sent outside the EntitySynchronize
        /// channel are queued up and processed later. This mechanism allows EntitySynchronize to synchronize the execution with the <paramref name="targetEntityId"/>
        /// as each Send-Receive pair forms an execution fence.
        /// </para>
        ///
        /// <para>
        /// Call to EntitySynchronizeAsync completes when the initial message is delivered to the <c>Actor.HandleEntitySynchronize(EntitySynchronize sync, MessageType msg)</c>
        /// handler of the target entity, but does not guarantee any progress is made beyond that. To observe progress, the target entity should send
        /// a reply on the <c>EntitySynchronize</c> channel, and caller await for it.
        /// </para>
        ///
        /// <para>
        /// Caller MUST dispose the returned <see cref="EntitySynchronize"/>.
        /// </para>
        /// </summary>
        protected async Task<EntitySynchronize> EntitySynchronizeAsync(EntityId targetEntityId, MetaMessage message)
        {
            // Check for valid target
            if (!targetEntityId.IsValid)
                throw new ArgumentException($"targetEntityId must be a valid entity (got {targetEntityId})", nameof(targetEntityId));

            // Track metrics
            string openingMessageType = MetaMessageRepository.Instance.GetFromType(message.GetType()).Name;
            Stopwatch sw = Stopwatch.StartNew();
            c_entitySyncs.WithLabels(openingMessageType).Inc();

            // Let parent shard take care of this
            TaskCompletionSource<int> openingPromise = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            Channel<MetaSerialized<MetaMessage>> syncChannel = Channel.CreateUnbounded<MetaSerialized<MetaMessage>>(new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false });

            MetaSerialized<MetaMessage> serializedMessage = MetaSerialization.ToMetaSerialized(message, MetaSerializationFlags.IncludeAll, logicVersion: null);
            EntitySynchronizationE2SBeginRequest request = new EntitySynchronizationE2SBeginRequest(
                targetEntityId: targetEntityId,
                fromEntityId:   _entityId,
                message:        serializedMessage,
                openingPromise: openingPromise,
                writerPeer:     syncChannel.Writer
                );
            Tell(_shard, request);

            // Wait for response (or timeout)
            Task<int> openingTask = openingPromise.Task;
            Task finished = await Task.WhenAny(openingTask, Task.Delay(10_000)).ConfigureAwait(false);
            if (finished != openingTask)
            {
                c_entitySyncErrors.WithLabels(openingMessageType, "timeout").Inc();
                throw new TimeoutException($"Timeout during EntitySynchronizeAsync<{message.GetType().Name}> from {_entityId} to {targetEntityId}");
            }

            c_entitySyncOpenDuration.WithLabels(openingMessageType).Observe(sw.Elapsed.TotalSeconds);

            int channelId = openingTask.GetCompletedResult();
            EntitySynchronize synchronize = new EntitySynchronize(this, _entityId, targetEntityId, channelId, syncChannel.Reader, sw, label: openingMessageType, isCaller: true);
            return synchronize;
        }

        void IEntitySynchronizeCallbacks.EntitySynchronizeSend(EntitySynchronize sync, MetaMessage message)
        {
            MetaSerialized<MetaMessage> serializedMessage = MetaSerialization.ToMetaSerialized(message, MetaSerializationFlags.IncludeAll, logicVersion: null);
            Tell(_shard, new EntitySynchronizationE2SChannelMessage(
                fromEntityId:   _entityId,
                channelId:      sync.ChannelId,
                message:        serializedMessage
                ));
        }

        void IEntitySynchronizeCallbacks.EntitySynchronizeDispose(EntitySynchronize sync)
        {
            string peerLabel = sync.IsCaller ? "sender" : "receiver";
            c_entitySyncDuration.WithLabels(sync.Label, peerLabel).Observe(sync.Elapsed.TotalSeconds);
            Tell(_shard, new EntitySynchronizationE2SChannelMessage(
                fromEntityId:   _entityId,
                channelId:      sync.ChannelId,
                message:        new MetaSerialized<MetaMessage>()
                ));
        }

        [CommandHandler]
        public async Task HandleEntityShardInitializeEntity(InitializeEntity initialize)
        {
            // Initialize entity
            await Initialize();

            // Inform shard we're ready for business
            Tell(_shard, EntityReady.Instance);

            // We are now Running, start autoshutdown logic.
            InitializeShutdown();
        }

        /// <summary>
        /// Request execution of a message handler using the EntityAsk pattern. This allows any errors be returned back
        /// to the caller. This is mainly used by the dashboard, to make sure that any errors are routed back to the AdminApi
        /// and can be shown to the dashboard user.
        /// </summary>
        /// <param name="ask"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [EntityAskHandler]
        async Task HandleHandleMessageRequest(EntityShard.EntityAsk<HandleMessageResponse> ask, HandleMessageRequest request)
        {
            MetaMessage msg = request.Message;
            if (_dispatcher.TryGetMessageDispatchFunc(msg.GetType(), out EntityDispatcher.DispatchMessage dispatchFunc))
                await dispatchFunc(this, ask.FromEntityId, msg).ConfigureAwait(false);
            else
                await HandleUnknownMessage(ask.FromEntityId, msg).ConfigureAwait(false);

            ReplyToAsk(ask, HandleMessageResponse.Instance);
        }

        /// <summary>
        /// Throws an informative exception if <see cref="MetaReceiveActor.IsCurrentThreadOnActorContext()"/> is false.
        /// Used to guard methods that may only be called from Actor thread.
        /// </summary>
        protected void RequireCurrentThreadOnActorContext()
        {
            if (!IsCurrentThreadOnActorContext())
                throw new InvalidOperationException("This method must be called on Actor thread. Background threads should use ExecuteOnActorContextAsync to post work on Actor thread instead.");
        }

        /// <summary>
        /// Initializies the Entity Actor. This is called after Actor constructor and <see cref="PreStart"/> but before
        /// the first message is handled. This is intended for initializing async resources, such as fetching data from
        /// database or EntityAsking or Subscribing to other entities.
        /// </summary>
        protected virtual Task Initialize() { return Task.CompletedTask; }

        /// <summary>
        /// Called when entity shuts down. No messages are delivered to this actor after this method completes. This
        /// is indended for async operations such as writing the final state of the entity to the database.
        /// After this method completes, the actor stops, and <see cref="PostStop"/> is called.
        /// </summary>
        protected virtual Task OnShutdown() { return Task.CompletedTask; }
        protected virtual void HandleUnknownCommand(object command)
        {
            _log.Warning("Received {CommandName} command from {Sender} but there is no [CommandHandler] handler for it", command.GetType().ToNamespaceQualifiedTypeString(), Sender);
        }
        protected virtual Task HandleUnknownMessage(EntityId fromEntityId, MetaMessage message)
        {
            _log.Error("Received {MessageType} message from {EntityId} but there is no [MessageHandler] handler for it: {Message}", message.GetType().GetNestedClassName(), fromEntityId, PrettyPrint.Compact(message));
            return Task.CompletedTask;
        }
        protected virtual Task HandleUnknownEntityAsk(EntityShard.EntityAsk ask, MetaMessage message)
        {
            string askTypeName = message.GetType().GetNestedClassName();
            _log.Error("Received {AskType} ask from {EntityId} but there is no [EntityAskHandler] handler for it: {Message}", askTypeName, ask.FromEntityId, PrettyPrint.Compact(message));
            throw new NoHandlerForEntityAsk(_entityId, askTypeName: askTypeName, actorTypeName: GetType().ToNamespaceQualifiedTypeString());
        }

        /// <summary>
        /// Called when a monitored actor on the local node stops. See <see cref="ICanWatch.Watch(IActorRef)"/> in <see cref="UntypedActor.Context"/>.
        /// </summary>
        protected virtual void OnTerminated(Terminated terminated) { _log.Warning("Unhandled Terminated: {Actor}", terminated.ActorRef); }
    }

    /// <summary>
    /// EntityComponents represent modular behaviour components of an Entity.
    /// </summary>
    ///
    /// <remarks>
    /// Components can encapsulate actor state associated with a given (optional) feature and declare message handler methods in the same way that
    /// is supported for EntityActors. Internally the owner EntityActor routes received messages the the component based on the message type. The
    /// execution context for these handlers is the owner actor context.
    ///
    /// Supported component types for an EntityKind are declared in the EntityConfig for the entity. When the supported component type is abstract,
    /// the component is automatically enabled when an integration of the abstract class is found.
    /// </remarks>
    public abstract class EntityComponent : IEntityReceiver
    {
        protected abstract EntityActor OwnerActor { get; }

        public void ReplyToAsk(EntityShard.EntityAsk ask, MetaMessage reply)
        {
            OwnerActor.ReplyToAsk(ask, reply);
        }

        public void ReplyToAsk<TResult>(EntityShard.EntityAsk<TResult> ask, TResult reply) where TResult : EntityAskResponse
        {
            OwnerActor.ReplyToAsk(ask, reply);
        }
    }
}
