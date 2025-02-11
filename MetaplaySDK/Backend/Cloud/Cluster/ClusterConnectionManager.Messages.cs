// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;

namespace Metaplay.Cloud.Cluster;

partial class ClusterConnectionManager
{
    /// <summary>
    /// Marker for network messages. This is only used for type-safety to make sure only
    /// messages marked with this can be sent to sent from one ConnectionManager to other.
    /// </summary>
    interface IClusterConnectionNetworkMessage
    {
    }

    /// <summary>
    /// A single Handshake message delivers the node information from the sender node to the recipient, to
    /// which the Recipent replies with <see cref="ClusterHandshakeAck"/>. Handshaking is a two-way process:
    /// Hanshaking to be considered complete with a node when its handshake has been received (i.e. sent its
    /// node information) and it has Ack'd our handshake (i.e. it has received our node information).
    ///
    /// Note that in this case, the peer node might not have received the Ack from us, and hence may not consider
    /// the handshake to be complete yet. The peer node will continue sending handshake messages which we ack again.
    /// </summary>
    class ClusterHandshake : IClusterConnectionNetworkMessage
    {
        /// <summary>
        /// True if the sender is the Leader node.
        /// </summary>
        public bool                     IsLeader    { get; private set; }

        /// <summary>
        /// Cluster Cookie. The Cookie must match the <see cref="ClusteringOptions.Cookie"/> or the message is ignored.
        /// </summary>
        public string                   Cookie      { get; private set; }

        /// <summary>
        /// Sender's unique value to identify which Ack replies were for the handshakes it sent.
        /// The value is copied into reply's <see cref="ClusterHandshakeAck.ReplyToken"/>.
        /// </summary>
        public MetaGuid                 ReplyToken  { get; private set; }

        /// <summary>
        /// Sender node's current <see cref="ClusterNodeInformation"/>.
        /// </summary>
        public ClusterNodeInformation   Info        { get; private set; }

        /// <summary>
        /// Sender node's <see cref="CloudCoreVersion.BuildNumber"/>. If this does not match the recipent's value,
        /// a warning is printed.
        ///
        /// In practice, if this value does not match, the <see cref="Cookie"/> doesn't match either.
        /// </summary>
        public string                   BuildNumber { get; private set; }

        /// <summary>
        /// Sender node's <see cref="CloudCoreVersion.CommitId"/>. If this does not match the recipent's value,
        /// a warning is printed.
        ///
        /// In practice, if this value does not match, the <see cref="Cookie"/> doesn't match either.
        /// </summary>
        public string                   CommitId    { get; private set; }

        ClusterHandshake() { }
        public ClusterHandshake(bool isLeader, string cookie, MetaGuid replyToken, ClusterNodeInformation info, string buildNumber, string commitId)
        {
            IsLeader = isLeader;
            Cookie = cookie;
            ReplyToken = replyToken;
            Info = info;
            BuildNumber = buildNumber;
            CommitId = commitId;
        }
    }

    /// <summary>
    /// Notification that ClusterHandshake was accepted.
    /// </summary>
    class ClusterHandshakeAck  : IClusterConnectionNetworkMessage
    {
        /// <summary>
        /// Used to detect and ignore if Ack was not for this server instance. For example if this server restarted
        /// when the Ack was in flight.
        /// </summary>
        public MetaGuid                 ReplyToken  { get; private set; }

        ClusterHandshakeAck() { }
        public ClusterHandshakeAck(MetaGuid replyToken)
        {
            ReplyToken = replyToken;
        }
    }

    /// <summary>
    /// Sent by a node to all its all peers when its Info changes.
    /// </summary>
    class ClusterNodeInfoUpdate : IClusterConnectionNetworkMessage
    {
        public ClusterNodeInformation   Info        { get; private set; }

        public ClusterNodeInfoUpdate(ClusterNodeInformation info)
        {
            Info = info;
        }

    }

    /// <summary>
    /// Sent initially by leader to all nodes that completed handshake when cluster topology is changed.
    /// <para>
    /// The cluster change contains only the list of the nodes. The node information is updated directly
    /// one to one with <see cref="ClusterNodeInfoUpdate"/> messages.
    /// </para>
    /// <para>
    /// Upon receiving this message, nodes relay this message to all other connected nodes once.
    /// This is done to make sure all nodes observe the cluster topology change at relatively same
    /// time. In network the triangle inequality does not hold: a message from A -> B may take more
    /// time than message from A -> C -> B. Hence, the node C receiving this message cannot be sure
    /// node B is aware of the cluster change. If any message sent by C requires the correct cluster
    /// topology to be active, these may be rejected by B. To fix this, the node C relays the cluster
    /// change to B and other nodes. This ensures any message received send by C to B are observed after
    /// the cluster change.
    /// </para>
    /// </summary>
    class ClusterTopologyChanged : IClusterConnectionNetworkMessage
    {
        public ClusterNodeAddress[]     Nodes           { get; private set; }
        public long                     TimestampId     { get; private set; }

        public ClusterTopologyChanged(ClusterNodeAddress[] nodes, long timestampId)
        {
            Nodes = nodes;
            TimestampId = timestampId;
        }
    }
}
