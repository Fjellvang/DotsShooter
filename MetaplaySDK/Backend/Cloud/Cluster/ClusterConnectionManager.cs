// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Akka.Remote;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static System.FormattableString;

namespace Metaplay.Cloud.Cluster
{
    /// <summary>
    /// Manages the membership of the cluster and tracks the state of the connections to cluster nodes.
    /// Publishes events when state changes.
    /// <para>
    /// Cluster forming works as follows:
    /// </para>
    ///
    /// <para>
    /// All nodes form connections to all other known nodes. Nodes perform handshakes in both directions,
    /// during which they exchange node-specific information with <see cref="ClusterNodeInformation"/>.
    /// </para>
    ///
    /// <para>
    /// A single node is the cluster leader and the rest are followers. The leader announces new cluster topologies
    /// (the set of nodes that are in the cluster) as new nodes join or leave the cluster.
    /// </para>
    ///
    /// <para>
    /// When follower receives the message that a new cluster has been made active, it announces this also to all
    /// peers. This ensures that Node A cannot transition to new cluster topology and send messages to Node B before
    /// Node B has also been made aware of the new cluster topology.
    /// </para>
    ///
    /// <para>
    /// For ordering events, the cluster topologies are timestamped by the leader. The stale updates that are older than
    /// current cluster are ignored.
    /// </para>
    /// </summary>
    public partial class ClusterConnectionManager : MetaReceiveActor
    {
        static Prometheus.Gauge c_clusterExpectedNodes  = Prometheus.Metrics.CreateGauge("metaplay_cluster_expected_nodes_current", "Number of expected nodes in the cluster");
        static Prometheus.Gauge c_clusterNodesConnected = Prometheus.Metrics.CreateGauge("metaplay_cluster_connected_nodes_current", "Number of connected-to nodes in the cluster");
        static Prometheus.Gauge c_clusterPhase = Prometheus.Metrics.CreateGauge("metaplay_cluster_phase", "Metaplay cluster phase", "phase");

        /// <summary>
        /// The internal cluster handshake state of a node.
        /// </summary>
        class ConnectedNodeState
        {
            public bool                     IsLeader;
            public bool                     TheirHandshakeReceived;
            public bool                     OurHandshakeAcknowledged;

            /// <summary>
            /// Only valid after IsHandshakeComplete().
            /// </summary>
            public ClusterNodeInformation   Info;

            public bool IsHandshakeComplete() => TheirHandshakeReceived && OurHandshakeAcknowledged;
        }

        /// <summary>
        /// The internal mutable state of the nodes in the cluster.
        /// </summary>
        class ClusterState
        {
            public ClusterNodeAddress[] Nodes;
            public readonly long        TimestampId;

            public ClusterState(ClusterNodeAddress[] nodes, long timestampId)
            {
                Nodes = nodes;
                TimestampId = timestampId;
            }

            /// <summary>
            /// Returns true if the given set of <paramref name="nodes"/> contains exactly the
            /// same nodes as this cluster. Order of nodes does not matter.
            /// </summary>
            public bool AreMembersEqual(IEnumerable<ClusterNodeAddress> nodes)
            {
                return new HashSet<ClusterNodeAddress>(nodes).SetEquals(Nodes);
            }
        }

        /// <summary>
        /// A node that was unexpectedly lost recently. Unexpectedly lost node
        /// is a node to which connection was lost even though the node was not
        /// in its shutdown phase.
        /// <para>
        /// Gone-too-soon is used to keep track of potentially-only-temporarily-lost
        /// nodes, i.e. a node we assume was lost due to connection being temporarily
        /// lost and which we expect to return soon.
        /// </para>
        /// <para>
        /// As long as a node is in the gone-too-soon state, it is not kicked from an
        /// active cluster topology.
        /// </para>
        /// </summary>
        struct GoneTooSoonNode
        {
            /// <summary>
            /// The timestamp after which this the gone-too-soon record is removed. This
            /// marks the node as completely lost (as opposed to "potentially only temporarily lost").
            /// </summary>
            readonly DateTime _expiresAt;

            GoneTooSoonNode(DateTime expiresAt)
            {
                _expiresAt = expiresAt;
            }

            public static GoneTooSoonNode CreateNew()
            {
                // Node is recent for 1 minute.
                return new GoneTooSoonNode(expiresAt: DateTime.UtcNow + TimeSpan.FromMinutes(1));
            }

            public readonly bool HasExpired() => DateTime.UtcNow >= _expiresAt;
        }

        /// <summary>
        /// Generates IDs that are contain the timestamp of the generation time. The IDs
        /// are strictly increasing. Event if two successive IDs are generated on the same
        /// moment of time, the IDs generated will be different and the latter ID will have
        /// a greater value.
        /// </summary>
        struct TimestampIdGenerator
        {
            long _lastId;

