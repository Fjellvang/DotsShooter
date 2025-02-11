// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Options;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Cloud.Cluster
{
    /// <summary>
    /// Current lifecycle phase that the cluster is in.
    /// </summary>
    /// <remarks>The status only goes in one direction: Connecting -> Starting -> Running -> Stopping -> Terminated</remarks>
    public enum ClusterPhase
    {
        Connecting,     // Cluster is waiting for all nodes to report in
        Starting,       // Cluster entity/service shards are being started, new nodes can join instantly
        Running,        // Cluster is in a running state, shards on new nodes don't need to wait
        Stopping,       // Cluster services are being stopped
        Terminated,     // All cluster services have been stopped, ready to exit
    }

    /// <summary>
    /// Lifecycle phase of an <see cref="EntityShardGroup"/>. Used within the cluster to synchronize
    /// initialization and shutdown sequences.
    /// </summary>
    public enum EntityGroupPhase
    {
        /// <summary>
        /// EntityShards have not been created yet or they have been shut down.
        /// </summary>
        NotCreated = 0,

        /// <summary>
        /// All EntityShards in the group have been created but no entities have been spawned.
        /// </summary>
        Created,

        /// <summary>
        /// All EntityShards in the group are up and running, with entities initialized.
        /// </summary>
        Running,

        // \todo: Add Stopped mode to shut down all entities before shard shutdown?
    }

    /// <summary>
    /// Locally published event to inform other actors about cluster status changes.
    /// </summary>
    public class ClusterPhaseUpdateEvent
    {
        public readonly ClusterPhase Phase;

        public ClusterPhaseUpdateEvent(ClusterPhase status) => Phase = status;
    }

    /// <summary>
    /// Exception for when the cluster is already shutting down when we're trying to
    /// join in.
    /// </summary>
    public class ClusterAlreadyShuttingDownException : Exception
    {
        public ClusterAlreadyShuttingDownException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Coordinates this node's lifecycle with respect to the cluster changes.
    /// <para>
    /// Specifically, this actor:
    /// <list type="bullet">
    /// <item>
    /// Listens for <see cref="ClusterConnectionManager.ClusterChangedEvent"/> event.
    /// </item>
    /// <item>
    /// Manages the lifecycle the current application. Coordinator chooses when the current server shuts down. Shutdown can happen if
    /// node shutdown is requested (see <see cref="RequestNodeShutdown"/>), or when cluster shutdown has been requested on any node
    /// of the cluster (see <see cref="RequestClusterShutdown"/>).
    /// </item>
    /// <item>
    /// Manages the lifecycle of the shards on the current node, in sync with other node's ClusterCoordinatorActors in the cluster.
    /// </item>
    /// <item>
    /// Maintains the state of <see cref="EntitySharding"/> global.
    /// </item>
    /// </list>
    /// </para>
    /// </summary>
    public partial class ClusterCoordinatorActor : MetaReceiveActor
    {
        const string CoordinatorName = "cluster";

        readonly ClusterNodeAddress     _selfAddress;
        readonly ClusterConfig          _clusterConfig;
        readonly EntitySharding         _entitySharding;
        readonly EntityShardResolver    _shardResolver;

        ClusterEntityShardingState      _clusterEntityShardingState; // null if not connected
        EntityGroupPhase[]              _selfEntityGroupPhases;

        MetaDictionary<EntityShardGroup, EntityConfigBase[]>  _localEntityConfigs;
        MetaDictionary<EntityKind, IActorRef>                 _localEntityShards = new();

        /// <summary>
        /// Lock to manage <see cref="s_clusterShutdownRequested"/> and <see cref="s_nodeShutdownRequested"/> bools are observed before <see cref="s_instance"/> is set.
        /// </summary>
        static readonly object          s_shutdownStateLock         = new object();

        /// <summary>
        /// True if <see cref="RequestClusterShutdown"/> has been called and this node has requested a coordinated shutdown.
        /// <para>
        /// This state consists of two components: a static variable and a member variable.
        /// The static variable allows requesting shutdown before coordinator has started. Essentially it is the "pending" state,
        /// that get applied into the actual state in the member variable. The member variable is the real state used in logic.
        /// </para>
        /// </summary>
        static bool                     s_clusterShutdownRequested  = false;

        /// <summary>
        /// <inheritdoc cref="s_clusterShutdownRequested"/>
        /// </summary>
        bool                            _clusterShutdownRequested;

        /// <summary>
        /// True if <see cref="RequestNodeShutdown"/> has been called and this node has requested a node shutdown.
        /// <para>
        /// This state consists of two components: a static variable and a member variable.
        /// The static variable allows requesting shutdown before coordinator has started. Essentially it is the "pending" state,
        /// that get applied into the actual state in the member variable. The member variable is the real state used in logic.
        /// </para>
        /// </summary>
        static bool                     s_nodeShutdownRequested     = false;

        /// <summary>
        /// <inheritdoc cref="s_nodeShutdownRequested"/>
        /// </summary>
        bool                            _nodeShutdownRequested;

        static IActorRef                s_instance                  = null;

        static volatile ClusterPhase    s_phase                     = ClusterPhase.Connecting;
        public static ClusterPhase      Phase                       => s_phase;

        /// <summary>
        /// If true, this is a coordinated, graceful shutdown, and application should wait for orchestrator (Kubernetes) to signal node shutdown (SIGTERM).
        /// </summary>
        static volatile bool            s_isCoordinatedShutdown     = false;
        /// <summary>
        /// <inheritdoc cref="s_isCoordinatedShutdown"/>
        /// </summary>
        public static bool              IsCoordinatedShutdown       => s_isCoordinatedShutdown;

        static readonly TimeSpan        TickInterval                = TimeSpan.FromMilliseconds(5_000);

        class InitiateShutdownRequest { public static readonly InitiateShutdownRequest Instance = new InitiateShutdownRequest(); }

        class GetClusterPhaseRequest { public static GetClusterPhaseRequest Instance => new GetClusterPhaseRequest(); }
        class GetClusterPhaseResponse
        {
            public ClusterPhase ClusterPhase { get; private set; }

            public GetClusterPhaseResponse(ClusterPhase clusterPhase)
            {
                ClusterPhase = clusterPhase;
            }
        }

        public ClusterCoordinatorActor()
        {
            ClusteringOptions clusterOpts = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>();

            _selfAddress        = clusterOpts.SelfAddress;
            _clusterConfig      = clusterOpts.ClusterConfig;
            _entitySharding     = EntitySharding.Get(Context.System);
            _shardResolver      = EntityShardResolver.Get(Context.System);

            // Resolve local EntityShards by EntityGroup
            _localEntityConfigs = new MetaDictionary<EntityShardGroup, EntityConfigBase[]>();
            for (EntityShardGroup entityGroup = 0; entityGroup < EntityShardGroup.Last; entityGroup++)
            {
                List<EntityConfigBase> matching = new List<EntityConfigBase>();
                foreach (EntityKind entityKind in GetEntityGroupKindsOnNode(_clusterConfig, _selfAddress, entityGroup))
                {
                    EntityConfigBase entityConfig = EntityConfigRegistry.Instance.GetConfig(entityKind);
                    matching.Add(entityConfig);
                }
                _localEntityConfigs.Add(entityGroup, matching.ToArray());
            }

            RegisterHandlers();

            // Initialize ClusterConnectionManager state
            _selfEntityGroupPhases = CreateEntityGroupArray(EntityGroupPhase.NotCreated);
            SendStateToPeers();
        }

        public static bool IsReady()
        {
            return s_phase == ClusterPhase.Running;
        }

        /// <summary>
        /// Request this node to shut down. Shutting down a single node does not shut down all the
        /// nodes in the cluster.
        /// See also: <seealso cref="RequestClusterShutdown"/>
        /// </summary>
        public static void RequestNodeShutdown()
        {
            lock (s_shutdownStateLock)
            {
                // Mark shutdown as requested (required in case actor is not yet up)
                s_nodeShutdownRequested = true;

                // If instance is already up, send request to start shutdown sequence
                if (s_instance != null)
                    s_instance.Tell(InitiateShutdownRequest.Instance);
            }
        }

        /// <summary>
        /// Request the cluster to shut down. All nodes will coordinate shutdown.
        /// If both this method and <see cref="RequestNodeShutdown"/> are called, and shutdown hasn't completed
        /// yet, the shutdown performed will be coordinated shutdown. That is, this method has precedence.
        /// </summary>
        public static void RequestClusterShutdown()
        {
            lock (s_shutdownStateLock)
            {
                // Mark shutdown as requested (required in case actor is not yet up)
                s_clusterShutdownRequested = true;

                // If instance is already up, send request to start shutdown sequence
                if (s_instance != null)
                    s_instance.Tell(InitiateShutdownRequest.Instance);
            }
        }

        static async Task<ClusterPhase> GetClusterPhaseAsync(IActorRef actor)
        {
            return (await actor.Ask<GetClusterPhaseResponse>(GetClusterPhaseRequest.Instance)).ClusterPhase;
        }

        /// <summary>
        /// Wait for this node to become a member of the running cluster and have all entity shards running.
        /// </summary>
        public static async Task WaitForClusterReadyAsync(IActorRef actor, IMetaLogger logger, TimeSpan timeout)
        {
            DateTime timeoutAt = DateTime.UtcNow + timeout;
            for (int iter = 0; ; iter++)
            {
                ClusterPhase clusterPhase = await GetClusterPhaseAsync(actor);

                if (clusterPhase == ClusterPhase.Running)
                    return;
                else if (clusterPhase > ClusterPhase.Running)
                    throw new ClusterAlreadyShuttingDownException($"Cluster in unexpected phase {clusterPhase} while we're waiting for it to get ready");

                // Log progress every second & wait a bit
                if (iter % 100 == 0)
                    logger.Debug("Waiting for cluster nodes to connect.");
                await Task.Delay(10);

                // Check for timeout
                if (DateTime.UtcNow >= timeoutAt)
                    throw new TimeoutException("Timeout while waiting for cluster to spin up, giving up!");
            }
        }

        /// <summary>
        /// Wait for this node to become a ready to shutdown and have all entity shard shut down. Entity shards may
        /// remain running if they exceed their shutdown timeouts. Completes with a <see cref="TimeoutException"/>
        /// if wait exceeds the <paramref name="timeout"/>.
        /// </summary>
        public static async Task WaitForClusterTerminatedAsync(IActorRef actor, IMetaLogger logger, TimeSpan timeout)
        {
            DateTime timeoutAt = DateTime.UtcNow + timeout;
            for (int iter = 0; ; iter++)
            {
                ClusterPhase clusterPhase = await GetClusterPhaseAsync(actor);

                if (clusterPhase == ClusterPhase.Terminated)
                    return;

                // Log progress every second & wait a bit
                if (iter % 100 == 0)
                    logger.Debug("Waiting for entity shards to shut down");
                await Task.Delay(10);

                // Check for timeout
                if (DateTime.UtcNow >= timeoutAt)
                    throw new TimeoutException("Timeout while waiting for cluster to shut down, giving up!");
            }
        }

        void SetClusterPhase(ClusterPhase newPhase, string reason)
        {
            // If status changed, publish event
            ClusterPhase oldPhase = s_phase;
            if (newPhase != oldPhase)
            {
                _log.Information("Switching cluster phase from {OldPhase} to {NewPhase} (reason={Reason})", oldPhase, newPhase, reason);
                s_phase = newPhase;

                // Send notifications to listeners
                Context.System.EventStream.Publish(new ClusterPhaseUpdateEvent(newPhase));

                // Inform peers of phase changes
                SendStateToPeers();
            }
        }

        /// <summary>
        /// Updates this node's <see cref="ClusterConnectionManager.ClusterNodeInformation"/>. Updating the information will propagate the information
        /// to all peers, and all cluster nodes will observe a <see cref="ClusterConnectionManager.ClusterChangedEvent"/>. This includes this current node
        /// too.
        /// </summary>
        /// <param name="overrideLocalEntityShards">If non-null, used to as the local entityShard actorref state.</param>
        void SendStateToPeers(MetaDictionary<EntityKind, IActorRef> overrideLocalEntityShards = null)
        {
            DeploymentOptions deploymentOpts = RuntimeOptionsRegistry.Instance.GetCurrent<DeploymentOptions>();

            // Note that if this node was part of the cluster, a new ClusterChangedEvent will be emitted. That will call ProgressClusterStateAsync(),
            // which will continue the startup progress if able.
            ClusterConnectionManager.SetSelfClusterNodeInfo(new ClusterConnectionManager.ClusterNodeInformation(
                publicIpV4Address:          deploymentOpts.PublicIpv4,
                localPhase:                 s_phase,
                clusterShutdownRequested:   _clusterShutdownRequested,
                entityGroupPhases:          _selfEntityGroupPhases,
                processStartedAt:           Application.Application.ProcessStartTimestamp,
                entityShardActors:          overrideLocalEntityShards ?? _localEntityShards));
        }

        void RegisterHandlers()
        {
            ReceiveAsync<ClusterConnectionManager.ClusterChangedEvent>(ReceiveClusterChangedEvent);

            ReceiveAsync<ActorTick>(ReceiveActorTick);
            ReceiveAsync<InitiateShutdownRequest>(ReceiveInitiateShutdownRequest);
            Receive<GetClusterPhaseRequest>(ReceiveGetClusterPhaseRequest);
            ReceiveAsync<Terminated>(ReceiveTerminated);
        }

        static EntityGroupPhase[] CreateEntityGroupArray(EntityGroupPhase fillValue)
        {
            EntityGroupPhase[] arr = new EntityGroupPhase[(int)EntityShardGroup.Last];
            for (int ndx = 0; ndx < arr.Length; ++ndx)
                arr[ndx] = fillValue;
            return arr;
        }

        static EntityKind[] GetEntityGroupKindsOnNode(ClusterConfig clusterConfig, ClusterNodeAddress address, EntityShardGroup entityGroup)
        {
            List<EntityKind> kinds = new List<EntityKind>();

            NodeSetConfig nodeSetConfig = clusterConfig.GetNodeSetConfigForAddress(address);
            foreach (EntityKind entityKind in nodeSetConfig.EntityKindMask.GetKinds())
            {
                if (EntityConfigRegistry.Instance.TryGetConfig(entityKind, out EntityConfigBase entityConfig) && entityConfig.EntityShardGroup == entityGroup)
                    kinds.Add(entityKind);
            }

            return kinds.ToArray();
        }

        async Task ReceiveClusterChangedEvent(ClusterConnectionManager.ClusterChangedEvent cluster)
        {
            // Update EntityShard resolver to ensure local EntityShards cannot be arbitrarily late compared to this actor.
            // We especially do not want a Shard starting and communicating with some other Shard before that other Shard
            // has been notified of the Shard existing.
            //
            // Theoretically this gains us nothing. With the assumption of reasonable fairness and liveliness, the various
            // observers can't disagree too much and events are sensibly ordered. Without reasonable fairness and liveliness,
            // the server is useless to human players.
            //
            // However, in CI tests we commonly run in resource starved environments with no players. For those environments,
            // this improves the general robustness. We only ensure the event ordering within the current node -- ordering
            // over multiple node is complicated, and as CI only runs on one Node, useless as descibed above.
            _shardResolver.OnClusterChangedEvent(cluster);

            // Construct a new ClusterEntityShardingState from the new cluster topology and apply changes
            // to the updated and new nodes.
            List<NodeEntityShardingState> nodes = new List<NodeEntityShardingState>();
            foreach (ClusterConnectionManager.ClusterChangedEvent.ClusterMember member in cluster.Members)
            {
                NodeEntityShardingState newState = new NodeEntityShardingState(member.Address, isSelf: member.Address == _selfAddress);

                if (newState.IsSelf)
                {
                    // For self, replace state with the local copies of the phases. This prevents us from going back in time
                    // in case we receive a cluster update after a state change, but before the new state change has been propagated
                    // to cluster state yet.
                    newState.IsConnected = true;
                    newState.ClusterPhase = s_phase;
                    newState.EntityGroupPhases = _selfEntityGroupPhases;
                }
                else
                {
                    // For remote nodes, update shard phases
                    if (member.IsConnected)
                    {
                        newState.IsConnected = true;
                        newState.ClusterPhase = member.Info.LocalPhase;
                        newState.EntityGroupPhases = member.Info.EntityGroupPhases;
                    }
                    else
                    {
                        newState.IsConnected = false;
                        newState.ClusterPhase = ClusterPhase.Connecting;
                        newState.EntityGroupPhases = CreateEntityGroupArray(EntityGroupPhase.NotCreated);
                    }
                }
                nodes.Add(newState);
            }

            ClusterEntityShardingState newShardingState = new ClusterEntityShardingState(nodes);

            // Handle all nodes that got removed from the cluster topology. Remove EntitySharding refs for
            // any removed remote node, and remove local entity shards if it is this node that got removed.
            if (_clusterEntityShardingState != null)
            {
                foreach (NodeEntityShardingState oldNode in _clusterEntityShardingState.Nodes)
                {
                    // Node still present? If so, skip
                    if (newShardingState.TryGetNodeState(oldNode.Address) != null)
                        continue;

                    if (oldNode.IsSelf)
                    {
                        // We were kicked out of the cluster. Membership to the cluster is in the sole discretion of the leader node.
                        // If leader node has lost connection this node for long enough, we get kicked removed from the node. Since we
                        // are receiving this topology update, it means we have established the connection again. (We could also be informed
                        // via relayed ClusterTopologyUpdate).

                        if (s_phase == ClusterPhase.Starting || s_phase == ClusterPhase.Running)
                        {
                            // We were kicked out of the cluster while we had workload.
                            _log.Information("This node was kicked from the cluster. Shutting down all shards and resuming into pending mode");

                            // Mark shards as dead so other nodes don't try to use them. We speculatively set the
                            // group phases to NotCreated and clear the (node information's) shard ActorRefs.
                            //
                            // This is needed because the server might join a new cluster while we are shutting down. Other
                            // nodes would still see the the old shard phases and refs.
                            //
                            // Note the actual actor refs (_localEntityShards) are not cleared since we need those for the shutdown. After
                            // successful shutdown, _localEntityShards will be empty.
                            _selfEntityGroupPhases = CreateEntityGroupArray(EntityGroupPhase.NotCreated);
                            MetaDictionary<EntityKind, IActorRef> speculatedEmptyEntityShards = new MetaDictionary<EntityKind, IActorRef>();
                            SendStateToPeers(overrideLocalEntityShards: speculatedEmptyEntityShards);

                            // Shut down shards.
                            bool anyShardStopFailure = false;
                            for (int groupNdx = (int)EntityShardGroup.Last - 1; groupNdx >= 0;  groupNdx--)
                            {
                                EntityShardGroup entityGroup = (EntityShardGroup)groupNdx;
                                if (!await TryStopEntityShardGroupAsync(entityGroup))
                                    anyShardStopFailure = true;
                            }

                            // If we have stopped all the entities, we can reset back into the connecting Phase, i.e. waiting for
                            // us to be added into the cluster. But if any shard stopping failed, the shard may still remain and we
                            // cannot restart. In that case, restart the whole node.
                            if (anyShardStopFailure)
                            {
                                s_isCoordinatedShutdown = false;
                                SetClusterPhase(ClusterPhase.Terminated, reason: "KickedFromCluster");
                            }
                            else
                            {
                                // Go back into connecting mode.
                                SetClusterPhase(ClusterPhase.Connecting, "KickedFromCluster");
                            }
                        }
                        else if (s_phase == ClusterPhase.Stopping)
                        {
                            // Kicked while already stopping. No need to do anything. Stopping will continue.
                        }
                    }
                }
            }

            _clusterEntityShardingState = newShardingState;

            // Update cluster shutdown
            if (!_clusterShutdownRequested)
            {
                bool isCoordinatedShutdownRequestedByAnyClusterMember =
                    cluster
                    .Members
                    .Any(member => member.IsConnected && member.Info.ClusterShutdownRequested);

                if (isCoordinatedShutdownRequestedByAnyClusterMember)
                {
                    _clusterShutdownRequested = true;
                    SendStateToPeers();
                }
            }

            // Try to make lifecycle progress
            await ProgressClusterStateAsync();
        }

        async Task ReceiveActorTick(ActorTick tick)
        {
            // Try to progress cluster lifecycle
            await ProgressClusterStateAsync();
        }

        async Task ReceiveInitiateShutdownRequest(InitiateShutdownRequest shutdown)
        {
            if (s_nodeShutdownRequested)
                _nodeShutdownRequested = true;
            if (s_clusterShutdownRequested)
                _clusterShutdownRequested = true;

            SendStateToPeers();

            // Start shutting down services
            await ProgressClusterStateAsync();
        }

        void ReceiveGetClusterPhaseRequest(GetClusterPhaseRequest req)
        {
            Sender.Tell(new GetClusterPhaseResponse(s_phase));
        }

        async Task ReceiveTerminated(Terminated terminated)
        {
            // Find the shard actor that died. If shard is shutting down, it may
            // be removed from _localEntityShards already. Otherwise this is fatal.
            // Note that Terminated is emitted for both expected and unexpected shutdowns.
            foreach ((EntityKind kind, IActorRef actor) in _localEntityShards)
            {
                if (actor != terminated.ActorRef)
                    continue;

                switch (s_phase)
                {
                    case ClusterPhase.Connecting:
                    case ClusterPhase.Starting:
                    {
                        _log.Error("EntityShard {ShardId} crashed during boot (Phase={Phase}). Cannot proceed. Terminating server.", kind, s_phase);
                        Application.Application.ForceTerminate(exitCode: 100, reason: Invariant($"EntityShard {kind} crashed in during boot"));
                        break;
                    }

                    case ClusterPhase.Running:
                    {
                        _log.Error("EntityShard {ShardId} unexpectedly crashed. Terminating server.", kind);

                        // Mark the shard as dead and start shutdown.
                        _localEntityShards.Remove(kind);
                        SetClusterPhase(ClusterPhase.Stopping, reason: "Unexpected shard crash");
                        await StepClusterStateAsync();
                        break;
                    }

                    case ClusterPhase.Stopping:
                    case ClusterPhase.Terminated:
                    {
                        _log.Error("EntityShard {ShardId} crashed during shutdown (Phase={Phase}). Continuing shutdown.", kind, s_phase);

                        // Mark the shard as dead and tolerate.
                        _localEntityShards.Remove(kind);
                        await StepClusterStateAsync();
                        break;
                    }
                }
                return;
            }
        }

        protected override void PreStart()
        {
            base.PreStart();

            // Store global instance
            if (s_instance != null)
                throw new InvalidOperationException($"Singleton instance of ClusterCoordinatorActor already registered!");

            bool anyShutdownsPending;
            lock (s_shutdownStateLock)
            {
                s_instance = _self;
                anyShutdownsPending = s_nodeShutdownRequested || s_clusterShutdownRequested;
            }

            // Start update timer
            Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.Zero, TickInterval, _self, ActorTick.Instance, ActorRefs.NoSender, _cancelTimers);

            // Subscribe to nodes being added and removed
            ClusterConnectionManager.SubscribeToClusterEvents(_self);

            // Continue any pending shutdowns
            if (anyShutdownsPending)
                Tell(_self, InitiateShutdownRequest.Instance);
        }

        protected override void PostStop()
        {
            // Clear global instance
            s_instance = null;

            base.PostStop();
            _log.Information("Cluster coordinator stopped");
        }

        protected override void PreRestart(Exception reason, object message)
        {
            base.PreRestart(reason, message);

            // Restart is not allowed.
            _log.Error("ClusterCoordinator has crashed. Internal cluster state has been lost, cannot proceed: {Error}", reason);
            Application.Application.ForceTerminate(exitCode: 102, reason: "ClusterCoordinator crashed");
        }

        async Task CreateEntityShardGroupAsync(EntityShardGroup entityGroup)
        {
            EntityKind[] groupEntityKinds = GetEntityGroupKindsOnNode(_clusterConfig, _selfAddress, entityGroup);
            _log.Information("Creating local EntityShardGroup {EntityGroup} with EntityShards: {EntityShards}", entityGroup, PrettyPrint.Compact(groupEntityKinds));

            // Create all local EntityShards in the entityGroup (in arbitrary order)
            foreach (EntityKind entityKind in groupEntityKinds)
            {
                // If the EntityShard should be running locally, spawn & register it
                bool isOwnedShard = _clusterConfig.ResolveNodeShardId(entityKind, _selfAddress, out EntityShardId entityShardId);
                if (!isOwnedShard)
                {
                    // Impossible
                    throw new InvalidOperationException($"Trying to start non-local EntityShard {entityKind}");
                }

                try
                {
                    IActorRef shardActor = await _entitySharding.CreateShardAsync(new EntityShardConfig(entityShardId));
                    Context.Watch(shardActor);
                    _localEntityShards[entityKind] = shardActor;
                }
                catch (Exception ex)
                {
                    // If shard actor creation fails, we are in an unknown state and cannot recover.
                    _log.Error("EntityShard {ShardId} is in an unpredictable state. Cannot proceed: {Error}", entityShardId, ex);
                    Application.Application.ForceTerminate(exitCode: 100, reason: Invariant($"EntityShard {entityShardId} state desynced on creation"));
                }
            }
        }

        async Task StartEntityShardGroupAsync(EntityShardGroup entityGroup)
        {
            EntityKind[] groupEntityKinds = GetEntityGroupKindsOnNode(_clusterConfig, _selfAddress, entityGroup);
            _log.Information("Starting local EntityShardGroup {EntityGroup}: {EntityShards}", entityGroup, PrettyPrint.Compact(groupEntityKinds));

            // Start all local EntityShards in the group (in arbitrary order)
            foreach (EntityKind entityKind in groupEntityKinds)
            {
                // Tell the EntityShard to start itself
                // \note: _localEntityShards[entityKind] must exist. Shard unexpectedly dying would kill the server.
                IActorRef shardActor = _localEntityShards[entityKind];
                shardActor.Tell(new EntityShard.StartShard());
            }

            // Wait for all EntityShards in the group to be ready
            await WaitUntilEntityShardsReadyAsync(entityGroup, timeout: TimeSpan.FromMinutes(5));

            _log.Information("Local EntityShardGroup {EntityGroup} is ready", entityGroup);
        }

        async Task WaitUntilEntityShardsReadyAsync(EntityShardGroup entityGroup, TimeSpan timeout)
        {
            DateTime        timeoutAt               = DateTime.UtcNow + timeout;
            DateTime        nextWaitLogAt           = DateTime.UtcNow + TimeSpan.FromSeconds(1);

            // Wait until the shard actor reports it's ready or failed to start. Wait until the timeout while printing status messages periodically.
            // \note: _localEntityShards[entityKind] must exist. Shard unexpectedly dying would kill the server.
            EntityKind[]    groupEntityKinds        = GetEntityGroupKindsOnNode(_clusterConfig, _selfAddress, entityGroup);
            IActorRef[]     shardActors             = groupEntityKinds.Select(entityKind => _localEntityShards[entityKind]).ToArray();
            int             numShards               = shardActors.Length;
            Task<bool>[]    waitUntilRunningQueries = new Task<bool>[numShards];
            bool[]          isShardRunning          = new bool[numShards];
            for (; ; )
            {
                // Timeout deadline is strict. No last checks.
                if (DateTime.UtcNow > timeoutAt)
                {
                    string pendingShards = string.Join(", ", Enumerable.Range(0, numShards).Where(ndx => !isShardRunning[ndx]).Select(ndx => shardActors[ndx].Path.Name));
                    _log.Error("Timeout while starting EntityShardGroup {entityGroup}. Pending shards: {pendingShards}", entityGroup, pendingShards);

                    // Exit immediately and wait for process to get restarted
                    Application.Application.ForceTerminate(exitCode: 101, reason: $"Timeout while starting EntityShardGroup {entityGroup}. Pending shards: {pendingShards}");
                }

                // Ensure we have a query to ping each shard
                for (int shardNdx = 0; shardNdx < numShards; shardNdx++)
                {
                    if (waitUntilRunningQueries[shardNdx] == null)
                        waitUntilRunningQueries[shardNdx] = EntityShard.TryWaitUntilRunning(shardActors[shardNdx]);
                }

                // Periodic print to show something is happening
                if (DateTime.UtcNow > nextWaitLogAt)
                {
                    nextWaitLogAt = DateTime.UtcNow + TimeSpan.FromSeconds(1);
                    EntityKind[] pendingShardKinds =
                        Enumerable.Range(0, numShards)
                        .Where(shardNdx => !isShardRunning[shardNdx])
                        .Select(shardNdx => groupEntityKinds[shardNdx])
                        .ToArray();
                    _log.Information("Waiting for EntityShardGroup {ShardGroupName} to be ready, pending: {PendingShardKinds}", entityGroup, PrettyPrint.Compact(pendingShardKinds));
                }

                // Poll all shard queries
                try
                {
                    // Wait a bit for the all the queries to make progress
                    await Task.WhenAny(Task.WhenAll(waitUntilRunningQueries), Task.Delay(TimeSpan.FromMilliseconds(100)));

                    // Update shard statuses & reset any queries that returned false (not yet running)
                    for (int shardNdx = 0; shardNdx < numShards; shardNdx++)
                    {
                        if (waitUntilRunningQueries[shardNdx].IsCompleted)
                        {
                            bool isRunning = await waitUntilRunningQueries[shardNdx];
                            isShardRunning[shardNdx] = isRunning;

                            if (!isRunning)
                                waitUntilRunningQueries[shardNdx] = null;
                        }
                    }

                    // If all shards are running, we're done
                    if (isShardRunning.All(isRunning => isRunning))
                        break;
                }
                catch (EntityShardStartException ex)
                {
                    // Print all exceptions that caused shard init to fail
                    foreach (Exception innerException in ex.Exceptions)
                        _log.Error("Failed to start EntityShard {ShardId}: {Error}", ex.ShardId, innerException);

                    // Exit immediately and wait for process to get restarted
                    Application.Application.ForceTerminate(exitCode: 101, reason: Invariant($"Failed to start EntityShard {ex.ShardId}"));
                }
                catch (EntityShardIllegalStartStateException ex)
                {
                    _log.Error("EntityShard {ShardId} is in unexpected state. Cannot proceed: {Error}", ex.ShardId, ex);

                    // The entity shard is not in the state we expected it to be. We cannot recover. Exit immediately.
                    Application.Application.ForceTerminate(exitCode: 100, reason: Invariant($"EntityShard {ex.ShardId} state desynced on start"));
                }
            }
        }

        async Task<bool> TryStopEntityShardGroupAsync(EntityShardGroup entityGroup)
        {
            // Stop all local EntityShards in the group (in arbitrary order)
            EntityKind[] groupEntityKinds = GetEntityGroupKindsOnNode(_clusterConfig, _selfAddress, entityGroup);

            _log.Information("Stopping EntityShardGroup {EntityGroup}: {EntityShards}", entityGroup, PrettyPrint.Compact(groupEntityKinds));
            bool[] successes = await Task.WhenAll(groupEntityKinds.Select(TryStopEntityShardAsync));
            return successes.All(e => e);
        }

        async Task<bool> TryStopEntityShardAsync(EntityKind entityKind)
        {
            EntityConfigBase entityConfig = EntityConfigRegistry.Instance.GetConfig(entityKind);

            // If already shut down or didn't exist, nothing to do
            if (!_localEntityShards.Remove(entityKind, out IActorRef shardActor))
                return true;

            TimeSpan                shutdownTimeout = entityConfig.ShardShutdownTimeout;
            DateTime                stopAt          = DateTime.UtcNow + shutdownTimeout;
            Task<ShutdownComplete>  askTask         = shardActor.Ask<ShutdownComplete>(ShutdownSync.Instance);

            while (true)
            {
                await Task.WhenAny(askTask, Task.Delay(1000));
                if (!askTask.IsCompleted)
                {
                    if (DateTime.UtcNow > stopAt)
                    {
                        _log.Warning("Failed to stop EntityShard {EntityKind} within the timeout {Exception}", entityKind, entityConfig.ShardShutdownTimeout);
                        return false;
                    }

                    _log.Information("Shutting down EntityShard {ShardName}..", entityKind);
                    continue;
                }

                try
                {
                    await askTask; // raise exceptions
                    return true;
                }
                catch (AskTimeoutException)
                {
                    _log.Warning("Failed to stop EntityShard {EntityKind} within the timeout {Exception}", entityKind, entityConfig.ShardShutdownTimeout);
                    return false;
                }
                catch (Exception ex)
                {
                    _log.Warning("Exception while shutting down EntityShard {ShardName}: {Exception}", entityKind, ex);
                    return false;
                }
            }
        }

        Task TerminateEntityShardGroupAsync(EntityShardGroup entityGroup)
        {
            _log.Information("Terminating EntityShardGroup {EntityGroup}: {EntityShards}", entityGroup, PrettyPrint.Compact(_localEntityConfigs[entityGroup].Select(cfg => cfg.EntityKind).ToArray()));
            // \todo [petri] actually terminate EntityShards

            return Task.CompletedTask;
        }

        async Task ProgressClusterStateAsync()
        {
            ProgressStepResult result;
            do
            {
                result = await StepClusterStateAsync();
            } while (result == ProgressStepResult.CallAgain);
        }

        enum ProgressStepResult
        {
            WaitForUpdate,
            CallAgain
        }

        async Task<ProgressStepResult> StepClusterStateAsync()
        {
            switch (s_phase)
            {
                case ClusterPhase.Connecting:
                    // Wait until this node becomes part of the cluster
                    // and we have connected to all cluster peers.
                    if (_nodeShutdownRequested || _clusterShutdownRequested)
                    {
                        SetClusterPhase(ClusterPhase.Stopping, reason: "ShutdownRequested");
                        return ProgressStepResult.CallAgain;
                    }
                    else if (_clusterEntityShardingState == null)
                    {
                        // No cluster
                        return ProgressStepResult.WaitForUpdate;
                    }
                    else if (_clusterEntityShardingState.TryGetNodeState(_selfAddress) == null)
                    {
                        // We are not a part of the cluster.
                        return ProgressStepResult.WaitForUpdate;
                    }
                    else if (_clusterEntityShardingState.Nodes.Any(node => !node.IsConnected))
                    {
                        // Wait for the node. Or for a new cluster without the node.
                        return ProgressStepResult.WaitForUpdate;
                    }
                    else
                    {
                        _log.Information("Connection established to all cluster peers");
                        SetClusterPhase(ClusterPhase.Starting, reason: "PeersConnected");
                        return ProgressStepResult.CallAgain;
                    }

                case ClusterPhase.Starting:
                    if (_nodeShutdownRequested || _clusterShutdownRequested)
                    {
                        SetClusterPhase(ClusterPhase.Stopping, reason: "ShutdownRequested");
                        return ProgressStepResult.CallAgain;
                    }
                    else if (_selfEntityGroupPhases.All(phase => phase == EntityGroupPhase.Running))
                    {
                        _log.Information("All EntityGroups started, switching to ClusterPhase.Running!");
                        SetClusterPhase(ClusterPhase.Running, reason: "EntityGroups started");
                        return ProgressStepResult.CallAgain;
                    }
                    else
                    {
                        // Figure out which EntityShardGroup we're progressing by finding the first ESG in non-Running phase.
                        // Note that this will skip without a barrier from Running phase of an ESG to creating the next ESG
                        // while the other nodes may still be in Starting phase. This is benign (and saves a barrier) as creating
                        // an ESG does not trigger any other behavior to happen; the entities on it only get initialized in the
                        // Starting phase (which isn't entered until all peers have been created).
                        int                             groupNdx        = Array.FindIndex(_selfEntityGroupPhases, phase => phase != EntityGroupPhase.Running);
                        EntityShardGroup                entityGroup     = (EntityShardGroup)groupNdx;
                        EntityGroupPhase                localPhase      = _selfEntityGroupPhases[groupNdx];
                        IEnumerable<EntityGroupPhase>   allPhases       = _clusterEntityShardingState.Nodes.Select(node => node.EntityGroupPhases[groupNdx]);

                        // Have all peers reached the same stage? This check includes disconnected nodes, and will not proceed since a disconnected nodes state
                        // is to have all groups in NotRunning.
                        // \note: we also compare our own node's phase to checks the localPhase has been published to the ClusterNodeInformation.EntityGroupPhases
                        bool                            syncStartDone   = allPhases.All(phase => phase >= localPhase);

                        // Is the cluster already running? (i.e. is any node Running?) Since all nodes perform sync start, a node in Running means this node
                        // joined the cluster dynamically. In that case we don't need to wait.
                        bool                            startingDone    = _clusterEntityShardingState.Nodes.Any(node => node.IsConnected && node.ClusterPhase == ClusterPhase.Running);
                        bool                            allowProgress   = syncStartDone || startingDone;

                        if (allowProgress)
                        {
                            _log.Information("EntityShardGroup {EntityGroup} starting transition {GroupPhaseNow} -> {GroupPhaseNext} (cluster members: {AllPhases})", entityGroup, localPhase, localPhase+1, PrettyPrint.Compact(allPhases));

                            if (localPhase == EntityGroupPhase.NotCreated)
                                await CreateEntityShardGroupAsync(entityGroup);
                            else if (localPhase == EntityGroupPhase.Created)
                                await StartEntityShardGroupAsync(entityGroup);
                            else
                                throw new InvalidOperationException($"EntityShardGroup {entityGroup} in invalid phase {localPhase}");

                            // Progress the local phase.
                            _selfEntityGroupPhases[groupNdx] = localPhase + 1;

                            // Inform peers
                            SendStateToPeers();

                            return ProgressStepResult.CallAgain;
                        }
                        else
                        {
                            // Wait for something to complete
                            return ProgressStepResult.WaitForUpdate;
                        }
                    }

                case ClusterPhase.Running:
                    if (_nodeShutdownRequested || _clusterShutdownRequested)
                    {
                        SetClusterPhase(ClusterPhase.Stopping, reason: "ShutdownRequested");
                        return ProgressStepResult.CallAgain;
                    }
                    else
                    {
                        return ProgressStepResult.WaitForUpdate;
                    }

                case ClusterPhase.Stopping:
                    if (_selfEntityGroupPhases.All(phase => phase == EntityGroupPhase.NotCreated))
                    {
                        _log.Information("All EntityGroups fully terminated!");
                        s_isCoordinatedShutdown = _clusterShutdownRequested;
                        SetClusterPhase(ClusterPhase.Terminated, reason: "EntityGroups terminated");
                        return ProgressStepResult.CallAgain;
                    }
                    else
                    {
                        // \todo [petri] mostly duplicate code with Starting -- refactor
                        int                             groupNdx        = Array.FindLastIndex(_selfEntityGroupPhases, phase => phase != EntityGroupPhase.NotCreated);
                        EntityShardGroup                entityGroup     = (EntityShardGroup)groupNdx;
                        EntityGroupPhase                localPhase      = _selfEntityGroupPhases[groupNdx];

                        // If this is a node-shutdown, wait for local groups to complete
                        // If this is a cluster-level shutdown, wait for sibling node groups to complete too
                        bool allowProgress;
                        if (!_clusterShutdownRequested)
                        {
                            // No synchronization required.
                            allowProgress = true;
                        }
                        else
                        {
                            if (localPhase == EntityGroupPhase.Created)
                            {
                                // We didn't even start the entity group yet. Kill it immediately.
                                allowProgress = true;
                            }
                            else if (_clusterEntityShardingState.TryGetNodeState(_selfAddress) == null)
                            {
                                // We are not a part of the cluster (anymore). Cannot synchronize
                                allowProgress = true;
                            }
                            else // if (localPhase == EntityGroupPhase.Running)
                            {
                                // We can shut down the entity group if all dependent groups have shut down on all other nodes.
                                allowProgress = _clusterEntityShardingState.Nodes.All(node => node.EntityGroupPhases.Skip(groupNdx + 1).All(phase => phase == EntityGroupPhase.NotCreated));
                            }
                        }

                        if (allowProgress)
                        {
                            // \note: we don't check the return value. Even if the shard fails to shut down, the application shutdown will proceed.
                            _  = await TryStopEntityShardGroupAsync(entityGroup);

                            // Progress the local phase.
                            _selfEntityGroupPhases[groupNdx] = EntityGroupPhase.NotCreated;

                            // Inform peers
                            SendStateToPeers();

                            return ProgressStepResult.CallAgain;
                        }
                        else
                        {
                            // Wait for something to complete
                            return ProgressStepResult.WaitForUpdate;
                        }
                    }

                case ClusterPhase.Terminated:
                    // nothing to do
                    return ProgressStepResult.WaitForUpdate;

                default:
                    throw new InvalidOperationException($"Invalid ClusterPhase {s_phase}");
            }
        }
    }
}
