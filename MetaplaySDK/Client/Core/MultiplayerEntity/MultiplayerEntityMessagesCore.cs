// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Client;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using System.Collections.Generic;

namespace Metaplay.Core.MultiplayerEntity.Messages
{
    //
    // This file contains Server-to-client and client-to-server messages that are part of Metaplay core.
    //

    /// <summary>
    /// Server generated data for the Entity Client passed in the the intialization handshake. Contains the
    /// information used to identify which entity client will take ownership of the new entity on client.
    /// <para>
    /// Practically, the subscribed Entity actor generates this when Session subscribes into it. Session then
    /// passes the context data to Client, where <c>IEntitySubClient</c> may process it. You may inherit this
    /// to send custom data from Entity's session start to your Entity Client on client.
    /// </para>
    /// </summary>
    [MetaSerializable]
    [MetaReservedMembers(100, 200)]
    public abstract class EntityClientData
    {
        [MetaSerializableDerived(101)]
        public sealed class Default : EntityClientData
        {
            Default() { }
            public Default(ClientSlot clientSlot) : base(clientSlot)
            {
            }
        }

        [MetaMember(101)] public ClientSlot ClientSlot { get; private set; }

        protected EntityClientData() { }
        protected EntityClientData(ClientSlot clientSlot)
        {
            ClientSlot = clientSlot;
        }
    }

    /// <summary>
    /// Serialized MultiplayerModel.
    /// </summary>
    [MetaSerializable]
    public struct EntitySerializedState
    {
        [MetaMember(1)] public MetaSerialized<IMultiplayerModel>                    PublicState                 { get; private set; }

        /// <summary>
        /// Serialized Member-specific additions to state, or Empty.
        /// </summary>
        [MetaMember(2)] public MetaSerialized<MultiplayerMemberPrivateStateBase>    MemberPrivateState          { get; private set; }

        /// <summary>
        /// Timeline position is (State.CurrentTick, CurrentOperation, 0)
        /// </summary>
        [MetaMember(3)] public int                                                  CurrentOperation            { get; private set; }
        [MetaMember(4)] public int                                                  LogicVersion                { get; private set; }
        [MetaMember(5)] public ContentHash                                          SharedGameConfigVersion     { get; private set; }
        [MetaMember(6)] public ContentHash                                          SharedConfigPatchesVersion  { get; private set; }
        [MetaMember(7)] public EntityActiveExperiment[]                             ActiveExperiments           { get; private set; }

        public EntitySerializedState(MetaSerialized<IMultiplayerModel> publicState, MetaSerialized<MultiplayerMemberPrivateStateBase> memberPrivateState, int currentOperation, int logicVersion, ContentHash sharedGameConfigVersion, ContentHash sharedConfigPatchesVersion, EntityActiveExperiment[] activeExperiments)
        {
            PublicState = publicState;
            MemberPrivateState = memberPrivateState;
            CurrentOperation = currentOperation;
            LogicVersion = logicVersion;
            SharedGameConfigVersion = sharedGameConfigVersion;
            SharedConfigPatchesVersion = sharedConfigPatchesVersion;
            ActiveExperiments = activeExperiments;
        }
    }

    [MetaSerializable]
    public struct EntityActiveExperiment
    {
        [MetaMember(1)] public PlayerExperimentId   ExperimentId;
        [MetaMember(2)] public ExperimentVariantId  VariantId;
        [MetaMember(3)] public string               ExperimentAnalyticsId;
        [MetaMember(4)] public string               VariantAnalyticsId;

        public EntityActiveExperiment(PlayerExperimentId experimentId, ExperimentVariantId variantId, string experimentAnalyticsId, string variantAnalyticsId)
        {
            ExperimentId = experimentId;
            VariantId = variantId;
            ExperimentAnalyticsId = experimentAnalyticsId;
            VariantAnalyticsId = variantAnalyticsId;
        }
    }

    /// <summary>
    /// Initialization data for setting up a multiplayer model on client side and the communication channel.
    /// </summary>
    [MetaSerializable]
    public class EntityInitialState
    {
        [MetaMember(1)] public EntitySerializedState        State               { get; private set; }
        [MetaMember(2)] public int                          ChannelId           { get; private set; }
        [MetaMember(3)] public EntityClientData             ClientData          { get; private set; }

        EntityInitialState() { }
        public EntityInitialState(EntitySerializedState state, int channelId, EntityClientData clientData)
        {
            State = state;
            ChannelId = channelId;
            ClientData = clientData;
        }
    }