            /// <inheritdoc cref="TimestampIdGenerator"/>
            public long Next()
            {
                long timestampId = DateTime.UtcNow.Ticks;
                long strictlyIncreasing = Math.Max(_lastId + 1, timestampId);
                _lastId = strictlyIncreasing;
                return strictlyIncreasing;
            }
        }

        /// <summary>
        /// All nodes that are connected, i.e. associated in Akka terminology. Nodes may not be part of the cluster or
        /// have even completed handshakes.
        /// </summary>
        readonly Dictionary<ClusterNodeAddress, ConnectedNodeState> _connectedNodes = new();

        /// <summary>
        /// Current cluster members. This is the set of nodes declared by the leader to form the cluster
        /// and it may contain nodes outside the connected nodes. <c>null</c> if there is no cluster.
        /// </summary>
        ClusterState _currentCluster;

        /// <summary>
        /// Is this node the clustering leader.
        /// </summary>
        bool _isLeader;

        /// <summary>
        /// This node's non-clustering custom payload. <c>null</c> if not yet set.
        /// </summary>
        ClusterNodeInformation? _selfNodeInfoMaybe;

        /// <summary>
        /// Token to identify this Process start. If process is restarted, we don't want to get confused
        /// with messages intended for the previous start.
        /// </summary>
        readonly MetaGuid _serverLaunchId;

        /// <summary>
        /// The recently unexpectedly lost nodes. See <see cref="GoneTooSoonNode"/>. Nodes are added
        /// upon unexpected connection losses, and removed on successful reconnections or by expiration.
        /// </summary>
        readonly MetaDictionary<ClusterNodeAddress, GoneTooSoonNode> _nodesGoneTooSoon = new();

        const string                    ActorName                   = "clusterconnection";
        static readonly TimeSpan        TickInterval                = TimeSpan.FromMilliseconds(5_000);
        readonly ClusteringOptions      _clusteringOptions;
        TimestampIdGenerator            _clusterIdGenerator;

        /// <summary>
        /// Called by <see cref="CreateActor"/>
        /// </summary>
        public ClusterConnectionManager()
        {
            _clusteringOptions = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>();
            _isLeader = _clusteringOptions.IsCurrentNodeClusterLeader;
            _serverLaunchId = MetaGuid.New();

            RegisterHandlers();
        }

        void RegisterHandlers()
        {
            Receive<AssociatedEvent>(ReceiveAssociatedEvent);
            Receive<DisassociatedEvent>(ReceiveDisassociatedEvent);
            Receive<ClusterHandshake>(ReceiveClusterHandshake);
            Receive<ClusterHandshakeAck>(ReceiveClusterHandshakeAck);
            Receive<ClusterNodeInfoUpdate>(ReceiveClusterNodeInfoUpdate);
            Receive<ClusterTopologyChanged>(ReceiveClusterTopologyChanged);
            Receive<ActorTick>(ReceiveActorTick);

            Receive<AddNewSubscriber>(ReceiveAddNewSubscriber);
            Receive<Terminated>(ReceiveTerminated);
            Receive<SetSelfClusterNodeInfoCommand>(ReceiveSetSelfClusterNodeInfoCommand);
            Receive<ClusterDebugStatusRequest>(ReceiveClusterDebugStatusRequest);
        }

        protected override void PreStart()
        {
            base.PreStart();
            Context.System.Scheduler.ScheduleTellRepeatedly(initialDelay: TimeSpan.Zero, TickInterval, _self, ActorTick.Instance, ActorRefs.NoSender, _cancelTimers);

            // Subscribe to Akka.Remote Association events (Associated, Disassociated)
            Context.System.EventStream.Subscribe(_self, typeof(AssociatedEvent));
            Context.System.EventStream.Subscribe(_self, typeof(DisassociatedEvent));
        }

        protected override void PostStop()
        {
            Context.System.EventStream.Unsubscribe(_self, typeof(AssociatedEvent));
            Context.System.EventStream.Unsubscribe(_self, typeof(DisassociatedEvent));

            base.PostStop();
        }

        void ReceiveAssociatedEvent(AssociatedEvent associated)
        {
            // Node connected. Start handshake.
            Address address = associated.RemoteAddress;
            ClusterNodeAddress nodeAddress = new ClusterNodeAddress(address.Host, address.Port.Value);

            _log.Information("Associated with {RemoteAddress}", nodeAddress);
            if (!_connectedNodes.TryAdd(nodeAddress, new ConnectedNodeState()))
            {
                // Sometimes Akka.Net sends additional Associated events for already associated nodes, tolerate by ignoring the extra events.
                _log.Information("Received AssociatedEvent for a node that was already associated: {RemoteAddress}", nodeAddress);
                return;
            }

            _log.Debug("Cluster status is now: {Cluster}", ClusterToDebugString(_currentCluster));

            // If this node hasn't resolved the node info, we can't handshake yet.
            if (_selfNodeInfoMaybe is not ClusterNodeInformation selfNodeInfo)
            {
                _log.Warning("Received AssociatedEvent but local node info is not yet available. Waiting for info before proceeding.");
                return;
            }

            SendHandshake(nodeAddress, selfNodeInfo);
        }

