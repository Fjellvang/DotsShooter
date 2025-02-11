// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Entity.EntityStatusMessages;
using Metaplay.Cloud.Entity.PubSub;
using Metaplay.Cloud.Entity.Synchronize.Messages;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Sharding
{
    // UpdateEntityShardStats

    [MetaMessage(MessageCodesCore.UpdateShardLiveEntityCount, MessageDirection.ServerInternal)]
    public class UpdateShardLiveEntityCount : MetaMessage
    {
        public ClusterNodeAddress   NodeAddress     { get; private set; }
        public EntityKind           EntityKind      { get; private set; }
        public int                  LiveEntityCount { get; private set; }

        UpdateShardLiveEntityCount() { }
        public UpdateShardLiveEntityCount(ClusterNodeAddress nodeAddress, EntityKind entityKind, int liveEntityCount)
        {
            NodeAddress     = nodeAddress;
            EntityKind      = entityKind;
            LiveEntityCount = liveEntityCount;
        }
    }

    /// <summary>
    /// Init args for EntityShard actor.
    /// </summary>
    public class EntityShardConfig
    {
        public readonly EntityShardId Id;

        public EntityShardConfig(EntityShardId id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// EntityShard failed to start. This is communicate back to ClusterCoordinator in case there
    /// were problems when starting the entities on an EntityShard.
    /// </summary>
    class EntityShardStartException : Exception
    {
        public readonly EntityShardId   ShardId;
        public readonly Exception[]     Exceptions;

        public EntityShardStartException(EntityShardId shardId, Exception[] exceptions)
            : base($"Failed to start shard {shardId}", innerException: new AggregateException(exceptions))
        {
            ShardId = shardId;
            Exceptions = exceptions;
        }
    }

    /// <summary>
    /// EntityShard is not in a state where it can be waited to be Running.
    /// </summary>
    class EntityShardIllegalStartStateException : Exception
    {
        public readonly EntityShardId ShardId;
        public EntityShardIllegalStartStateException(EntityShardId shardId, string message) : base(message)
        {
            ShardId = shardId;
        }
    }

    // EntityShard

    public partial class EntityShard : MetaReceiveActor
    {
        /// <summary>
        /// Request an EntityShard to initialize.
        /// </summary>
        public class InitializeShard
        {
            public static readonly InitializeShard Instance = new InitializeShard();
        }

        /// <summary>
        /// Request an EntityShard to start itself, i.e., spawn any entities that should be auto-started.
        /// </summary>
        public class StartShard
        {
            public static readonly StartShard Instance = new StartShard();
        }

        /// <summary>
        /// Request to get init completion status. If init (transition to Running) failed, the
        /// response contains error information.
        /// </summary>
        public class ShardWaitUntilRunningRequest { public static readonly ShardWaitUntilRunningRequest Instance = new ShardWaitUntilRunningRequest(); }
        public class ShardWaitUntilRunningResponse
        {
            public EntityShardId        ShardId     { get; private set; }
            public ShardLifecyclePhase  Phase       { get; private set; }
            public Exception[]          InitErrors  { get; private set; }
            public ShardWaitUntilRunningResponse(EntityShardId shardId, ShardLifecyclePhase phase, Exception[] initErrors)
            {
                ShardId = shardId;
                Phase = phase;
                InitErrors = initErrors;
            }
        }

        /// <summary>
        /// Internal EntityShard command to invoke OnShutdown(). Do not use.
        /// </summary>
        class EntityShardFinalizeShutdownCommand { public static readonly EntityShardFinalizeShutdownCommand Instance = new EntityShardFinalizeShutdownCommand(); }

        // \todo [petri] enable attribute when supported on interfaces
        //[MetaSerializable]
        public interface IRoutedMessage
        {
            EntityId                    TargetEntityId  { get; }
            MetaSerialized<MetaMessage> Message         { get; }
            EntityId                    FromEntityId    { get; }
        }

        [MetaSerializable]
        public class CastMessage : IRoutedMessage
        {
            [MetaMember(1)] public EntityId                     TargetEntityId  { get; private set; }
            [MetaMember(2)] public MetaSerialized<MetaMessage>  Message         { get; private set; }
            [MetaMember(3)] public EntityId                     FromEntityId    { get; private set; }

            public CastMessage() { }
            public CastMessage(EntityId targetEntityId, MetaSerialized<MetaMessage> message, EntityId fromEntityId)
            {
                TargetEntityId  = targetEntityId;
                Message         = message;
                FromEntityId    = fromEntityId;
            }
        }

        public interface IEntityAsk
        {
            int Id { get; }
            EntityId TargetEntityId { get; }
            MetaSerialized<MetaMessage> Message { get; }
            EntityId FromEntityId { get; }
            TaskCompletionSource<MetaSerialized<MetaMessage>> Promise { get; }
            IActorRef OwnerShard { get; }
        }

        [MetaSerializable(MetaSerializableFlags.ImplicitMembers)]
        [MetaImplicitMembersRange(1, 100)]
        public class EntityAsk : IEntityAsk, IRoutedMessage
        {
            public int                                                  Id              { get; set; } // Unique identifier (in context of sending entity)
            public EntityId                                             TargetEntityId  { get; private set; }
            public MetaSerialized<MetaMessage>                          Message         { get; private set; }
            public EntityId                                             FromEntityId    { get; private set; }
            [IgnoreDataMember]
            public TaskCompletionSource<MetaSerialized<MetaMessage>>    Promise         { get; set; } // \note only kept on local node
            [IgnoreDataMember]
            public IActorRef                                            OwnerShard      { get; set; } // Owning ShardEntity actor (for routing reply directly to)

            public EntityAsk() { }
            public EntityAsk(EntityId targetEntityId, MetaSerialized<MetaMessage> message, EntityId fromEntityId, TaskCompletionSource<MetaSerialized<MetaMessage>> promise)
            {
                TargetEntityId  = targetEntityId;
                Message         = message;
                FromEntityId    = fromEntityId;
                Promise         = promise;
            }
        }

        /// <summary>
        /// Stronger-typed wrapper for <see cref="EntityAsk"/>, used in <see cref="EntityAskHandlerAttribute"/>
        /// methods when the request type is defined with the <see cref="EntityAskRequest{TResponse}"/> base type.
        /// <para>
        /// An init-time check ensures that <typeparamref name="TReply"/> matches the <c>TResponse</c> for the
        /// request parameter of the <c>[EntityAskHandler]</c> method,
        /// and static typing ensures the response given to <see cref="EntityActor.ReplyToAsk{TResult}"/>
        /// matches <typeparamref name="TReply"/>.
        /// </para>
        /// </summary>
        public class EntityAsk<TReply> : IEntityAsk where TReply : EntityAskResponse
        {
            public EntityAsk UntypedAsk;

            internal EntityAsk(EntityAsk untypedAsk)
            {
                UntypedAsk = untypedAsk;
            }

            public int Id => UntypedAsk.Id;
            public EntityId TargetEntityId => UntypedAsk.TargetEntityId;
            public MetaSerialized<MetaMessage> Message => UntypedAsk.Message;
            public EntityId FromEntityId => UntypedAsk.FromEntityId;
            public TaskCompletionSource<MetaSerialized<MetaMessage>> Promise => UntypedAsk.Promise;
            public IActorRef OwnerShard => UntypedAsk.OwnerShard;
        }

        /// <summary>
        /// Details of any exception that happens during the handling of an EntityAsk, so the error can be propagated back to the caller.
        /// </summary>
        [MetaMessage(MessageCodesCore.RemoteEntityError, MessageDirection.ServerInternal)]
        public class RemoteEntityError : MetaMessage
        {
            public EntityAskExceptionBase Payload { get; private set; }
            private RemoteEntityError() { }
            public RemoteEntityError(EntityAskExceptionBase payload)
            {
                Payload = payload;
            }
        }

        /// <summary>
        /// A stringly-typed container for propagating information about unexpected errors in entity ask handlers.
        /// <para>
        /// When an exception not of type EntityAskError is thrown by an entity ask handler, the corresponding actor is terminated and
        /// the exception information is propagated to the caller via extracting information into an UnexpectedEntityAskError about
        /// the exception for debugging needs.
        /// </para>
        /// </summary>
        [MetaSerializableDerived(100)]
        public class UnexpectedEntityAskError : EntityAskExceptionBase
        {
            [MetaMember(1)] public string   ExceptionType       { get; private set; }   // Type of the exception thrown in the handler
            [MetaMember(2)] public string   HandlerStackTrace   { get; private set; }   // Stack trace of the exception thrown in the handler
            [MetaMember(3)] public string   HandlerErrorMessage { get; private set; }   // Message of the exception thrown in the handler
            [MetaMember(4)] public EntityId CrashedEntity       { get; private set; }
            [MetaMember(5)] public string   HandlerMethod       { get; private set; }   // The handler type. Usually "EntityAsk handler", or "OnNewSubscriber"

            UnexpectedEntityAskError() {}
            UnexpectedEntityAskError(string exceptionType, string handlerStackTrace, string handlerErrorMessage, EntityId crashedEntity, string method)
            {
                ExceptionType       = exceptionType;
                HandlerStackTrace   = handlerStackTrace;
                HandlerErrorMessage = handlerErrorMessage;
                CrashedEntity       = crashedEntity;
                HandlerMethod       = method;
            }
            public UnexpectedEntityAskError(EntityId crashedEntity, string method, Exception ex) : this(ex.GetType().ToGenericTypeString(), EntityStackTraceUtil.ExceptionToEntityStackTrace(ex), ex.Message, crashedEntity, method)
            {
            }

            public override string Message => $"{HandlerMethod} on {CrashedEntity} threw unexpected exception {ExceptionType}: {HandlerErrorMessage}\n{HandlerStackTrace}";
        }

        /// <summary>
        /// Error for when target entity crashed after the message was put to the entity's inbox but before the message was processed.
        /// </summary>
        [MetaSerializableDerived(101)]
        public class EntityCrashedError : EntityAskExceptionBase
        {
            [MetaMember(1)] public string ExceptionType       { get; private set; }   // Type of the exception thrown in the handler
            [MetaMember(2)] public string HandlerStackTrace   { get; private set; }   // Stack trace of the exception thrown in the handler
            [MetaMember(3)] public string HandlerErrorMessage { get; private set; }   // Message of the exception thrown in the handler

            EntityCrashedError() {}
            public EntityCrashedError(Exception ex)
            {
                ExceptionType       = ex.GetType().ToGenericTypeString();
                HandlerStackTrace   = EntityStackTraceUtil.ExceptionToEntityStackTrace(ex);
                HandlerErrorMessage = ex.Message;
            }

            public override string Message => $"Target entity crashed with {ExceptionType}: {HandlerErrorMessage}\n{HandlerStackTrace}";
        }

        /// <summary>
        /// Error for when message couldn't be delivered to the target entity. This can happen due to a network issue, or by losing connection to
        /// the destination node.
        /// </summary>
        [MetaSerializableDerived(102)]
        [MetaAllowNoSerializedMembers]
        public class EntityUnreachableError : EntityAskExceptionBase
        {
            [MetaMember(1)] public EntityId TargetEntity { get; private set; }

            EntityUnreachableError() { }
            public EntityUnreachableError(EntityId targetEntity)
            {
                TargetEntity = targetEntity;
            }

            public override string Message => $"Could not deliver message to the target entity {TargetEntity}. This is either a network issue or target entity crashing while message was put to its inbox but not yet processed.";
        }

        [MetaSerializable]
        public class EntityAskReply : IRoutedMessage
        {
            [MetaMember(1)] public int                          AskId           { get; private set; } // Unique identifier (in context of sending entity), same as EntityAsk.Id
            [MetaMember(2)] public EntityId                     TargetEntityId  { get; private set; } // EntityId where to send this message (the original EntityAsk caller)
            [MetaMember(3)] public MetaSerialized<MetaMessage>  Message         { get; private set; } // Payload message of the reply (contains an EntityAskReplyException if ask failed at target)
            [MetaMember(4)] public EntityId                     FromEntityId    { get; private set; } // Source EntityId of the reply (ie, the EntityAsk's target)

            private EntityAskReply() { }
            private EntityAskReply(int askId, EntityId targetEntityId, MetaSerialized<MetaMessage> message, EntityId fromEntityId)
            {
                AskId           = askId;
                TargetEntityId  = targetEntityId;
                Message         = message;
                FromEntityId    = fromEntityId;
            }

            public static EntityAskReply Success(int askId, EntityId targetEntityId, EntityId fromEntityId, MetaSerialized<MetaMessage> message) =>
                new EntityAskReply(askId, targetEntityId, message, fromEntityId);

            public static EntityAskReply FailWithError(int askId, EntityId targetEntityId, EntityId fromEntityId, EntityAskExceptionBase ex) =>
                new EntityAskReply(askId, targetEntityId, message: new MetaSerialized<MetaMessage>(new RemoteEntityError(ex), MetaSerializationFlags.IncludeAll, logicVersion: null), fromEntityId);
        }

        /// <summary>
        /// The phase of the shard lifecycle. Follows the following state machine:
        /// <code>
        /// Created
        ///    |
        ///    |- InitializeAsync()
        ///    |
        ///    |'----------------------------------.
        ///    V                                   |
        /// Initialized                            |
        ///    |     (shutdown before start)       |
        ///    |'--------------------------.       |
        ///    |                           |       |
        ///    V                           |       |
        /// Starting                       |       |
        ///    |                           |       |
        ///    |'----------------.         |       |
        ///    |                 |         |       |
        ///    |- StartAsync()   |         |       V
        ///    |                 |         |  ( Error during init )
        ///    |'------------.   |         |       |
        ///    |             |   |         |       V
        ///    |             V   V         |  InitializeFailed
        ///    |   ( Error during start )  |       |
        ///    |             |             |       |
        ///    |             V             |       |
        ///    |           StartingFailed  |       |
        ///    |                       |   |       |
        ///    |'-----.                |   |       |
        ///    |      |                |   |       |
        ///    V      |                |   |       |
        /// Running   |                |   |       |
        ///    |      |                |   |       |
        ///    V      V                V   V       V
        ///  (       Shutdown message        )   ( Shutdown message )
        ///    |                                   |
        ///    V                                   |
        /// Stopping                               |
        ///    |                                   |
        ///    |- OnShutdown()                     |
        ///    |                                   |
        ///    | .---------------------------------'
        ///    V V
        /// Stopped
        /// </code>
        /// </summary>
        public enum ShardLifecyclePhase
        {
            /// <summary>
            /// Shard has been created, and it will deliver messages to entities if such are received from other shards. "AlwaysRunningEntities" are not
            /// necessarily up yet.
            /// </summary>
            Created,

            /// <summary>
            /// Shard has completed InitializeAsync.
            /// </summary>
            Initialized,

            /// <summary>
            /// Shard failed to run InitializeAsync.
            /// </summary>
            InitializeFailed,

            /// <summary>
            /// Shard is starting up. It is in the process of ensuring all "AlwaysRunningEntities" are up.
            /// </summary>
            Starting,

            /// <summary>
            /// Shard could not ensure initial AlwaysRunningEntities. Running entities are being shut down, and no new entities are started.
            /// </summary>
            StartingFailed,

            /// <summary>
            /// Shard is running. The AlwaysRunningEntities have been up at least for an instant during the initialization.
            /// </summary>
            Running,

            /// <summary>
            /// Shard is shutting down. Running entities are being shut down, and no new entities are started.
            /// </summary>
            Stopping,

            /// <summary>
            /// Shard has shut down. No entities are running.
            /// </summary>
            Stopped,
        }

        protected class EntityAskState
        {
            public TaskCompletionSource<MetaSerialized<MetaMessage>> Promise { get; private set; }

            public EntityAskState(TaskCompletionSource<MetaSerialized<MetaMessage>> promise)
            {
                Promise = promise;
            }
        }

        protected class EntitySyncState
        {
            public readonly int                                 LocalChannelId;
            public int                                          RemoteChannelId; // -1 until remote replies with the channel
            public readonly EntityId                            LocalEntityId;
            public readonly EntityId                            RemoteEntityId;
            public TaskCompletionSource<int>                    OpeningPromise; // delivers channel id to entity
            public ChannelWriter<MetaSerialized<MetaMessage>>   WriterPeer;
            public bool                                         IsRemoteClosed;

            public EntitySyncState(int localChannelId, int remoteChannelId, EntityId localEntityId, EntityId remoteEntityId, TaskCompletionSource<int> openingPromise, ChannelWriter<MetaSerialized<MetaMessage>> writerPeer)
            {
                LocalChannelId = localChannelId;
                RemoteChannelId = remoteChannelId;
                LocalEntityId = localEntityId;
                RemoteEntityId = remoteEntityId;
                OpeningPromise = openingPromise;
                WriterPeer = writerPeer;
                IsRemoteClosed = false;
            }
        }

        protected partial class EntityState
        {
            public EntityId                         EntityId;
            public IActorRef                        Actor;
            public int                              IncarnationId;
            public EntityStatus                     Status                  = EntityStatus.Starting;
            public List<object>                     PendingMessages         = new List<object>();
            public int                              EntityAskRunningId      = 1;
            public Dictionary<int, EntityAskState>  EntityAsks              = new Dictionary<int, EntityAskState>(); // \todo [petri] asks only removed when response is received
            public int                              EntitySyncRunningId     = 1;
            public Dictionary<int, EntitySyncState> EntitySyncs             = new Dictionary<int, EntitySyncState>(); // local channel id -> Sync.

            /// <summary>
            /// The exception that caused entity to crash.
            /// </summary>
            public Exception TerminationReason = null;

            /// <summary>
            /// The point in time when the entity was requested to shut down. If the shutdown request
            /// hasn't been sent yet, <see cref="DateTime.MinValue"/>.
            /// </summary>
            public DateTime EntityShutdownStartedAt = DateTime.MinValue;

            /// <summary>
            /// When the throttle period ends and throttle count is reset.
            /// </summary>
            DateTime _destinationShardMissingLogPeriodEndsAt = DateTime.MinValue;

            /// <summary>
            /// How many logs have been attempted this cycle.
            /// </summary>
            int _numDestinationShardMissingLogsThisPeriod;

            public EntityState(EntityId entityId, IActorRef actor, int incarnationId)
            {
                EntityId        = entityId;
                Actor           = actor;
                IncarnationId   = incarnationId;
            }

            /// <summary>
            /// Returns the log level of this entity's this log message about destination missing should be logged at.
            /// This method assumes a log message is written for each call of this method, and keeps track of how many
            /// log messages this entity has written recently. If an entity keeps causing many warnings in a short amount
            /// of time, the log level is decreased.
            /// </summary>
            public LogLevel TriggerAndGetThrottledLogLevelForDestinationShardMissing()
            {
                const int numLogAllowedPerCycle = 5;
                TimeSpan cycleLength = TimeSpan.FromSeconds(5);

                DateTime now = DateTime.UtcNow;
                bool isThottled;
                if (now > _destinationShardMissingLogPeriodEndsAt)
                {
                    _destinationShardMissingLogPeriodEndsAt = now + cycleLength;
                    _numDestinationShardMissingLogsThisPeriod = 1;
                    isThottled = false;
                }
                else
                {
                    _numDestinationShardMissingLogsThisPeriod += 1;
                    isThottled = _numDestinationShardMissingLogsThisPeriod > numLogAllowedPerCycle;
                }
                return isThottled ? LogLevel.Verbose : LogLevel.Warning;
            }
        }

        // Metrics
        static readonly Prometheus.Counter  c_entitySpawned     = Prometheus.Metrics.CreateCounter("game_entity_started_total", "Cumulative number of entities started (by type)", "entity");
        static readonly Prometheus.Counter  c_entityTerminated  = Prometheus.Metrics.CreateCounter("game_entity_terminated_total", "Cumulative number of entities terminated (by type)", "entity");
        static readonly Prometheus.Gauge    c_entityActive      = Prometheus.Metrics.CreateGauge("game_entity_active", "Cumulative number of entities active currently (by type)", "entity");

        // Members
        protected readonly string                       _nodeName;
        protected readonly ClusterConfig                _clusterConfig;
        protected readonly EntityShardId                _selfShardId;   // Value is -1 for proxy shards (not part of sharding) and [0..N) for shard nodes.
        protected readonly EntityConfigBase             _entityConfig;
        protected readonly Func<EntityId, Props>        _entityProps;
        protected readonly EntityShardResolver          _shardResolver;

        protected EntityKind                            EntityKind  => _selfShardId.Kind;

        protected HashSet<EntityId>                     _alwaysRunningEntities = new HashSet<EntityId>();   // Entities that should always be running

        ShardLifecyclePhase                             _lifecyclePhase;
        bool                                            _initializationCompleted;
        protected IActorRef                             _shutdownSender;
        protected IActorRef                             _currentWaitUntilRunningSender;
        protected Dictionary<EntityId, EntityState>     _entityStates   = new Dictionary<EntityId, EntityState>();
        protected Dictionary<IActorRef, EntityState>    _entityByActor  = new Dictionary<IActorRef, EntityState>();

        static readonly TimeSpan                        UpdateTickInterval  = TimeSpan.FromMilliseconds(2000);
        ICancelable                                     _cancelUpdateTimer  = new Cancelable(Context.System.Scheduler);
        List<Exception>                                 _shardStartupErrors = new List<Exception>();

        int                                             _entityIncarnationRunningId = 1;

        // Entity shutdown throttling

        /// <inheritdoc cref="EntityConfigBase.MaxConcurrentEntityShutdownsPerShard"/>
        readonly int                                    _maxConcurrentEntityShutdowns;
        int                                             _numOngoingShutdowns;
        OrderedSet<EntityId>                            _deferredShutdowns = new OrderedSet<EntityId>();

        /// <summary>
        /// The current cluster state. To detect changes, see <see cref="OnClusterChanged(ClusterConnectionManager.ClusterChangedEvent)"/> hook.
        /// This value is null during early init and in constructor, but guaranteed to be not-null when <see cref="InitializeAsync"/> is called.
        /// </summary>
        protected ClusterConnectionManager.ClusterChangedEvent CurrentClusterState { get; private set; }

        public EntityShard(EntityShardConfig shardConfig)
        {
            ClusteringOptions clusterOpts = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>();
            _nodeName       = clusterOpts.SelfAddress.ToString();
            _clusterConfig  = clusterOpts.ClusterConfig;
            _selfShardId    = shardConfig.Id;
            _entityConfig   = EntityConfigRegistry.Instance.GetConfig(shardConfig.Id.Kind);
            _entityProps    = _entityConfig.AllowEntitySpawn ? (EntityId entityId) => Props.Create(_entityConfig.GetActorTypeForEntity(entityId), entityId) : null;
            _shardResolver  = EntityShardResolver.Get(Context.System);

            // Resolve which entities should always be running locally
            IReadOnlyList<EntityId> autoSpawnEntityIds = _entityConfig.ShardingStrategy.GetAutoSpawnEntities(_selfShardId);
            if (autoSpawnEntityIds != null)
            {
                foreach (EntityId entityId in autoSpawnEntityIds)
                    _alwaysRunningEntities.Add(entityId);
            }

            // Register message handlers
            RegisterHandlers();

            _lifecyclePhase = ShardLifecyclePhase.Created;
            _maxConcurrentEntityShutdowns = _entityConfig.MaxConcurrentEntityShutdownsPerShard;
        }

        bool IsStartingNewEntitiesAllowed =>
               _lifecyclePhase == ShardLifecyclePhase.Initialized   // Entities created for messages received before Shard is started but after successfully initializing.
            || _lifecyclePhase == ShardLifecyclePhase.Starting      // Entities created for AlwaysRunningEntities and for messages received before AlwaysRunningEntities have completed init.
            || _lifecyclePhase == ShardLifecyclePhase.Running;      // Entities created for messages received, and AlwaysRunningEntities restarts

        bool AreAlwaysRunningEntitiesRunning()
        {
            foreach (EntityId entityId in _alwaysRunningEntities)
            {
                if (!_entityStates.TryGetValue(entityId, out EntityState entity))
                    return false;
                if (entity.Status != EntityStatus.Running)
                    return false;
            }
            return true;
        }

        void RegisterHandlers()
        {
            ReceiveAsync<InitializeShard>(ReceiveInitializeShard);
            ReceiveAsync<StartShard>(ReceiveStartShardAsync);
            Receive<ShardWaitUntilRunningRequest>(ReceiveShardWaitUntilRunningRequest);
            Receive<ActorTick>(ReceiveActorTick);
            Receive<EntityReady>(ReceiveEntityReady);
            Receive<EntityShutdownRequest>(ReceiveEntityShutdownRequest);
            Receive<EntitySuspendRequest>(ReceiveEntitySuspendRequest);
            Receive<EntityResumeRequest>(ReceiveEntityResumeRequest);
            Receive<CastMessage>(ReceiveCastMessage);
            Receive<EntityAsk>(ReceiveEntityAsk);
            Receive<EntityAskReply>(ReceiveEntityAskReply);
            Receive<WatchedEntityTerminated>(ReceiveWatchedEntityTerminated);
            Receive<Terminated>(ReceiveTerminated);
            Receive<ClusterConnectionManager.ClusterChangedEvent>(ReceiveClusterChangedEvent);
            Receive<ShutdownSync>(ReceiveShutdownSync);
            ReceiveAsync<EntityShardFinalizeShutdownCommand>(ReceiveEntityShardFinalizeShutdownCommand);
            Receive<EntitySynchronizationE2SBeginRequest>(ReceiveEntitySynchronizationE2SBeginRequest);
            Receive<EntitySynchronizationS2SBeginRequest>(ReceiveEntitySynchronizationS2SBeginRequest);
            Receive<EntitySynchronizationS2EBeginResponse>(ReceiveEntitySynchronizationS2EBeginResponse);
            Receive<EntitySynchronizationS2SBeginResponse>(ReceiveEntitySynchronizationS2SBeginResponse);
            Receive<EntitySynchronizationE2SChannelMessage>(ReceiveEntitySynchronizationE2SChannelMessage);
            Receive<EntitySynchronizationS2SChannelMessage>(ReceiveEntitySynchronizationS2SChannelMessage);
            RegisterPubSubHandlers();
        }

        /// <summary>
        /// Called when immeditely after constructor.
        /// Throwing an exception will fail the shard startup and the application will shut down.
        /// You may not start entities in this method. To spawn entities, see <see cref="StartAsync"/>.
        /// Default implementation does nothing.
        /// </summary>
        protected virtual Task InitializeAsync() => Task.CompletedTask;

        async Task ReceiveInitializeShard(InitializeShard _)
        {
            // Allow start only once.
            // Note that a shard can fail already before the start.
            if (_lifecyclePhase != ShardLifecyclePhase.Created)
            {
                _log.Warning("Received InitializeShard in an unexpected shard phase: {Phase}", _lifecyclePhase);
                return;
            }

            try
            {
                // Subscribe cluster changes
                CurrentClusterState = await ClusterConnectionManager.SubscribeToClusterEventsAsync(_self);

                // Setup cluster state so Initialize can access it.
                if (CurrentClusterState == null)
                    throw new InvalidOperationException("Attempted to start entity shard without there being a formed cluster");
                OnClusterChangedInternal(CurrentClusterState);

                await InitializeAsync();

                _lifecyclePhase = ShardLifecyclePhase.Initialized;
                _initializationCompleted = true;
            }
            catch (Exception ex)
            {
                // any Failure terminates the shard. No point in continuing.
                _lifecyclePhase = ShardLifecyclePhase.InitializeFailed;
                AppendStartingFailure(ex);
                return;
            }

            OnClusterChanged(CurrentClusterState);
        }

        /// <summary>
        /// Called when shard is started in coordination with the rest of the cluster.
        /// The default implementation starts always running entities.
        /// </summary>
        protected virtual Task StartAsync()
        {
            // Spawn local always-running entities
            foreach (EntityId entityId in _alwaysRunningEntities)
            {
                EntityState entity = GetOrSpawnEntity(entityId, out SpawnFailure failureReason);
                if (entity == null)
                    throw new InvalidOperationException($"Failed to start service entity {entityId} (on {_selfShardId}): {failureReason}");
            }

            return Task.CompletedTask;
        }

        async Task ReceiveStartShardAsync(StartShard _)
        {
            // Allow start only once.
            if (_lifecyclePhase != ShardLifecyclePhase.Initialized)
            {
                // If the shard has failed already before the start, we don't warn about that. The next
                // poll to fetch the shard error will tell the caller there is an error:
                //   * InitializeFailed if InitializeAsync started()
                //   * StartingFailed if always-running entity crashed after being woken up to handle a message from another shard before Start was called.
                if (_lifecyclePhase != ShardLifecyclePhase.InitializeFailed && _lifecyclePhase != ShardLifecyclePhase.StartingFailed)
                    _log.Warning("Received StartShard in an unexpected shard phase: {Phase}", _lifecyclePhase);
                return;
            }
            _lifecyclePhase = ShardLifecyclePhase.Starting;

            try
            {
                await StartAsync();
            }
            catch (Exception ex)
            {
                // any Failure terminates the shard. No point in continuing.
                _lifecyclePhase = ShardLifecyclePhase.StartingFailed;
                AppendStartingFailure(ex);
                NotifyShardStartingComplete();

                RequestShutdownAllEntities();

                return;
            }

            // If there are no pending entities or they have been completed already, the shard is Running immediately
            // Entities can be running already if another shard woke up the entity before the init was called).
            if (AreAlwaysRunningEntitiesRunning())
            {
                _lifecyclePhase = ShardLifecyclePhase.Running;
                NotifyShardStartingComplete();
            }
        }

        /// <summary>
        /// Completes with true when shard is running.
        /// Completes with false if reached internal timeout while waiting for shard. Shard status is still starting.
        /// Completes with <see cref="EntityShardStartException"/> error if shard failed to initialize or start.
        /// Completes with <see cref="EntityShardIllegalStartStateException"/> if shard is not in a state where waiting for Runnins is meaningful.
        /// </summary>
        public static async Task<bool> TryWaitUntilRunning(IActorRef actor)
        {
            try
            {
                ShardWaitUntilRunningResponse response = await actor.Ask<ShardWaitUntilRunningResponse>(ShardWaitUntilRunningRequest.Instance, timeout: TimeSpan.FromSeconds(10));

                switch (response.Phase)
                {
                    case ShardLifecyclePhase.InitializeFailed:
                    case ShardLifecyclePhase.StartingFailed:
                        throw new EntityShardStartException(response.ShardId, response.InitErrors);
                    case ShardLifecyclePhase.Running:
                        return true;

                    case ShardLifecyclePhase.Stopping:
                    case ShardLifecyclePhase.Stopped:
                    default:
                        throw new EntityShardIllegalStartStateException(response.ShardId, $"Entity shard {response.ShardId} state {response.Phase} was not initial state");
                }
            }
            catch(AskTimeoutException)
            {
                return false;
            }
        }

        void ReceiveShardWaitUntilRunningRequest(ShardWaitUntilRunningRequest req)
        {
            // \note: We might assign over some pre-existing sender. However, that only happens
            //        if ShardWaitUntilRunningRequest is asked multiple times. That only happens
            //        if the previous Ask has timed out. Repling to timed-out asks is not needed
            //        and hence we can only reply to the latest Sender.
            _currentWaitUntilRunningSender = Sender;

            // If we are still starting, delay answering until phase is complete
            if (_lifecyclePhase == ShardLifecyclePhase.Starting)
                return;

            // Otherwise notify immediately
            NotifyShardStartingComplete();
        }

        /// <summary>
        /// Notifies any waiters of the EntityShard Init completion.
        /// Should be called just after lifecycle phase has exited <see cref="ShardLifecyclePhase.Starting"/>.
        /// </summary>
        void NotifyShardStartingComplete()
        {
            _currentWaitUntilRunningSender?.Tell(new ShardWaitUntilRunningResponse(_selfShardId, _lifecyclePhase, _shardStartupErrors.ToArray()));
            _currentWaitUntilRunningSender = null;
        }

        void ReceiveActorTick(ActorTick tick)
        {
            // Publish shard entity counts (handled by StatsCollectorProxy)
            ClusteringOptions clusterOpts = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>();
            Context.System.EventStream.Publish(new UpdateShardLiveEntityCount(clusterOpts.SelfAddress, EntityKind, _entityStates.Count));

            // DEBUG DEBUG: Dump all watch states
            //if (_entityWatches.Count > 0)
            //{
            //    StringBuilder sb = new StringBuilder();
            //    foreach ((EntityId fromEntityId, HashSet<EntityId> watchers) in _entityWatches)
            //        sb.AppendLine($"  {fromEntityId}: {string.Join(", ", watchers)}");
            //    sb.Length -= 1;
            //    _log.Info("Watchers:\n{0}", sb.ToString());
            //}

            // Metrics
            c_entityActive.WithLabels(EntityKind.ToString()).Set(_entityStates.Count);
        }

        void ReceiveEntityReady(EntityReady ready)
        {
            if (!_entityByActor.TryGetValue(Sender, out EntityState entity))
            {
                _log.Warning("Got EntityReady for unknown child: {Actor}", Sender);
                return;
            }

            if (entity.Status == EntityStatus.Starting)
            {
                // Set status to Running & store sequence number
                //_log.Debug("Entity {0} ready for action, {1} pending messages", ready.EntityId, entity.PendingMessages.Count);
                entity.Status = EntityStatus.Running;

                // Flush all pending messages
                FlushPendingMessagesToLocalEntity(entity);

                // If this is one of the initial entities, the shard may be ready now. Check it.
                if (_lifecyclePhase == ShardLifecyclePhase.Starting && _alwaysRunningEntities.Contains(entity.EntityId))
                {
                    if (AreAlwaysRunningEntitiesRunning())
                    {
                        _lifecyclePhase = ShardLifecyclePhase.Running;
                        NotifyShardStartingComplete();
                    }
                }
            }
            else if (entity.Status == EntityStatus.Stopping)
            {
                // ignored: can end up here, if entity is immediately terminated upon starting (eg, short-lived connections from LB)
                //          or if shard shutdown is started after entity init is started but not yet completed.
            }
            else
                _log.Warning("Got EntityReady for {EntityId} in invalid state {Status}", entity.EntityId, entity.Status);
        }

        void ReceiveEntityShutdownRequest(EntityShutdownRequest shutdown)
        {
            if (!_entityByActor.TryGetValue(Sender, out EntityState entity))
            {
                _log.Warning("Got EntityRequestShutdown for unknown child: {Actor}", Sender);
                return;
            }

            // \note allow shutdown during any state (including Starting) & allow double-stopping
            RequestEntityShutdown(entity);
        }

        void ReceiveEntitySuspendRequest(EntitySuspendRequest suspend)
        {
            if (!_entityByActor.TryGetValue(Sender, out EntityState entity))
            {
                _log.Warning("Got EntitySuspendRequest for unknown child: {Actor}", Sender);
                return;
            }

            if (entity.Status == EntityStatus.Stopping)
            {
                // If the entity is shutting down, the shutdown request is already being delivered.
                // No point replying.
            }
            else if (entity.Status == EntityStatus.Running)
            {
                // suspend, and let the entity know
                entity.Status = EntityStatus.Suspended;
                entity.Actor.Tell(EntitySuspendEvent.Instance);
            }
            else
                _log.Warning("Got EntitySuspendRequest for {EntityId} in invalid state {Status}", entity.EntityId, entity.Status);
        }

        void ReceiveEntityResumeRequest(EntityResumeRequest resume)
        {
            if (!_entityByActor.TryGetValue(Sender, out EntityState entity))
            {
                _log.Warning("Got EntityResumeRequest for unknown child: {Actor}", Sender);
                return;
            }

            if (entity.Status == EntityStatus.Stopping)
            {
                // If the entity is shutting down, the shutdown request is already being delivered.
            }
            else if (entity.Status == EntityStatus.Suspended)
            {
                // Mark entity as Running
                entity.Status = EntityStatus.Running;

                // Flush all pending messages
                FlushPendingMessagesToLocalEntity(entity);
            }
            else
                _log.Warning("Got EntityResumeRequest for {EntityId} in invalid state {Status}", entity.EntityId, entity.Status);
        }

        /// <summary>
        /// Handle an outgoing <see cref="CastMessage"/>. First handled by the source Entity's parent EntityShard,
        /// then routed onwards to target entity's parent EntityShard, which routes it locally to the target Entity.
        /// </summary>
        /// <param name="cast"></param>
        void ReceiveCastMessage(CastMessage cast)
        {
            //_log.Debug("Handle CastMessage from {0} to {1}: {2}", Sender, cast.TargetEntityId, MetaSerializationUtil.PeekMessageName(cast.Message));

            // Check that source is valid
            if (!cast.FromEntityId.IsValid)
            {
                _log.Error("Received CastMessage from invalid entity {FromEntityId}: {MessageName}", cast.FromEntityId, MetaSerializationUtil.PeekMessageName(cast.Message));
                return;
            }

            // Check that targetEntityId is valid
            if (!cast.TargetEntityId.IsValid)
            {
                _log.Error("Trying to send an CastMessage message to invalid entity {TargetEntityId} (from {FromEntityId}): {MessageName}", cast.TargetEntityId, cast.FromEntityId, MetaSerializationUtil.PeekMessageName(cast.Message));
                return;
            }

            // Route message onwards (either locally or to remote shard)
            RouteMessage(cast);
        }

        /// <summary>
        /// Handle an <see cref="EntityAsk"/>. First handled when an Entity sends this to its
        /// owning EntityShard (ask.Promise != null), where the state of the EntityAsk is stored in Entity's
        /// metadata, for handling the response. After this, the EntityShard can forward the EntityAsk
        /// forward to the target Entity's EntityShard.
        /// The target Entity's parent EntityShard will finally route the message to the Entity itself.
        /// </summary>
        /// <param name="ask"></param>
        void ReceiveEntityAsk(EntityAsk ask)
        {
            //_log.Debug("Handle EntityAsk<{0}> #{1} to {2}: {3} promise", MetaSerializationUtil.PeekMessageName(ask.Message), ask.Id, ask.TargetEntityId, ask.Promise != null ? "has" : "no");

            // Check that targetEntityId is valid
            if (!ask.TargetEntityId.IsValid)
            {
                _log.Error("Trying to send an EntityAsk message to invalid entity {TargetEntityId} (from {FromEntityId}): {MessageName}", ask.TargetEntityId, ask.FromEntityId, MetaSerializationUtil.PeekMessageName(ask.Message));
                return;
            }

            // If ask.Promise != null, we just received the EntityAsk from the originating Entity (our child)
            if (ask.Promise != null)
            {
                // Check that the Entity is a known child of ours
                if (!_entityByActor.TryGetValue(Sender, out EntityState askingEntity))
                {
                    // Source Entity unknown, ignore message
                    _log.Warning("Received EntityAsk from unknown entity {Actor} (entity {FromEntityId}) for {TargetEntityId}: {MessageType}, ignoring it", Sender, ask.FromEntityId, ask.TargetEntityId, MetaSerializationUtil.PeekMessageName(ask.Message));
                    return;
                }

                // Store the promise for handling the response
                ask.Id = askingEntity.EntityAskRunningId++;
                askingEntity.EntityAsks.Add(ask.Id, new EntityAskState(ask.Promise));

                // Clear the promise (we've captured it already) and mark ourselves as the source
                ask.Promise = null;
                ask.OwnerShard = _self;

                // Route message onwards
                RouteMessage(ask);
            }
            else
            {
                // Received from remote shard, use Sender as Owner
                ask.OwnerShard = Sender;

                // Route message onwards
                RouteMessage(ask);
            }
        }

        /// <summary>
        /// Handle a reply to an <see cref="EntityAsk"/>. The source Entity sends it directly to the asking
        /// Entity's parent EntityShard, so we know it always gets delivered locally. The delivery is done
        /// by resolving EntityAsk's promise, thus no more messaging is done and the replies are not ordered
        /// with other messaging.
        /// </summary>
        /// <param name="reply"></param>
        void ReceiveEntityAskReply(EntityAskReply reply)
        {
            //_log.Debug("Routing EntityAskReply from {FromEntityId} to {TargetEntityId}: {MessageName}", reply.FromEntityId, reply.TargetEntityId, MetaSerializationUtil.PeekMessageName(reply.Message));

            // Check that source is valid
            if (!reply.FromEntityId.IsValid)
            {
                _log.Error("Received EntityAskReply from invalid entity {FromEntityId}: {MessageName}", reply.FromEntityId, MetaSerializationUtil.PeekMessageName(reply.Message));
                return;
            }

            // Check that target entity's kind matches this shard's kind
            if (reply.TargetEntityId.Kind != EntityKind)
            {
                _log.Error("Received EntityAskReply for entity {TargetEntityId} of invalid kind, this shard expects {ExpectedEntityKind}", reply.TargetEntityId, EntityKind);
                return;
            }

            // Check that destination is valid
            // \note: AskReply is sent directly to the destination shard so this should always be local
            if (!_entityStates.TryGetValue(reply.TargetEntityId, out EntityState entity))
            {
                _log.Warning("Received EntityAskReply for unknown entity {TargetEntityId} (from {FromEntityId}): {MessageName}", reply.TargetEntityId, reply.FromEntityId, MetaSerializationUtil.PeekMessageName(reply.Message));
                return;
            }

            // Route the response (or error) to the awaiting Task
            int typeCode = MetaSerializationUtil.PeekMessageTypeCode(reply.Message);
            if (entity.EntityAsks.Remove(reply.AskId, out EntityAskState askState))
            {
                if (typeCode == MessageCodesCore.RemoteEntityError)
                {
                    RemoteEntityError error = (RemoteEntityError)reply.Message.Deserialize(resolver: null, logicVersion: null);
                    //_log.Debug("Delivering failed EntityAskReply to {CallerEntityId} (#{AskId}) with {ExceptionType}: {ErrorMessage}\n{StackTrace}", reply.TargetEntityId, reply.AskId, error.ExceptionType, error.Message, error.StackTrace);
                    askState.Promise.SetException(error.Payload);
                }
                else
                {
                    //_log.Debug("Delivering successful EntityAskReply to {CallerEntityId} (#{AskId}): {ReplyMessageType}", reply.TargetEntityId, reply.AskId, MetaSerializationUtil.PeekMessageName(reply.Message));
                    askState.Promise.SetResult(reply.Message); // \note passing on serialized version of message
                }
            }
            else
            {
                _log.Warning("Received response to unknown EntityAsk #{AskId}: {MessageType}", reply.AskId, MetaSerializationUtil.PeekMessageName(reply.Message));
            }
        }

        void ReceiveTerminated(Terminated terminated)
        {
            // Handle terminated child entity actors
            // \note: We remove the state here but watches still remain. We ware inconsistent until
            //        the end of the method.
            if (!_entityByActor.Remove(terminated.ActorRef, out EntityState entity))
            {
                _log.Warning("Received Terminated for unknown actor {Actor}", terminated.ActorRef);
                return;
            }
            _entityStates.Remove(entity.EntityId);

            EntityId entityId = entity.EntityId;
            bool shutdownIsExpected = entity.EntityShutdownStartedAt != DateTime.MinValue;

            c_entityTerminated.WithLabels(entityId.Kind.ToString()).Inc();

            // Entity crash, unexpected shutdown, or expected?
            if (entity.TerminationReason != null)
            {
                string stackTrace = EntityStackTraceUtil.ExceptionToEntityStackTrace(entity.TerminationReason);
                _log.Error("Entity {EntityId} (status={EntityStatus}, numPendingMessages={NumPendingMessages}) crashed due to {ExceptionType} '{ExceptionMessage}':\n{ExceptionStackTrace}",
                    entityId, entity.Status, entity.PendingMessages.Count, entity.TerminationReason.GetType().Name, entity.TerminationReason.Message, stackTrace);

                // If entity crashes in Starting state, it means the entity failed to Initialize(). Clear the pending messages to prevent it from restarting.
                // Otherwise, we could get into infinite retry loop with no throtting, spamming the logs.
                //
                // We terminate pending asks only in Starting phase. In Running phase, there are no pending messages. In Suspended and Stopping phases, crash
                // means the entity crashed on shutdown and there is no similar risk of an infinite retry loop.
                if (entity.Status == EntityStatus.Starting)
                {
                    EntityCrashedError error = new EntityCrashedError(entity.TerminationReason);

                    // Reply to all pending EntityAsks with the error. They haven't actually caused it, but it's better that they are informed regardless of why the actor failed.
                    ReplyToAllAsksOnActorTerminate(entity, error);

                    // End all pending subscribers and inform subscriber of the failure.
                    ReplyToAllPubsubRequestsOnActorTerminate(entity, error);

                    // Remove any pending messages
                    entity.PendingMessages.Clear();
                }

                OnChildActorCrashed(entity);
            }
            else if (!shutdownIsExpected)
            {
                _log.Error("Entity {EntityId} unexpectedly terminated (status={Status}, actor={Actor}, numPendingMessages={NumPendingMessages})", entityId, entity.Status, terminated.ActorRef, entity.PendingMessages.Count);

                OnChildActorCrashed(entity);
            }
            else
            {
                // Expected shutdown
                // \todo [jarkko]: keep metrics on shutdowns
                //    TimeSpan shutdownDuration = (DateTime.UtcNow - entity.EntityShutdownStartedAt);
                //    observe()
            }

            // Does this terminate the shard init? If required initial entities fail to start, shard fails to start.
            // \note: We check both Starting and Initialized:
            //         * Starting: Always-Running-Entities started by us
            //         * Initialized: Always-Running-Entities started before Start to handle a message from another shard.
            bool terminateShardInit = (_lifecyclePhase == ShardLifecyclePhase.Starting || _lifecyclePhase == ShardLifecyclePhase.Initialized) && _alwaysRunningEntities.Contains(entityId);
            if (terminateShardInit)
            {
                // \note: this sets IsStartingNewEntitiesAllowed to False, and the entity will not be auto restarted below
                // \note: Two different transitions:
                //         * ShardLifecyclePhase.Starting -> ShardLifecyclePhase.StartingFailed. The "normal" transition as expected.
                //         * ShardLifecyclePhase.Initialized -> ShardLifecyclePhase.StartingFailed. We skip the normal start.
                _lifecyclePhase = ShardLifecyclePhase.StartingFailed;
                AppendStartingFailure(entity.TerminationReason ?? new InvalidOperationException($"Entity {entityId} terminated unexpectedly"));
                NotifyShardStartingComplete();
            }

            // Remove weak pubsub messages. We don't want to wake entity only to be informed of a dead watch.
            RemoveWeakPubsubMessagesOnActorTerminate(entity);

            // Inform PubSub subscribers and subscription
            InformTerminatedEntityPubSubListeners(entity);

            // Update shutdown queue status
            // * Any (expected or not) shutdown removes the entity from the shutdown queue.
            // * Any expected shutdown releases one back to the semaphore
            _deferredShutdowns.Remove(entity.EntityId);
            if (shutdownIsExpected)
                _numOngoingShutdowns--;
            TryRunNextEnqueuedEntityShutdown();

            // Entities with pending messages (unless entity is transient) or always-running entities are re-started immediately
            bool shouldRestart;
            if (!IsStartingNewEntitiesAllowed)
                shouldRestart = false;
            else if (_alwaysRunningEntities.Contains(entity.EntityId))
                shouldRestart = true;
            else if (_entityProps == null) // entity is transient
                shouldRestart = false;
            else if (entity.PendingMessages.Count > 0)
                shouldRestart = true;
            else if (entity.Subscribers.Count > 0) // has pending subscribers
                shouldRestart = true;
            else
                shouldRestart = false;

            if (shouldRestart)
            {
                EntityState newEntity = GetOrSpawnEntity(entityId, out SpawnFailure failureReason);
                if (newEntity != null)
                {
                    // Migrate mailbox to the new entity.
                    newEntity.PendingMessages = entity.PendingMessages;

                    // Migrate used ID space to the new entity.
                    newEntity.EntityAskRunningId = entity.EntityAskRunningId;
                    newEntity.EntitySyncRunningId = entity.EntitySyncRunningId;

                    // Pending subscribers must be migrated. If the mailbox contains a subscribe request,
                    // we must have the state when entity responds. InformTerminatedEntityPubSubListeners
                    // removes all dead subscribers so we can just take the subscribers as is.
                    newEntity.Subscribers = entity.Subscribers;

                    // Exit. Fallthrough is for error path
                    CheckWatcherSetConsistency();
                    return;
                }
                else
                {
                    _log.Error("Failed to restart entity {EntityId}: failureReason", entityId, failureReason);

                    // fallthrough
                }
            }

            // Reply to all pending requests since the entity itself will not be woken up to reply.
            {
                EntityUnreachableError error = new EntityUnreachableError(entityId);
                ReplyToAllAsksOnActorTerminate(entity, error);
                ReplyToAllPubsubRequestsOnActorTerminate(entity, error);
                CheckWatcherSetConsistency();
            }

            // If shard became a failed shard, start cleaning up already
            if (terminateShardInit)
                RequestShutdownAllEntities();

            // Check whether ready to shutdown immediately
            UpdateShardShutdown();
        }

        /// <summary>
        /// Called after an actor has crashed or shut down unexpectedly. If actor has crashed, <see cref="EntityState.TerminationReason"/> is set.
        /// </summary>
        protected virtual void OnChildActorCrashed(EntityState entityState) { }

        void ReplyToAllAsksOnActorTerminate(EntityState entity, EntityAskExceptionBase error)
        {
            foreach (object routed in entity.PendingMessages)
            {
                if (routed is EntityAsk ask)
                {
                    Tell(ask.OwnerShard, EntityAskReply.FailWithError(ask.Id, ask.FromEntityId, entity.EntityId, error));
                }
            }
        }

        void ReceiveClusterChangedEvent(ClusterConnectionManager.ClusterChangedEvent cluster)
        {
            CurrentClusterState = cluster;
            OnClusterChangedInternal(cluster);
        }

        void OnClusterChangedInternal(ClusterConnectionManager.ClusterChangedEvent cluster)
        {
            // Update EntityShardResolver.
            _shardResolver.OnClusterChangedEvent(cluster);

            // Inform subscribers and subscriptions if their peer was lost.
            InformLocalPubSubEntitiesOnClusterUpdate(cluster);

            // Custom shard-specific hooks. But only after InitializeAsync().
            if (_initializationCompleted)
                OnClusterChanged(cluster);
        }

        /// <summary>
        /// Called when <see cref="ClusterConnectionManager.ClusterChangedEvent"/> is received after <see cref="InitializeAsync"/>.
        /// The base class subscribes to and handles the events automatically.
        /// This method is also called immediately after <see cref="InitializeAsync"/> to inform implementation of any changes happened
        /// before Initialize was called.
        /// <para>
        /// See also <seealso cref="CurrentClusterState"/>.
        /// </para>
        /// </summary>
        protected virtual void OnClusterChanged(ClusterConnectionManager.ClusterChangedEvent cluster) { }

        void ReceiveShutdownSync(ShutdownSync s)
        {
            _log.Info("Shutting down ({NumChildren} children)..", _entityStates.Count);

            // Mark as shutting down
            _lifecyclePhase = ShardLifecyclePhase.Stopping;
            _shutdownSender = Sender;

            RequestShutdownAllEntities();

            // Check if should terminated immediately (checks for no children)
            UpdateShardShutdown();
        }

        void UpdateShardShutdown()
        {
            // Not shutting down?
            if (_lifecyclePhase != ShardLifecyclePhase.Stopping)
                return;

            // Waiting for entities to shut down?
            if (_entityStates.Count > 0)
                return;

            // Shutting down and all entities are shut down. Enqueue OnShutdown().
            // Note that we follow the command with a PoisonPill. PoisonPill will kill
            // the actor, and any following messages are discarded.
            Tell(_self, EntityShardFinalizeShutdownCommand.Instance);
            Tell(_self, PoisonPill.Instance);
        }

        async Task ReceiveEntityShardFinalizeShutdownCommand(EntityShardFinalizeShutdownCommand s)
        {
            try
            {
                if (_initializationCompleted)
                    await OnShutdown();
            }
            catch (Exception ex)
            {
                // Ignore crash when closing. This actor is dying anyway and we want to reply
                // regardless of how we die.
                _log.Error("EntityShard.OnShutdown failed: {Error}", ex);
            }
            _lifecyclePhase = ShardLifecyclePhase.Stopped;

            // Inform requester. This command is followed by PoisonPill so this entity will terminate
            // immediately after this handler;
            _shutdownSender.Tell(ShutdownComplete.Instance);
        }

        /// <summary>
        /// Called at the end of <see cref="ShardLifecyclePhase.Stopping"/> after all Entities have been shut down.
        /// This method is NOT CALLED if <see cref="InitializeAsync"/> did not complete successfully.
        /// </summary>
        protected virtual Task OnShutdown() => Task.CompletedTask;

        void AppendStartingFailure(Exception ex)
        {
            _shardStartupErrors.Add(ex);
        }

        protected override void PreStart()
        {
            base.PreStart();

            // Start update timer
            Context.System.Scheduler.ScheduleTellRepeatedly(UpdateTickInterval, UpdateTickInterval, _self, ActorTick.Instance, ActorRefs.NoSender, _cancelUpdateTimer);
        }

        protected override void PostStop()
        {
            // Cancel timer
            _cancelUpdateTimer.Cancel();

            base.PostStop();
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(wrappedExeption =>
            {
                // For convenience, unwrap trivial AggregateExceptions.
                Exception ex;
                if (wrappedExeption is AggregateException aggregateException && aggregateException.InnerExceptions.Count == 1)
                    ex = aggregateException.InnerException;
                else
                    ex = wrappedExeption;

                // Handle terminated child entity actors
                // \note We don't remove the entity here as the Terminated handler will do that
                if (_entityByActor.TryGetValue(Sender, out EntityState entity))
                {
                    entity.TerminationReason = ex;
                }
                else
                    _log.Error("Exception occurred in unknown child {Actor}, {ExceptionType} {ExceptionMessage}:\n{ExceptionStackTrace}", Sender, ex.GetType().Name, ex.Message, ex.StackTrace);

                // Don't auto-restart any actors
                return Directive.Stop;
            }, loggingEnabled: false);
        }

        /// <summary>
        /// Requests entity to shut down. If there are too many ongoing shutdowns, the entity is put into a shutdown queue.
        /// During shutdown or on the queue the entity receives no further messages, and the messages for it are buffered.
        /// </summary>
        protected void RequestEntityShutdown(EntityState entityState)
        {
            // Nothing to do if already done
            if (entityState.Status == EntityStatus.Stopping)
                return;

            // Log a warning if this is the one that exceeds the capacity
            if (_maxConcurrentEntityShutdowns > 0 && _numOngoingShutdowns == _maxConcurrentEntityShutdowns && _deferredShutdowns.Count == 0)
            {
                _log.Warning("Entity shutdown concurrency limit of {Limit} exceeded, throtting.", _maxConcurrentEntityShutdowns);
            }

            // Marks as stopping and try to schedule stop
            entityState.Status = EntityStatus.Stopping;
            _deferredShutdowns.Add(entityState.EntityId);
            TryRunNextEnqueuedEntityShutdown();
        }

        /// <summary>
        /// If there is shutdown capacity, pops the next entity from the shutdown list and begins its shutdown.
        /// </summary>
        void TryRunNextEnqueuedEntityShutdown()
        {
            for (;;)
            {
                // Capacity reached?
                if (_maxConcurrentEntityShutdowns > 0 && _numOngoingShutdowns >= _maxConcurrentEntityShutdowns)
                    return;

                // Pop next from the queue
                if (_deferredShutdowns.Count == 0)
                    return;
                EntityId nextEntityToShutdown = _deferredShutdowns.First();
                _deferredShutdowns.Remove(nextEntityToShutdown);

                if (!_entityStates.TryGetValue(nextEntityToShutdown, out EntityState entityState))
                {
                    _log.Warning("Unknown entity in shutdown list: {EntityId}", nextEntityToShutdown);
                    continue;
                }

                _numOngoingShutdowns++;
                entityState.EntityShutdownStartedAt = DateTime.UtcNow;
                Tell(entityState.Actor, ShutdownEntity.Instance);
            }
        }

        void RequestShutdownAllEntities()
        {
            // Send shutdown request to all children
            foreach ((EntityId entityId, EntityState entityState) in _entityStates)
                RequestEntityShutdown(entityState);
        }

        /// <summary>
        /// Route a message to a local or remote Entity.
        /// </summary>
        void RouteMessage(IRoutedMessage routed)
        {
            //_log.Debug("Routing message from {FromEntityId} to {TargetEntityId} with message {MessageName}", routed.FromEntityId, routed.TargetEntityId, MetaSerializationUtil.PeekMessageName(routed.Message));

            // Check that source is valid
            if (!routed.FromEntityId.IsValid)
            {
                _log.Error("Received message from invalid entity {FromEntityId}: {MessageName}", routed.FromEntityId, MetaSerializationUtil.PeekMessageName(routed.Message));
                return;
            }

            // Resolve target shardId and handle local vs remote cases
            ShardActorResolutionResult targetShard = TryGetShardForEntity(routed.TargetEntityId);
            if (targetShard.ShardActor == _self)
            {
                // Ensure entity actor is woken up
                EntityState entity = GetOrSpawnEntity(routed.TargetEntityId, out SpawnFailure failureReason);

                // Route the triggering message
                if (entity != null)
                {
                    RouteMessageToLocalEntity(entity, routed);
                }
                else
                {
                    // only warn if entities can be spawned
                    if (failureReason != SpawnFailure.AutomaticEntitySpawningIsDisabled)
                        _log.Warning("Failed to spawn entity {EntityId} to receive message {MessageType}: {FailureReason}", routed.TargetEntityId, routed.GetType().Name, failureReason);

                    // \todo: If this is Ask, reject immediately
                }
            }
            else if (targetShard.ShardActor != null)
            {
                // message target not on this shard, forward to receiver shard
                targetShard.ShardActor.Tell(routed);
            }
            else
            {
                EntityState localSourceEntityMaybe = _entityStates.GetValueOrDefault(routed.FromEntityId);
                LogLevel level = localSourceEntityMaybe?.TriggerAndGetThrottledLogLevelForDestinationShardMissing() ?? LogLevel.Warning;
                _log.LogEvent(level, ex: null, "Could not route message {SenderId} -> {EntityId}. Destination not available: {DestinationShard}", routed.FromEntityId, routed.TargetEntityId, targetShard.FailureDestinationDescription);

                // \todo: If this is Ask, reject immediately
            }
        }

        /// <summary>
        /// Immediately deliver a message to a local Entity. Entity must be in Running state.
        /// </summary>
        void DeliverMessageToLocalEntity(EntityState entity, object routed)
        {
            // Log error about invalid status (but try to deliver in any case)
            if (entity.Status != EntityStatus.Running)
                _log.Error("Trying to flush message to Entity {EntityId} in invalid state ({EntityStatus})", entity.EntityId, entity.Status);

            // Forward the message to the child actor
            entity.Actor.Tell(routed);
        }

        /// <summary>
        /// Flush all pending messages to a given local Entity.
        /// </summary>
        /// <param name="entity"></param>
        void FlushPendingMessagesToLocalEntity(EntityState entity)
        {
            foreach (object routed in entity.PendingMessages)
                DeliverMessageToLocalEntity(entity, routed);
            entity.PendingMessages.Clear();
        }

        /// <summary>
        /// Route a message to an entity that is owned by this shard. The message is delivered immediately
        /// if the Entity is in Running state, or put into the pending messages queue otherwise.
        /// </summary>
        /// <param name="entity">Local entity to deliver message to</param>
        /// <param name="routed">Message to deliver</param>
        void RouteMessageToLocalEntity(EntityState entity, object routed)
        {
            // Check that EntityAskReplies are not delivered here
            if (routed is EntityAskReply)
            {
                _log.Error($"Should never get EntityAskReplies in RouteMessageToLocalEntity()!");
                return;
            }

            // Deliver if Entity is in Running state
            if (entity.Status == EntityStatus.Running)
            {
                //_log.Debug("Delivering {ContainerType} / {MessageType} to local {EntityId} (status={EntityStatus})", routed.GetType().Name, MetaSerializationUtil.PeekMessageName(routed.Message), entity.EntityId, entity.Status);
                DeliverMessageToLocalEntity(entity, routed);
            }
            else // not in Running state, so just buffer up the message
            {
                //_log.Debug("Buffering message {MessageType} to local {EntityId} (status={EntityStatus})", routed.GetType().Name, entity.EntityId, entity.Status);
                entity.PendingMessages.Add(routed);
            }
        }

        public enum SpawnFailure
        {
            NoError = 0,
            AutomaticEntitySpawningIsDisabled,
            ShardIsShuttingDown,
            ShardIsNotYetInitialized,
            SpawningActorFailed,
            AlreadyExists,
        }

        protected EntityState GetOrSpawnEntity(EntityId entityId, out SpawnFailure failureReason)
        {
            if (_entityStates.TryGetValue(entityId, out EntityState existingEntity))
            {
                failureReason = SpawnFailure.NoError;
                return existingEntity;
            }

            // Included in IsStartingNewEntitiesAllowed but we want special, more descriptive error.
            if (_lifecyclePhase == ShardLifecyclePhase.Created)
            {
                failureReason = SpawnFailure.ShardIsNotYetInitialized;
                return null;
            }

            if (!IsStartingNewEntitiesAllowed)
            {
                failureReason = SpawnFailure.ShardIsShuttingDown;
                return null;
            }

            // Spawn entity & create state (only if spawned successfully)
            // \note spawning can fail when ClientConnections receive messages after getting destroyed

            // If no entity props func, don't event try to spawn
            if (_entityProps == null)
            {
                failureReason = SpawnFailure.AutomaticEntitySpawningIsDisabled;
                return null;
            }

            // Spawn actor for entity & start watching
            IActorRef actor;
            try
            {
                actor = Context.ActorOf(_entityProps(entityId), entityId.ToString());
            }
            catch (Exception ex)
            {
                _log.Error("Failed to spawn actor for entity {EntityId}: {Error}", entityId, ex);
                failureReason = SpawnFailure.SpawningActorFailed;
                return null;
            }
            Context.Watch(actor);

            // Send InitializeEntity to ensure it's the first received command
            actor.Tell(InitializeEntity.Instance);

            c_entitySpawned.WithLabels(EntityKind.ToString()).Inc();
            EntityState entity = new EntityState(entityId, actor, _entityIncarnationRunningId++);
            _entityStates.Add(entityId, entity);
            _entityByActor.Add(actor, entity);

            failureReason = SpawnFailure.NoError;
            return entity;
        }

        protected SpawnFailure SpawnEntityWithCustomActor(EntityId entityId, Func<IActorRef> actorFactory)
        {
            if (_entityStates.TryGetValue(entityId, out EntityState existingEntity))
                return SpawnFailure.AlreadyExists;

            // Included in IsStartingNewEntitiesAllowed but we want special, more descriptive error.
            if (_lifecyclePhase == ShardLifecyclePhase.Created)
                return SpawnFailure.ShardIsNotYetInitialized;

            if (!IsStartingNewEntitiesAllowed)
                return SpawnFailure.ShardIsShuttingDown;

            // Spawn actor for entity & start watching
            IActorRef actor;
            try
            {
                actor = actorFactory();
            }
            catch (Exception ex)
            {
                _log.Error("Failed to spawn actor for entity {EntityId}: {Error}", entityId, ex);
                return SpawnFailure.SpawningActorFailed;
            }
            Context.Watch(actor);

            // Send InitializeEntity to ensure it's the first received command
            actor.Tell(InitializeEntity.Instance);

            c_entitySpawned.WithLabels(EntityKind.ToString()).Inc();
            EntityState entity = new EntityState(entityId, actor, _entityIncarnationRunningId++);
            _entityStates.Add(entityId, entity);
            _entityByActor.Add(actor, entity);

            return SpawnFailure.NoError;
        }

        /// <summary>
        /// Result to <see cref="TryGetShardForEntity"/>, i.e. the attempt to get the ActorRef
        /// for the EntityShard (local or remote) for a given EntityId. In case there is no actor
        /// available for the EntityId, a description of the destination is provided for
        /// making "message unroutable" error messages more informative.
        /// </summary>
        struct ShardActorResolutionResult
        {
            /// <summary>
            /// Non-null on success.
            /// </summary>
            public readonly IActorRef ShardActor;

            /// <summary>
            /// Only set on success.
            /// </summary>
            public readonly EntityShardId ShardId;

            /// <summary>
            /// For failure, the destination shard url-like for which the resolution failed.
            /// </summary>
            public readonly string FailureDestinationDescription;

            public ShardActorResolutionResult(IActorRef actor, EntityShardId shardId)
            {
                ShardActor = actor;
                ShardId = shardId;
                FailureDestinationDescription = null;
            }

            public ShardActorResolutionResult(string failureDestination)
            {
                ShardActor = null;
                ShardId = default;
                FailureDestinationDescription = failureDestination;
            }
        }

        /// <summary>
        /// Attempts to retrieve the ActorRef for the (local or remote) EntityShard for the given EntityId.
        /// </summary>
        ShardActorResolutionResult TryGetShardForEntity(EntityId entityId)
        {
            EntityShardId shardId = TryGetShardIdForEntity(entityId);

            if (shardId == default)
                return new ShardActorResolutionResult(failureDestination: $"<no shard for {entityId}>");
            else if (shardId == _selfShardId)
                return new ShardActorResolutionResult(_self, shardId);

            IActorRef actor = _shardResolver.TryGetShardActor(shardId);
            if (actor != null)
                return new ShardActorResolutionResult(actor, shardId);

            if (_clusterConfig.TryGetNodeAddressForShardId(shardId, out ClusterNodeAddress address))
                return new ShardActorResolutionResult(failureDestination: $"{address}/{entityId.Kind}");

            return new ShardActorResolutionResult(failureDestination: $"<unknown shard {shardId}>");
        }

        EntityShardId TryGetShardIdForEntity(EntityId entityId)
        {
            if (!EntityConfigRegistry.Instance.TryGetConfig(entityId.Kind, out EntityConfigBase config))
                return default;

            IShardingStrategy kindStrategy = config.ShardingStrategy;
            try
            {
                // \note: May throw if the entityId is not valid within the Strategy's own rules.
                return kindStrategy.ResolveShardId(entityId);
            }
            catch
            {
                return default;
            }
        }

        #region EntitySynchronize

        void ReceiveEntitySynchronizationE2SBeginRequest(EntitySynchronizationE2SBeginRequest req)
        {
            if (!_entityStates.TryGetValue(req.FromEntityId, out EntityState entity))
            {
                _log.Warning("Got EntitySynchronizationE2SBeginRequest for unknown entity: {EntityId}", req.FromEntityId);
                return;
            }

            // Create id from Source entity <-> Source shard communication

            int localChannelId = entity.EntitySyncRunningId++;

            EntitySyncState syncState = new EntitySyncState(
                localChannelId:     localChannelId,
                remoteChannelId:    -1,
                localEntityId:      req.FromEntityId,
                remoteEntityId:     req.TargetEntityId,
                openingPromise:     req.OpeningPromise,
                writerPeer:         req.WriterPeer
                );
            entity.EntitySyncs.Add(localChannelId, syncState);

            // Send to the destination shard.
            // \todo: if target is on the same node, could use more optimized strategy. We could skip
            //        messaging, and fill sync promise and inbox directly. Even maybe from the entity.
            //        Note that this really means same node, not the same shard, which is unlikely due
            //        to the deadlock risk.

            ShardActorResolutionResult targetShard = TryGetShardForEntity(req.TargetEntityId);
            if (targetShard.ShardActor == null)
            {
                LogLevel level = entity.TriggerAndGetThrottledLogLevelForDestinationShardMissing();
                _log.LogEvent(level, ex: null, "Could not process EntitySynchronizationE2SBeginRequest {SourceEntityId} -> {TargetEntityId}. Destination not available: {DestinationShard}", req.FromEntityId, req.TargetEntityId, targetShard.FailureDestinationDescription);
                return;
            }

            EntitySynchronizationS2SBeginRequest shardToShardRequest = new EntitySynchronizationS2SBeginRequest(
                sourceChannelId:    localChannelId,
                targetEntityId:     req.TargetEntityId,
                fromEntityId:       req.FromEntityId,
                message:            req.Message
                );
            targetShard.ShardActor.Tell(shardToShardRequest);
        }

        void ReceiveEntitySynchronizationS2SBeginRequest(EntitySynchronizationS2SBeginRequest req)
        {
            EntityId fromEntityId = req.FromEntityId;
            if (!fromEntityId.IsValid)
            {
                _log.Error("Invalid EntitySynchronizationS2SBeginRequest, source {FromEntityId} is not valid", fromEntityId);
                return;
            }

            EntityId targetEntityId = req.TargetEntityId;
            if (TryGetShardIdForEntity(targetEntityId) != _selfShardId)
            {
                _log.Error("Invalid shard for EntitySynchronizationS2SBeginRequest, target {TargetEntityId} not allocated for this shard", targetEntityId);
                return;
            }

            EntityState entity = GetOrSpawnEntity(targetEntityId, out SpawnFailure failureReason);
            if (entity == null)
            {
                _log.Warning("Failed to spawn entity {EntityId} to receive EntitySynchronize: {FailureReason}", targetEntityId, failureReason);
                return;
            }

            // Create id from Target Entity <-> Target shard communication

            int localChannelId = entity.EntitySyncRunningId++;

            EntitySyncState syncState = new EntitySyncState(
                localChannelId:     localChannelId,
                remoteChannelId:    req.SourceChannelId,
                localEntityId:      targetEntityId,
                remoteEntityId:     fromEntityId,
                openingPromise:     null,
                writerPeer:         null
                );
            entity.EntitySyncs.Add(localChannelId, syncState);

            // Forward to the entity
            EntitySynchronizationS2EBeginRequest entityRequest = new EntitySynchronizationS2EBeginRequest(
                channelId:      localChannelId,
                targetEntityId: targetEntityId,
                fromEntityId:   fromEntityId,
                message:        req.Message
                );

            // Deliver message to the entity (may also buffer)
            RouteMessageToLocalEntity(entity, entityRequest);
        }

        void ReceiveEntitySynchronizationS2EBeginResponse(EntitySynchronizationS2EBeginResponse response)
        {
            if (!_entityStates.TryGetValue(response.FromEntityId, out EntityState entity))
            {
                _log.Warning("Got EntitySynchronizationS2EBeginResponse for unknown entity: {EntityId}", response.FromEntityId);
                return;
            }
            if (!entity.EntitySyncs.TryGetValue(response.ChannelId, out EntitySyncState syncState))
            {
                _log.Warning("Got EntitySynchronizationS2EBeginResponse for unknown channel: Channel {ChannelId}", response.ChannelId);
                return;
            }
            if (syncState.WriterPeer != null)
            {
                _log.Warning("Got EntitySynchronizationS2EBeginResponse at unexpected time: Channel {ChannelId}", response.ChannelId);
                return;
            }

            // set writer once.
            syncState.WriterPeer = response.WriterPeer;

            // Reply to remote shard
            ShardActorResolutionResult targetShard = TryGetShardForEntity(syncState.RemoteEntityId);
            if (targetShard.ShardActor == null)
            {
                LogLevel level = entity.TriggerAndGetThrottledLogLevelForDestinationShardMissing();
                _log.LogEvent(level, ex: null, "Could not process EntitySynchronizationS2EBeginResponse {SourceEntityId} -> {TargetEntityId}. Destination not available: {DestinationShard}", response.FromEntityId, syncState.RemoteEntityId, targetShard.FailureDestinationDescription);
                return;
            }

            EntitySynchronizationS2SBeginResponse shardResponse = new EntitySynchronizationS2SBeginResponse(
                targetEntityId:     syncState.RemoteEntityId,
                sourceChannelId:    syncState.LocalChannelId,
                targetChannelId:    syncState.RemoteChannelId
                );

            targetShard.ShardActor.Tell(shardResponse);

            // sync channel is now open
        }

        void ReceiveEntitySynchronizationS2SBeginResponse(EntitySynchronizationS2SBeginResponse response)
        {
            if (!_entityStates.TryGetValue(response.TargetEntityId, out EntityState entity))
            {
                _log.Warning("Got EntitySynchronizationS2SBeginResponse for unknown entity: {EntityId}", response.TargetEntityId);
                return;
            }
            if (!entity.EntitySyncs.TryGetValue(response.TargetChannelId, out EntitySyncState syncState))
            {
                _log.Warning("Got EntitySynchronizationS2SBeginResponse for unknown channel: Channel {ChannelId}", response.TargetChannelId);
                return;
            }
            if (syncState.RemoteChannelId != -1)
            {
                _log.Warning("Got EntitySynchronizationS2SBeginResponse at unexpected time: Channel {ChannelId}", response.TargetChannelId);
                return;
            }

            // set channel once and trigger open promise
            syncState.RemoteChannelId = response.SourceChannelId;
            syncState.OpeningPromise.SetResult(syncState.LocalChannelId);
            syncState.OpeningPromise = null;

            // sync channel is now open
        }

        void ReceiveEntitySynchronizationE2SChannelMessage(EntitySynchronizationE2SChannelMessage channelMessage)
        {
            bool isEndOfStream = channelMessage.Message.IsEmpty;

            if (!_entityStates.TryGetValue(channelMessage.FromEntityId, out EntityState entity))
            {
                _log.Warning("Got EntitySynchronizationE2SChannelMessage for unknown entity: {EntityId}", channelMessage.FromEntityId);
                return;
            }
            if (!entity.EntitySyncs.TryGetValue(channelMessage.ChannelId, out EntitySyncState syncState))
            {
                _log.Warning("Got EntitySynchronizationE2SChannelMessage for unknown channel: Channel {ChannelId}", channelMessage.ChannelId);
                return;
            }

            if (isEndOfStream)
                entity.EntitySyncs.Remove(channelMessage.ChannelId);

            // Deliver to the target shard, unless already closed there.

            if (syncState.IsRemoteClosed)
            {
                if (!isEndOfStream)
                    _log.Warning("Entity {EntityId} attempted to write to a closed sync channel {ChannelId} to {TargetEntityId}", syncState.LocalEntityId,  channelMessage.ChannelId, syncState.RemoteEntityId);
                return;
            }

            ShardActorResolutionResult targetShard = TryGetShardForEntity(syncState.RemoteEntityId);
            if (targetShard.ShardActor == null)
            {
                LogLevel level = entity.TriggerAndGetThrottledLogLevelForDestinationShardMissing();
                _log.LogEvent(level, ex: null, "Could not process EntitySynchronizationE2SChannelMessage {SourceEntityId} -> {TargetEntityId}. Destination not available: {DestinationShard}", channelMessage.FromEntityId, syncState.RemoteEntityId, targetShard.FailureDestinationDescription);
                return;
            }

            EntitySynchronizationS2SChannelMessage shardToShardRequest = new EntitySynchronizationS2SChannelMessage(
                targetEntityId: syncState.RemoteEntityId,
                channelId:      syncState.RemoteChannelId,
                message:        channelMessage.Message
                );
            targetShard.ShardActor.Tell(shardToShardRequest);
        }

        void ReceiveEntitySynchronizationS2SChannelMessage(EntitySynchronizationS2SChannelMessage channelMessage)
        {
            bool isEof = channelMessage.Message.IsEmpty;

            if (!_entityStates.TryGetValue(channelMessage.TargetEntityId, out EntityState entity))
            {
                _log.Warning("Got EntitySynchronizationS2SChannelMessage for unknown entity: {EntityId}", channelMessage.TargetEntityId);
                return;
            }
            if (!entity.EntitySyncs.TryGetValue(channelMessage.ChannelId, out EntitySyncState syncState))
            {
                // Acknowlegding channel close? If so, ignore. We most likely just closed the channel from this end. Otherwise want
                if (!isEof)
                    _log.Warning("Got EntitySynchronizationS2SChannelMessage for unknown channel: Channel {ChannelId}", channelMessage.ChannelId);
                return;
            }

            if (!isEof)
            {
                bool success = syncState.WriterPeer.TryWrite(channelMessage.Message);
                if (!success)
                    _log.Error("While processing EntitySynchronization message unbounded queue refused write");
            }
            else
            {
                syncState.WriterPeer.Complete();
                syncState.IsRemoteClosed = true;
                // \note: we don't remove half-closed connection from entity.EntitySyncs. We wait for the local entity to clear it.
            }
        }

        #endregion
    }
}
