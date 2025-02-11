// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Cluster
{
    partial class ClusterConnectionManager
    {
        /// <summary>
        /// Marker for all locally published events.
        /// </summary>
        public interface IClusterConnectionEvent
        {
        }

        /// <summary>
        /// Locally published event. Sent when the current cluster changes or any cluster node's Info changes.
        /// <para>
        /// This event contains the new cluster state. If you want to detect events such as node additions or
        /// removals, you must manually compare the cluster state between events.
        /// </para>
        /// </summary>
        public class ClusterChangedEvent : IClusterConnectionEvent
        {
            public readonly struct ClusterMember
            {
                /// <summary>
                /// The address to the node.
                /// </summary>
                public readonly ClusterNodeAddress Address;

                /// <summary>
                /// If the node is currently connected with this node. Connected means it has completed handshakes.
                /// </summary>
                public readonly bool IsConnected;

                /// <summary>
                /// Latest node information. Only set if <see cref="IsConnected" />.
                /// </summary>
                public readonly ClusterNodeInformation Info;

                public ClusterMember(ClusterNodeAddress address, bool isConnected, ClusterNodeInformation info)
                {
                    Address = address;
                    IsConnected = isConnected;
                    Info = info;
                }
            }

            /// <summary>
            /// All nodes of the updated cluster.
            /// </summary>
            public readonly ClusterMember[] Members;

            /// <summary>
            /// The unique, strictly increasing Id of this event. Note that this is not the
            /// timestamp of the topology. Each event, even for non-topology reasons, such as
            /// info-change, will result in a new event with a new, larger event Id.
            /// </summary>
            public readonly long            EventTimestampId;

            public ClusterChangedEvent(ClusterMember[] members, long eventTimestampId)
            {
                Members = members;
                EventTimestampId = eventTimestampId;
            }
        }

        static IActorRef s_actor;

        public static IActorRef CreateActor(ActorSystem system)
        {
            if (s_actor != null)
                throw new InvalidOperationException("CreateActor() must not be called multiple times");

            s_actor = system.ActorOf(Props.Create<ClusterConnectionManager>(), name: ActorName);
            return s_actor;
        }

        #region Subscriptions

        class AddNewSubscriber
        {
            public readonly IActorRef Actor;
            public readonly bool      ExpectReply;
            public AddNewSubscriber(IActorRef actor, bool expectReply)
            {
                Actor = actor;
                ExpectReply = expectReply;
            }
        }

        OrderedSet<IActorRef>   _eventSubscribers = new();
        TimestampIdGenerator    _eventIdGenerator;

        /// <summary>
        /// Subscribes to <see cref="ClusterChangedEvent"/> events.
        /// Upon subscribe, the latest <see cref="ClusterChangedEvent"/> will be sent to the subscriber, provided there is an active cluster.
        /// Subscriber is unsubscribed automatically when the subscriber entity terminates.
        /// </summary>
        public static void SubscribeToClusterEvents(IActorRef listener)
        {
            if (s_actor == null)
                throw new InvalidOperationException("Not allowed to call ClusterConnectionManager.SubscribeToClusterEvents() without calling ClusterConnectionManager.CreateActor() first.");
            s_actor.Tell(new AddNewSubscriber(listener, expectReply: false));
        }

        /// <summary>
        /// Subscribes to <see cref="ClusterChangedEvent"/> events.
        /// Upon subscribe, the latest <see cref="ClusterChangedEvent"/> is returned, if any. If there is no current
        /// cluster, <c>null</c> is returned.
        /// Subscriber is unsubscribed automatically when the subscriber entity terminates.
        /// </summary>
        public static async Task<ClusterChangedEvent> SubscribeToClusterEventsAsync(IActorRef listener)
        {
            if (s_actor == null)
                throw new InvalidOperationException("Not allowed to call ClusterConnectionManager.SubscribeToClusterEventsAsync() without calling ClusterConnectionManager.CreateActor() first.");

            return await s_actor.Ask<ClusterChangedEvent>(new AddNewSubscriber(listener, expectReply: true), timeout: TimeSpan.FromSeconds(10));
        }

        void ReceiveAddNewSubscriber(AddNewSubscriber subscriber)
        {
            _eventSubscribers.Add(subscriber.Actor);
            Context.Watch(subscriber.Actor);

            if (subscriber.ExpectReply)
            {
                // Reply current state
                Tell(Sender, CreateCurrentClusterStateEvent());
            }
            else
            {
                // Synthetic update message to get to the current state.
                if (_currentCluster != null)
                    subscriber.Actor.Tell(CreateCurrentClusterStateEvent());
            }
        }

        void ReceiveTerminated(Terminated terminated)
        {
            _eventSubscribers.Remove(terminated.ActorRef);
        }

        ClusterChangedEvent CreateCurrentClusterStateEvent()
        {
            List<ClusterChangedEvent.ClusterMember> members = new();

            foreach (ClusterNodeAddress address in _currentCluster.Nodes)
            {
                bool isConnected = false;
                ClusterNodeInformation info = default;

                if (_connectedNodes.TryGetValue(address, out ConnectedNodeState nodeState))
                {
                    if (nodeState.IsHandshakeComplete())
                    {
                        isConnected = true;
                        info = nodeState.Info;
                    }
                }
                else if (address == _clusteringOptions.SelfAddress)
                {
                    // Local node is "connected" to the cluster always.
                    isConnected = true;
                    // _selfNodeInfoMaybe is always set here. A node cannot send handshakes before the
                    // nodeInfo is set. ClusterUpdates are only sent to handshaken nodes.
                    info = _selfNodeInfoMaybe.Value;
                }

                ClusterChangedEvent.ClusterMember member = new ClusterChangedEvent.ClusterMember(
                    address:        address,
                    isConnected:    isConnected,
                    info:           info
                    );
                members.Add(member);
            }

            long eventTimestampId = _eventIdGenerator.Next();
            return new ClusterChangedEvent(members.ToArray(), eventTimestampId);
        }

        void PublishEvent(IClusterConnectionEvent message)
        {
            foreach (IActorRef subscriber in _eventSubscribers)
                subscriber.Tell(message);
        }

        #endregion

        #region Updating Current Node's Cluster Info

        class SetSelfClusterNodeInfoCommand
        {
            public readonly ClusterNodeInformation Info;
            public SetSelfClusterNodeInfoCommand(ClusterNodeInformation info)
            {
                Info = info;
            }
        }

        /// <summary>
        /// Updates this node's cluster node information. The information will be passed on all nodes on the cluster.
        /// If this node is part of the current cluster, <see cref="ClusterChangedEvent"/> will be emitted. If Info
        /// is equal to already set value, the call is ignored.
        /// </summary>
        public static void SetSelfClusterNodeInfo(ClusterNodeInformation info)
        {
            s_actor.Tell(new SetSelfClusterNodeInfoCommand(info));
        }

        void ReceiveSetSelfClusterNodeInfoCommand(SetSelfClusterNodeInfoCommand command)
        {
            InternalSetSelfClusterNodeInfo(command.Info);
        }

        #endregion

        #region Admin API

        class ClusterDebugStatusRequest
        {
            public static ClusterDebugStatusRequest Instance = new ClusterDebugStatusRequest();
        }
        public class ClusterDebugStatusResponse
        {
            public ClusterNodeAddress[] ConnectedNodes;
            public ClusterChangedEvent CurrentCluster;
        }

        public static async Task<ClusterDebugStatusResponse> GetClusterStatusAsync()
        {
            return await s_actor.Ask<ClusterDebugStatusResponse>(ClusterDebugStatusRequest.Instance, timeout: TimeSpan.FromSeconds(10));
        }

        void ReceiveClusterDebugStatusRequest(ClusterDebugStatusRequest _)
        {
            ClusterChangedEvent clusterStateMaybe = _currentCluster != null ? CreateCurrentClusterStateEvent() : null;
            Sender.Tell(new ClusterDebugStatusResponse()
            {
                ConnectedNodes = _connectedNodes.Keys.ToArray(),
                CurrentCluster = clusterStateMaybe
            });
        }

        #endregion
    }
}