        void ReceiveDisassociatedEvent(DisassociatedEvent disassociated)
        {
            // Node disconnected. Remove it from the connected list
            Address address = disassociated.RemoteAddress;
            ClusterNodeAddress nodeAddress = new ClusterNodeAddress(address.Host, address.Port.Value);

            _log.Information("DisassociatedEvent for {RemoteAddress}", nodeAddress);
            if (!_connectedNodes.Remove(nodeAddress, out ConnectedNodeState nodeState))
            {
                // \note: Don't print on error - Akka.Net emits these when it cannot connect to a node
                _log.Information("Received DisassociatedEvent for a node that was already disassociated: {RemoteAddress}", nodeAddress);
                return;
            }

            // If the node was a part of the current cluster, inform all listeners the node has is unavailable.
            if (_currentCluster != null && _currentCluster.Nodes.Contains(nodeAddress))
            {
                _log.Debug("Cluster status is now: {Cluster}", ClusterToDebugString(_currentCluster));
                PublishEvent(CreateCurrentClusterStateEvent());
            }

            // If the node is lost and we know it wasn't shutting down, add it to the recent nodes.
            if (nodeState.IsHandshakeComplete() && !nodeState.Info.IsNodeShuttingDown())
            {
                _nodesGoneTooSoon.AddOrReplace(nodeAddress, GoneTooSoonNode.CreateNew());
            }

            // Leader:
            //  * If the node was a part of the current cluster, update the topology
            if (_isLeader)
            {
                if (_currentCluster != null && _currentCluster.Nodes.Contains(nodeAddress))
                {
                    TryCreateNewClusterTopology();
                }
            }
        }

        void ReceiveClusterHandshake(ClusterHandshake handshake)
        {
            Address address = Sender.Path.Address;
            ClusterNodeAddress nodeAddress = new ClusterNodeAddress(address.Host, address.Port.Value);

            if (!_connectedNodes.TryGetValue(nodeAddress, out ConnectedNodeState nodeState))
            {
                // This can happen if the actor started late and missed AssociatedEvent. Tolerate by assuming we are connected.
                _log.Information("Received ClusterHandshake from a node that was not connected: {RemoteAddress}", nodeAddress);
                nodeState = new ConnectedNodeState();
                _connectedNodes.Add(nodeAddress, nodeState);

                // Send handshake just in case, if possible. AssociatedEvent should handle it, but we have missed it.
                if (_selfNodeInfoMaybe is ClusterNodeInformation selfNodeInfo)
                    SendHandshake(nodeAddress, selfNodeInfo);
            }

            // Check remote version. Cookie mismatch is an error, others are just weird. On non-happy path, print all
            // fields for a better experience.
            if (handshake.Cookie != _clusteringOptions.Cookie)
            {
                _log.Error(
                    "Received ClusterHandshake for with invalid Cookie from {RemoteAddress}, ignoring:\n" +
                    " Remote sent cookie {RemoteCookie}, but we expected {ExpectedCookie}\n" +
                    " Remote sent BuildNumber {RemoteBuildNumber}, and we have {ExpectedBuildNumber} ({BuildNumberStatus})\n" +
                    " Remote sent CommitId {RemoteCommitId}, and we have {ExpectedCommitId} ({CommitIdStatus})",
                    address,
                    handshake.Cookie, _clusteringOptions.Cookie,
                    handshake.BuildNumber, CloudCoreVersion.BuildNumber, (handshake.BuildNumber == CloudCoreVersion.BuildNumber ? "matching" : "not matching"),
                    handshake.CommitId ?? "<not set>", CloudCoreVersion.CommitId ?? "<not set>", (handshake.CommitId == CloudCoreVersion.CommitId ? "matching" : "not matching"));
                return;
            }
            else if (handshake.BuildNumber != CloudCoreVersion.BuildNumber || handshake.CommitId != CloudCoreVersion.CommitId)
            {

                _log.Warning(
                    "Received ClusterHandshake from {RemoteAddress} with a different build version. Since Cookie is matching, we continue handshake:\n" +
                    " Remote sent cookie {RemoteCookie} which matches ours\n" +
                    " Remote sent BuildNumber {RemoteBuildNumber}, and we have {ExpectedBuildNumber} ({BuildNumberStatus})\n" +
                    " Remote sent CommitId {RemoteCommitId}, and we have {ExpectedCommitId} ({CommitIdStatus})",
                    address,
                    handshake.Cookie,
                    handshake.BuildNumber, CloudCoreVersion.BuildNumber, (handshake.BuildNumber == CloudCoreVersion.BuildNumber ? "matching" : "not matching"),
                    handshake.CommitId ?? "<not set>", CloudCoreVersion.CommitId ?? "<not set>", (handshake.CommitId == CloudCoreVersion.CommitId ? "matching" : "not matching"));
            }

            // Ack all valid handshakes.
            SendToNode(nodeAddress, new ClusterHandshakeAck(handshake.ReplyToken));

            // Store handshake information. Info might change during the process.
            nodeState.IsLeader = handshake.IsLeader;

            bool infoChanged = nodeState.Info != handshake.Info;
            nodeState.Info = handshake.Info;

            // Handshake already handled? Then handle as an info update.
            if (nodeState.TheirHandshakeReceived)
            {
                if (infoChanged)
                    OnNodeInfoUpdated(nodeAddress, nodeState);
                return;
            }

            // Store static handshake information
            nodeState.TheirHandshakeReceived = true;

            // Handle handshake completion
            bool didCompleteHandshake = nodeState.IsHandshakeComplete();
            if (didCompleteHandshake)
                OnNodeCompletedHandshake(nodeAddress);
        }