    /// <summary>
    /// Request to execute certain actions on entity
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityEnqueueActionsRequest, MessageDirection.ClientToServer), MessageRoutingRuleEntityChannel]
    public class EntityEnqueueActionsRequest : MetaMessage
    {
        public List<ModelAction> Actions { get; private set; }

        EntityEnqueueActionsRequest() { }
        public EntityEnqueueActionsRequest(List<ModelAction> actions)
        {
            Actions = actions;
        }
    }

    /// <summary>
    /// Announcement from server that entity model has moved forward in time by executing the specified
    /// actions and ticks.
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityTimelineUpdateMessage, MessageDirection.ServerToClient, hasExplicitMembers: true)]
    public class EntityTimelineUpdateMessage : MetaMessage
    {
        [PrettyPrint(PrettyPrintFlag.SizeOnly)]
        [MetaMember(1)] public List<ModelAction>    Operations      { get; private set; } // if null, then Tick
        [MetaMember(2)] public uint                 FinalChecksum   { get; private set; }
        [MetaMember(5)] public uint[]               DebugChecksums  { get; private set; } // Null if per-operation debugging is not enabled

        EntityTimelineUpdateMessage() { }
        public EntityTimelineUpdateMessage(List<ModelAction> operations, uint finalChecksum, uint[] debugChecksums)
        {
            Operations = operations;
            FinalChecksum = finalChecksum;
            DebugChecksums = debugChecksums;
        }
    }

    /// <summary>
    /// Client report of a checksum mismatch.
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityChecksumMismatchDetails, MessageDirection.ClientToServer, hasExplicitMembers: true), MessageRoutingRuleEntityChannel]
    public class EntityChecksumMismatchDetails : MetaMessage
    {
        [MetaMember(1)] public byte[]           ChecksumBuffer;
        [MetaMember(2)] public long             Tick;
        [MetaMember(3)] public int              Operation;

        EntityChecksumMismatchDetails() { }
        public EntityChecksumMismatchDetails(byte[] checksumBuffer, long tick, int operation)
        {
            ChecksumBuffer = checksumBuffer;
            Tick = tick;
            Operation = operation;
        }
    }

    /// <summary>
    /// Request from client for server to reply with a set of Markers as this message goes through the system.
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityTimelinePingTraceQuery, MessageDirection.ClientToServer), MessageRoutingRuleEntityChannel]
    public class EntityTimelinePingTraceQuery : MetaMessage
    {
        public uint Id { get; private set; }

        EntityTimelinePingTraceQuery() { }
        public EntityTimelinePingTraceQuery(uint id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// Server to client trace marker response to EntityTimelinePingTraceQuery.
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityTimelinePingTraceMarker, MessageDirection.ServerToClient)]
    public class EntityTimelinePingTraceMarker : MetaMessage
    {
        [MetaSerializable]
        public enum TracePosition
        {
            /// <summary>
            /// Reply marker sent when the message was processed on destination entity.
            /// </summary>
            MessageReceivedOnEntity = 0,

            /// <summary>
            /// Reply marker sent immediately after the tick following the message was executed on entity.
            /// </summary>
            AfterNextTick = 1,
        }

        public uint Id { get; private set; }
        public TracePosition Position { get; private set; }

        EntityTimelinePingTraceMarker() { }
        public EntityTimelinePingTraceMarker(uint id, TracePosition position)
        {
            Id = id;
            Position = position;
        }
    }

    /// <summary>
    /// Envelope for delivering a message from client entity to the server entity.
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityClientToServerEnvelope, MessageDirection.ClientToServer), MessageRoutingRuleSession]
    public class EntityClientToServerEnvelope : MetaMessage
    {
        public int ChannelId { get; private set; }
        public MetaSerialized<MetaMessage> Message { get; private set; }

        EntityClientToServerEnvelope() { }
        public EntityClientToServerEnvelope(int channelId, MetaSerialized<MetaMessage> message)
        {
            ChannelId = channelId;
            Message = message;
        }
    }

    /// <summary>
    /// Envelope for delivering a message from server entity to the client entity.
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityServerToClientEnvelope, MessageDirection.ServerToClient)]
    public class EntityServerToClientEnvelope : MetaMessage
    {
        public int ChannelId { get; private set; }
        public MetaSerialized<MetaMessage> Message { get; private set; }

        EntityServerToClientEnvelope() { }
        public EntityServerToClientEnvelope(int channelId, MetaSerialized<MetaMessage> message)
        {
            ChannelId = channelId;
            Message = message;
        }
    }

    /// <summary>
    /// Notification from server to client that player's current entity has changed. This means both
    /// changes from no-entity to some entity and vice versa, but also changes from entity A to entity B.
    /// This changes the channel.
    /// </summary>
    [MetaMessage(MessageCodesCore.EntitySwitchedMessage, MessageDirection.ServerToClient)]
    public class EntitySwitchedMessage : MetaMessage
    {
        /// <summary>
        /// The ID of the channel on which the entity is removed when it changes to the the new channel.
        /// -1 if there was no precursor entity.
        /// </summary>
        public int OldChannelId { get; private set; }

        /// <summary>
        /// <c>null</c> if switching to no entity.
        /// </summary>
        public EntityInitialState NewState { get; private set; }

        EntitySwitchedMessage() { }
        public EntitySwitchedMessage(int oldChannelId, EntityInitialState newState)
        {
            OldChannelId = oldChannelId;
            NewState = newState;
        }
    }

    /// <summary>
    /// Notification that the client has activated an entity on a channel. This is used to determine which
    /// client->server messages were intended to which "current" entity if those have just changed.
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityActivated, MessageDirection.ClientToServer), MessageRoutingRuleSession]
    public class EntityActivated : MetaMessage
    {
        public int ChannelId  { get; private set; }

        EntityActivated () { }
        public EntityActivated (int channelId)
        {
            ChannelId = channelId;
        }
    }
}
