// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using System;

namespace Metaplay.Core
{
    /// <summary>
    /// Scope where a MetaMessage may be used.
    /// </summary>
    public enum MessageDirection
    {
        /// <summary>
        /// Both client and server are allowed to send these message to each other.
        /// Part of the public protocol (contributes to the MetaSerializerTypeRegistry.FullProtocolHash).
        /// </summary>
        Bidirectional,

        /// <summary>
        /// Client is allowed to send this to server. Server may not send this to client.
        /// Part of the public protocol (contributes to the MetaSerializerTypeRegistry.FullProtocolHash).
        /// </summary>
        ClientToServer,

        /// <summary>
        /// Server is allowed to send this to client. Client may not send this to server.
        /// Part of the public protocol (contributes to the MetaSerializerTypeRegistry.FullProtocolHash).
        /// </summary>
        ServerToClient,

        /// <summary>
        /// Server internal message. This message cannot be sent over network. If this is referred to by a public (non-internal) message or model, an error is reported latest at application boot time.
        /// </summary>
        ServerInternal,

        /// <summary>
        /// Client internal message. This message cannot be sent over network. If this is referred to by a public (non-internal) message or model, an error is reported latest at application boot time.
        /// </summary>
        ClientInternal,
    }

    /// <summary>
    /// Declares MetaMessage metadata for types inheriting MetaMessage.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MetaMessageAttribute : Attribute, ISerializableTypeCodeProvider, ISerializableFlagsProvider
    {
        public readonly int              MessageTypeCode;
        public readonly MessageDirection Direction;

        // Extra information for serializer: integer typeCode of message, and set implicit members
        public int                   TypeCode   => MessageTypeCode;
        public MetaSerializableFlags ExtraFlags { get; } = MetaSerializableFlags.ImplicitMembers;

        /// <param name="typeCode">The unique type code for this message. Codes are simple integers and have no inherent semantics or structure. If two types have the same typecode, a failure is reported latest at application boot time.</param>
        /// <param name="direction">Denotes the scope of the message. See values of <see cref="MessageDirection"/> for more details.</param>
        /// <param name="hasExplicitMembers">
        /// If <c>false</c>, members do not need [MetaMember(..)] annotation and member ID codes are assigned sequentially. [MetaMember(..)] annotations are allowed as long as they agree with the automatic assignment.
        /// <br/>
        /// If <c>true</c>, only members with a [MetaMember(..)] annotation are serialized. Members without a [MetaMember(..)] annotation are ignored during serialization and deserialization.
        /// </param>
        public MetaMessageAttribute(int typeCode, MessageDirection direction, bool hasExplicitMembers = false)
        {
            MessageTypeCode = typeCode;
            Direction       = direction;

            if (hasExplicitMembers)
                ExtraFlags &= ~MetaSerializableFlags.ImplicitMembers;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public abstract class MessageRoutingRule : Attribute { }

    /// <summary> Message is a part of the core connection or session protocol. </summary>
    public class MessageRoutingRuleProtocol     : MessageRoutingRule{ }
    /// <summary> Message is handled by SessionActor (or SessionActorBase). </summary>
    public class MessageRoutingRuleSession      : MessageRoutingRule{ public static MessageRoutingRuleSession Instance = new MessageRoutingRuleSession(); }
    /// <summary> Message is routed to the owned player. </summary>
    public class MessageRoutingRuleOwnedPlayer  : MessageRoutingRule{ }
    #if !METAPLAY_DISABLE_GUILDS
    /// <summary> Message is routed to the current guild. </summary>
    public class MessageRoutingRuleCurrentGuild : MessageRoutingRule{ }
    #endif
    /// <summary> Message is routed to the other peer of the entity channel. This may only be sent from a context where entity channel is established, e.g MultiplayerEntityClientContext, and MultiplayerEntityActorBase. </summary>
    public class MessageRoutingRuleEntityChannel  : MessageRoutingRule{ }

    [AttributeUsage(AttributeTargets.Class)]
    public class RequestResponseAttribute : Attribute, ISerializableFlagsProvider
    {
        public MetaSerializableFlags ExtraFlags => MetaSerializableFlags.ImplicitMembers;
    }
}