        void ReceiveClusterHandshakeAck(ClusterHandshakeAck handshake)
        {
            Address address = Sender.Path.Address;
            ClusterNodeAddress nodeAddress = new ClusterNodeAddress(address.Host, address.Port.Value);

            if (_serverLaunchId != handshake.ReplyToken)
            {
                _log.Error("Received ClusterHandshakeAck with a wrong token: {RemoteAddress}", nodeAddress);
                return;
            }

            if (!_connectedNodes.TryGetValue(nodeAddress, out ConnectedNodeState nodeState))
            {
                // This can happen if the actor started late and missed AssociatedEvent. Tolerate by assuming we are connected.
                _log.Warning("Received ClusterHandshakeAck from a node that was not connected: {RemoteAddress}", nodeAddress);
                nodeState = new ConnectedNodeState();
                _connectedNodes.Add(nodeAddress, nodeState);
            }

            // Already handled?
            if (nodeState.OurHandshakeAcknowledged)
                return;

            nodeState.OurHandshakeAcknowledged = true;

            // Handle handshake completion
            bool didCompleteHandshake = nodeState.IsHandshakeComplete();
            if (didCompleteHandshake)
                OnNodeCompletedHandshake(nodeAddress);
        }

        void OnNodeCompletedHandshake(ClusterNodeAddress nodeAddress)
        {
            _log.Information("Node {NodeAddress} completed handshake", nodeAddress);

            // If the node is part of the current cluster, announce the node has returned.
            // Note that the initial disassociation might have started cluster topology change that
            // might happen soon and may remove this node.
            if (_currentCluster != null && _currentCluster.Nodes.Contains(nodeAddress))
            {
                _log.Debug("Cluster status is now: {Cluster}", ClusterToDebugString(_currentCluster));
                PublishEvent(CreateCurrentClusterStateEvent());
            }

            // Remove the node from the unexpectedly lost list since it's now back.
            _nodesGoneTooSoon.Remove(nodeAddress);

            // Leader:
            //   * Inform node of the current cluster state. I.e. Play back the updates it might have missed.
            //   * Update cluster topology (i.e. commonly add the node into the cluster).
            if (_isLeader)
            {
                // Playback latest cluster state
                if (_currentCluster != null)
                {
                    ClusterTopologyChanged topologyChanged = new ClusterTopologyChanged(_currentCluster.Nodes, _currentCluster.TimestampId);
                    SendToNode(nodeAddress, topologyChanged);
                }

                TryCreateNewClusterTopology();
            }
        }

        void ReceiveClusterNodeInfoUpdate(ClusterNodeInfoUpdate nodeInfoUpdate)
        {
            Address address = Sender.Path.Address;
            ClusterNodeAddress nodeAddress = new ClusterNodeAddress(address.Host, address.Port.Value);

            if (!_connectedNodes.TryGetValue(nodeAddress, out ConnectedNodeState nodeState))
            {
                _log.Error("Received ClusterNodeInfoUpdate from a node that was not connected: {RemoteAddress}", nodeAddress);
                return;
            }

            bool infoChanged = nodeState.Info != nodeInfoUpdate.Info;
            nodeState.Info = nodeInfoUpdate.Info;
            if (infoChanged)
                OnNodeInfoUpdated(nodeAddress, nodeState);
        }

        void OnNodeInfoUpdated(ClusterNodeAddress nodeAddress, ConnectedNodeState nodeState)
        {
            // When cluster nodes info changes, we reannounce the cluster state to local listeners.
            //
            // We do this only if this node is a part of the current cluster (it wouldn't be part of the cluster update event otherwise),
            // and the node is fully completed handshake. If handshake is still pending, the cluster update event will be published when
            // the handshake completes.
            //
            // We could push the update even before the handshake is completed. That is not useful though -- The consumer of the
            // ClusterEvent cannot know if Info is up-to-date if the handshakes haven't been completed yet.
            if (nodeState.IsHandshakeComplete() && _currentCluster != null && _currentCluster.Nodes.Contains(nodeAddress))
            {
                PublishEvent(CreateCurrentClusterStateEvent());
            }
        }

        void ReceiveClusterTopologyChanged(ClusterTopologyChanged topologyChanged)
        {
            Address address = Sender.Path.Address;
            ClusterNodeAddress nodeAddress = new ClusterNodeAddress(address.Host, address.Port.Value);

            if (_isLeader)
            {
                _log.Error("Received ClusterTopologyChanged but the node was a leader");
                return;
            }

            // Skip if we have seen this already. As nodes send this to each other, we expect
            // to get this from all nodes.
            if (_currentCluster != null && _currentCluster.TimestampId == topologyChanged.TimestampId)
                return;

            // Ignore if cluster change is older than we have already received.
            if (_currentCluster != null && _currentCluster.TimestampId > topologyChanged.TimestampId)
            {
                _log.Debug("Received stale ClusterTopologyChanged (Id was {ReceivedId}, currently active {ExpectedId}) from: {RemoteAddress}", topologyChanged.TimestampId, _currentCluster.TimestampId, nodeAddress);
                return;
            }

            // Skip if we haven't set our current nodes info. If we haven't set the info, it means
            // we haven't been able to make handshakes, so the received message must be stale. This
            // shouldn't happen (since nodes should only send topology update to nodes that have
            // completed handshakes, which requires the info) but it's easy to check here.
            //
            // Dropping the message is not a problem. When the node info is set, this node will
            // handshake with the peers and after handshake with Leader, will receive new
            // TopologyUpdate.
            if (_selfNodeInfoMaybe is null)
            {
                _log.Error("Received ClusterTopologyChanged before this node was has set the node info. Ignoring.");
                return;
            }

            // Otherwise this is the first cluster change or we are going forward. Accept.
            _currentCluster = MakeClusterStateFromTopologyChange(topologyChanged);
            _log.Information("Cluster topology changed: {Cluster}", ClusterToDebugString(_currentCluster));

            // Forward to all other nodes, except Leader and sender.
            // By forwarding, we make sure that if we send any messages to that node, the other node must have received the ClusterTopologyChanged message.
            foreach ((ClusterNodeAddress peerAddress, ConnectedNodeState peerState) in _connectedNodes)
            {
                if (peerState.IsLeader)
                    continue;
                if (peerAddress == nodeAddress)
                    continue;
                if (!peerState.IsHandshakeComplete())
                    continue;
                SendToNode(peerAddress, topologyChanged);
            }

            // Inform local listeners
            PublishEvent(CreateCurrentClusterStateEvent());
        }

        void ReceiveActorTick(ActorTick tick)
        {
            // Metrics and logs
            // \note: By convention we count ourselves as connected but only we have completed the initial state setting
            int numLocalNodes = _selfNodeInfoMaybe.HasValue ? 1 : 0;
            int numRemoteNodes = _connectedNodes.Count(nodeAddressState => nodeAddressState.Value.IsHandshakeComplete());
            int numConnectedNodes = numRemoteNodes + numLocalNodes;
            int numNodesInCluster = _currentCluster == null ? 0 : _currentCluster.Nodes.Length;
            string role = _isLeader ? "Leader" : "Follower";

            if (!_selfNodeInfoMaybe.HasValue)
            {
                _log.Information("<{Role}> Tick: Waiting for local node to initialize and provide the initial state. {NumConnectedNodes} nodes connected", role, numConnectedNodes);
            }
            else if (_currentCluster == null)
            {
                _log.Information("<{Role}> Tick: Waiting for valid initial topology. {NumConnectedNodes} nodes connected.", role, numConnectedNodes);
            }
            else
            {
                // For a non-server, running singleton application, don't print anything. This information
                // is only useful for non-singletons and for server (on server, the periodic print shows the application is alive in logs)
                if (!RuntimeOptionsBase.IsServerApplication && numNodesInCluster == 1 && numConnectedNodes == 1 && _selfNodeInfoMaybe.Value.LocalPhase == ClusterPhase.Running)
                {
                    // Not interesting case.
                }
                else
                {
                    _log.Information("<{Role}> Tick: Running. {NumConnectedNodes}/{NumClusterNodes} cluster nodes connected. Local phase: {LocalPhase}", role, numConnectedNodes, numNodesInCluster, _selfNodeInfoMaybe.Value.LocalPhase.ToString());
                }
            }

            c_clusterExpectedNodes.Set(numNodesInCluster);
            c_clusterNodesConnected.Set(numConnectedNodes);

            // \note: This is technically ClusterCoordinator's state but we report all clustering and clustering-like metrics here.
            if (_selfNodeInfoMaybe is ClusterNodeInformation selfNodeInfo)
            {
                foreach (ClusterPhase phase in EnumUtil.GetValues<ClusterPhase>())
                    c_clusterPhase.WithLabels(phase.ToString()).Set(selfNodeInfo.LocalPhase == phase ? 1.0 : 0.0);
            }

            // Periodic reconnects
            TryConnectToAllNodes();

            if (_isLeader)
            {
                // Periodic attempts to form a (bigger) cluster
                TryCreateNewClusterTopology();
            }

            // Remove expired records of lost nodes
            _nodesGoneTooSoon.RemoveWhere(kv => kv.Value.HasExpired());
        }

        void TryConnectToAllNodes()
        {
            // If node info not yet resolved, we can't handshake. And hence we cannot connect yet
            if (_selfNodeInfoMaybe is not ClusterNodeInformation selfNodeInfo)
                return;

            // Connect to all missing nodes:
            HashSet<ClusterNodeAddress> nodesToConnectTo = new HashSet<ClusterNodeAddress>();

            // For each node in the current cluster.
            if (_currentCluster != null)
            {
                foreach (ClusterNodeAddress clusterMemberNode in _currentCluster.Nodes)
                {
                    nodesToConnectTo.Add(clusterMemberNode);
                }
            }

            // For each static node.
            foreach (NodeSetConfig nodeSet in _clusteringOptions.ClusterConfig.EnumerateNodeSets())
            {
                if (nodeSet.ScalingMode != NodeSetScalingMode.Static)
                    continue;

                for (int nodeIndex = 0; nodeIndex < nodeSet.StaticNodeCount; ++nodeIndex)
                {
                    ClusterNodeAddress staticNode = nodeSet.ResolveNodeAddress(nodeIndex);
                    nodesToConnectTo.Add(staticNode);
                }
            }

            // For all known expected dynamic nodes. Nodes are between min and max, and
            // if a node N exists, then all nodes (N-1, N-2, .. 0) exist too.
            //
            // Note that we don't speculatively probe nodes N+1, N+2.. Instead we let them
            // wake up in peace and have them connect to Leader/Static subset first to announce
            // themselves.
            Dictionary<ObjectIdentity, int> highestKnowDynamicNodeIndexPerNodeSet = new();
            foreach (ClusterNodeAddress address in _connectedNodes.Keys.Append(_clusteringOptions.SelfAddress))
            {
                foreach (NodeSetConfig nodeSet in _clusteringOptions.ClusterConfig.EnumerateNodeSets())
                {
                    if (nodeSet.ScalingMode != NodeSetScalingMode.DynamicLinear)
                        continue;
                    if (!nodeSet.ResolveNodeIndex(address, out int nodeIndex))
                        continue;

                    highestKnowDynamicNodeIndexPerNodeSet[new ObjectIdentity(nodeSet)] = Math.Max(nodeIndex, highestKnowDynamicNodeIndexPerNodeSet.GetValueOrDefault(new ObjectIdentity(nodeSet), defaultValue: 0));
                    break;
                }
            }

            foreach (NodeSetConfig nodeSet in _clusteringOptions.ClusterConfig.EnumerateNodeSets())
            {
                if (nodeSet.ScalingMode != NodeSetScalingMode.DynamicLinear)
                    continue;

                int numExpectedNodes = Math.Clamp(
                    min:    nodeSet.DynamicLinearMinNodeCount,
                    value:  highestKnowDynamicNodeIndexPerNodeSet.GetValueOrDefault(new ObjectIdentity(nodeSet), defaultValue: 0) + 1, // \note: +1 to convert from highest index to count
                    max:    nodeSet.DynamicLinearMaxNodeCount);

                for (int nodeIndex = 0; nodeIndex < numExpectedNodes; ++nodeIndex)
                {
                    ClusterNodeAddress staticNode = nodeSet.ResolveNodeAddress(nodeIndex);
                    nodesToConnectTo.Add(staticNode);
                }
            }

            // For each recent node.
            foreach ((ClusterNodeAddress nodeAddress, GoneTooSoonNode goneTooSoon) in _nodesGoneTooSoon)
            {
                if (!goneTooSoon.HasExpired())
                    nodesToConnectTo.Add(nodeAddress);
            }

            // Try to connect if we haven't yet.
            foreach (ClusterNodeAddress nodeAddress in nodesToConnectTo)
            {
                bool shouldSendHandshake;

                if (nodeAddress == _clusteringOptions.SelfAddress)
                {
                    // No need to greet itself.
                    shouldSendHandshake = false;
                }
                else if (!_connectedNodes.TryGetValue(nodeAddress, out ConnectedNodeState nodeState))
                {
                    // Not connected to the node. Say hello.
                    shouldSendHandshake = true;
                }
                else if (!nodeState.OurHandshakeAcknowledged)
                {
                    // If the other hasn't replied to our hello, we send hello again
                    shouldSendHandshake = true;
                }
                else
                {
                    // Already acked, no need to do anything.
                    shouldSendHandshake = false;
                }

                if (shouldSendHandshake)
                    SendHandshake(nodeAddress, selfNodeInfo);
            }
        }

        /// <summary>
        /// Leader only. Creates the maximum size cluster currently possible. If the resulting cluster is
        /// the same the current cluster, the current cluster is kept. If there is no valid cluster possible,
        /// this method does nothing.
        /// </summary>
        void TryCreateNewClusterTopology()
        {
            // We only want to form a cluster if we have info for all nodes. The handshake check
            // ensures that in most cases. Handshakes cannot be formed without the info, so handshakes
            // cannot be complete. Except if this cluster has only one node, the current. Wait for the
            // info to keep behavior consistent.
            if (_selfNodeInfoMaybe is null)
                return;

            // Choose nodes to form a cluster
            List<ClusterNodeAddress> clusterNodes = new();
            foreach (NodeSetConfig nodeSetDef in _clusteringOptions.ClusterConfig.EnumerateNodeSets())
            {
                if (nodeSetDef.ScalingMode == NodeSetScalingMode.Static)
                {
                    // Fill in the static part of the cluster
                    for (int nodeIndex = 0; nodeIndex < nodeSetDef.StaticNodeCount; ++nodeIndex)
                    {
                        ClusterNodeAddress address = nodeSetDef.ResolveNodeAddress(nodeIndex);

                        // Static nodes are always part of every cluster topology. Add them to the topology, regardless
                        // if they are connected or not. If they are not connected, they will appear as Disconnected nodes.
                        clusterNodes.Add(address);
                    }
                }
                else if (nodeSetDef.ScalingMode == NodeSetScalingMode.DynamicLinear)
                {
                    // Fill in the largest set (from 0 to N) such that it contains all suitable nodes.
                    // This means nodes that are not suitable may be added in order to add another
                    // suitable node with a higher index.
                    //
                    // Practically, if you have nodes node-1, node-2 and node-3 and node-2 is unavailable,
                    // we try to form a cluster with [node-1, node-2, node-3].
                    //
                    // The smallest set size that contains all suitable nodes.
                    //
                    // Similar to the static nodes, the minimum size of dynamic nodeset are always part of every cluster
                    // topology. They are effectively static. Add these "static" nodes to the topology, regardless
                    // if they are connected or not. If they are not connected, they will appear as Disconnected nodes.
                    int largestSetLength = nodeSetDef.DynamicLinearMinNodeCount;
                    for (int nodeIndex = nodeSetDef.DynamicLinearMinNodeCount; nodeIndex < nodeSetDef.DynamicLinearMaxNodeCount; ++nodeIndex)
                    {
                        ClusterNodeAddress address = nodeSetDef.ResolveNodeAddress(nodeIndex);

                        if (IsNodeSuitableNodeForNewCluster(address))
                        {
                            // This node is suitable. So the smallest set must contain at least this node.
                            largestSetLength = nodeIndex + 1;
                        }
                    }

                    // Add all suitable nodes, and the other nodes between.
                    for (int nodeIndex = 0; nodeIndex < largestSetLength; ++nodeIndex)
                    {
                        ClusterNodeAddress address = nodeSetDef.ResolveNodeAddress(nodeIndex);
                        clusterNodes.Add(address);
                    }
                }
                else
                    throw new InvalidOperationException($"Invalid scaling mode {nodeSetDef.ScalingMode}");
            }

            // If equal to the current cluster, skip
            if (_currentCluster != null && _currentCluster.AreMembersEqual(clusterNodes))
                return;

            // Apply the new topology
            long timestampId = _clusterIdGenerator.Next();
            ClusterTopologyChanged topologyChanged = new ClusterTopologyChanged(clusterNodes.ToArray(), timestampId);

            _currentCluster = MakeClusterStateFromTopologyChange(topologyChanged);
            _log.Information("New cluster topology created: {Cluster}", ClusterToDebugString(_currentCluster));

            // Broadcast to all nodes, even those who are outside the selected nodes
            foreach ((ClusterNodeAddress address, ConnectedNodeState state) in _connectedNodes)
            {
                if (state.IsHandshakeComplete())
                    SendToNode(address, topologyChanged);
            }

            PublishEvent(CreateCurrentClusterStateEvent());
        }

        /// <summary>
        /// Leader only. Determines if a node can be added into the cluster.
        /// </summary>
        bool IsNodeSuitableNodeForNewCluster(ClusterNodeAddress address)
        {
            // The current node (leader) is always connected and a valid member.
            if (address == _clusteringOptions.SelfAddress)
                return true;

            bool nodeIsMemberOfTheCurrentCluster = _currentCluster != null && _currentCluster.Nodes.Contains(address);

            // If node is connected and has completed handshake, it is valid.
            if (_connectedNodes.TryGetValue(address, out ConnectedNodeState nodeState))
            {
                if (nodeState.IsHandshakeComplete())
                {
                    // Unless this would be a new node to the cluster and the node is shutting down.
                    if (!nodeIsMemberOfTheCurrentCluster && nodeState.Info.IsNodeShuttingDown())
                        return false;

                    return true;
                }
            }

            // If the connection to the node was lost recently and unexpectedly, we allow some time for node to
            // come back. We only do this when the node is part of the current cluster.
            if (nodeIsMemberOfTheCurrentCluster && _nodesGoneTooSoon.TryGetValue(address, out GoneTooSoonNode goneTooSoon) && !goneTooSoon.HasExpired())
            {
                return true;
            }

            return false;
        }

        ClusterState MakeClusterStateFromTopologyChange(ClusterTopologyChanged topologyChanged)
        {
            return new ClusterState(topologyChanged.Nodes, topologyChanged.TimestampId);
        }

        void InternalSetSelfClusterNodeInfo(ClusterNodeInformation info)
        {
            bool isFirstTimeSetup = _selfNodeInfoMaybe is null;

            // If value doesn't change, ignore the update.
            if (!isFirstTimeSetup && _selfNodeInfoMaybe.Value == info)
                return;

            _selfNodeInfoMaybe = info;

            // Update info to all connected nodes.
            foreach (ClusterNodeAddress address in _connectedNodes.Keys)
            {
                SendToNode(address, new ClusterNodeInfoUpdate(info));
            }

            // If this node was part the cluster, reannounce cluster
            if (_currentCluster != null && _currentCluster.Nodes.Contains(_clusteringOptions.SelfAddress))
                PublishEvent(CreateCurrentClusterStateEvent());

            // Setting node info might have unblocked the handshake. Continue handshake by continuing connecting.
            if (isFirstTimeSetup)
                TryConnectToAllNodes();

            // On leader, the cluster might have been blocked by this node's info being missing.
            // Attempt to continue immediately
            if (isFirstTimeSetup && _isLeader)
            {
                TryCreateNewClusterTopology();
            }
        }

        void SendHandshake(ClusterNodeAddress nodeAddress, ClusterNodeInformation info)
        {
            // \note: info is always _currentNodeInfoMaybe.Value but we force call site to unwrap it.
            //        This make the preconditions for this call clear.
            SendToNode(nodeAddress, new ClusterHandshake(
                isLeader:       _isLeader,
                cookie:         _clusteringOptions.Cookie,
                replyToken:     _serverLaunchId,
                info:           info,
                buildNumber:    CloudCoreVersion.BuildNumber,
                commitId:       CloudCoreVersion.CommitId
                ));
        }

        void SendToNode(ClusterNodeAddress nodeAddress, IClusterConnectionNetworkMessage message)
        {
            ActorSelection selection = RemoteNodeUtil.GetRemoteActorSelection(Context.System, nodeAddress, actorPath: ActorName);
            selection.Tell(message);
        }

        string ClusterToDebugString(ClusterState state)
        {
            if (state == null)
                return "(<null>, no cluster state)";

            MetaDictionary<string, MetaDictionary<int, ClusterNodeAddress>> nodesByNodesetAndIndex = new ();
            foreach (ClusterNodeAddress node in state.Nodes)
            {
                foreach (NodeSetConfig nodeSet in _clusteringOptions.ClusterConfig.EnumerateNodeSets())
                {
                    if (nodeSet.ResolveNodeIndex(node, out int nodeIndex))
                    {
                        if (!nodesByNodesetAndIndex.ContainsKey(nodeSet.ShardName))
                            nodesByNodesetAndIndex.Add(nodeSet.ShardName, new MetaDictionary<int, ClusterNodeAddress>());

                        nodesByNodesetAndIndex[nodeSet.ShardName][nodeIndex] = node;
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            bool firstNodeSet = true;
            sb.Append("(");
            foreach (NodeSetConfig nodeSetDef in _clusteringOptions.ClusterConfig.EnumerateNodeSets())
            {
                if (!nodesByNodesetAndIndex.TryGetValue(nodeSetDef.ShardName, out var nodesByIndex))
                    continue;

                if (!firstNodeSet)
                    sb.Append(' ');
                firstNodeSet = false;

                sb.Append($"{nodeSetDef.ShardName}: [");

                bool firstIndex = true;
                foreach (int index in nodesByIndex.Keys.Order())
                {
                    ClusterNodeAddress node = nodesByIndex[index];

                    if (!firstIndex)
                        sb.Append(' ');
                    firstIndex = false;

                    sb.Append(Invariant($"{index}"));
                    sb.Append('=');
                    sb.Append(node);

                    if (node == _clusteringOptions.SelfAddress)
                        sb.Append(" (self)");
                    else if (!_connectedNodes.TryGetValue(node, out ConnectedNodeState nodeState))
                        sb.Append(" (disconnected)");
                    else if (!nodeState.IsHandshakeComplete())
                        sb.Append(" (pending-handshake)");
                }

                sb.Append(']');
            }
            sb.Append(Invariant($" Id: {state.TimestampId})"));

            return sb.ToString();
        }
    }
}
