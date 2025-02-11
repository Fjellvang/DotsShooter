// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;

namespace Metaplay.Core.Model
{
    /// <summary>
    /// Attribute for ModelActions that can only be executed in development
    /// environments, or by developer players in production. This is verified by
    /// the server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DevelopmentOnlyActionAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute given to all ModelAction classes. Provides the unique <c>TypeCode</c> (used by
    /// serializer to distinguish between different Actions when deserializing).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ModelActionAttribute : Attribute, ISerializableTypeCodeProvider, ISerializableFlagsProvider
    {
        public int                   TypeCode   { get; }
        public MetaSerializableFlags ExtraFlags => MetaSerializableFlags.ImplicitMembers;

        public ModelActionAttribute(int typeCode)
        {
            TypeCode    = typeCode;
        }
    }

    /// <summary>
    /// The modes in which a ModelAction may be executed.
    /// </summary>
    [Flags]
    public enum ModelActionExecuteFlags
    {
        /// <summary>
        /// No allowed issuers.
        /// </summary>
        None = 0,

        /// <summary>
        /// Allow timeline leader issue this action in a synchronous manner. This is the normal action execution mode.
        /// <para>
        /// A synchronous execution means that the action has a fixed position on the timeline that both the leader and the follower agree
        /// on. As leader controls the timeline, the action is simply executed immediately and appended into the timeline.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Examples: <br/>
        /// For PlayerModel, this is the normal client-issued action. <br/>
        /// For GuildModel, this is the normal server-issued action. <br/>
        /// </remarks>
        LeaderSynchronized = 1 << 0,

        ///// <summary>
        ///// Allow timeline leader issue this action in an unsynchronous manner.
        ///// <para>
        ///// An unsynchronous execution means that the both leader (client) and the follower (server) may not execute this action on the same
        ///// position on the timeline. This requires that the action does not change any Checksummed field.
        ///// </para>
        ///// </summary>
        //LeaderUnsynchronized = 1 << 1,

        /// <summary>
        /// Allow timeline follower issue this action in a synchronous manner.
        /// <para>
        /// A synchronous execution means that the action has a fixed position on the timeline both the leader and the follower agree
        /// on. As the follower does not control the timeline, the action is instead enqueued for the leader to be executed. Hence the
        /// issuer (the follower) only see the action results in a delayed manner.
        /// </para>
        /// <para>
        /// Note: Server is always the ultimate authority of the state and may execute any action regardless of the of the flags.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Examples: <br/>
        /// For PlayerModel, this is an server-issued enqueued action. Server sees the results in a delayed manner.<br/>
        /// For GuildModel, this is the client-issued normal action. Client enqueues this for server execution, and sees results in a delayed manner.<br/>
        /// </remarks>
        FollowerSynchronized = 1 << 2,

        /// <summary>
        /// Allow timeline follower issue this action in an unsynchronous manner.
        /// <para>
        /// An unsynchronous execution means that the both leader (client) and the follower (server) may not execute this action on the same
        /// position on the timeline. This requires that the action does not change any Checksummed field. Follower executes this action
        /// immediately, and hence observes the actions results immediately. Leader will then execute the action at a later time.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Examples: <br/>
        /// For PlayerModel, this is an server-issued unsynchronized action.<br/>
        /// </remarks>
        FollowerUnsynchronized = 1 << 3,
    }

    /// <summary>
    /// Attribute that may be given to abstract ModelAction classes to set the <see cref="ModelActionExecuteFlags"/>
    /// of the action.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ModelActionExecuteFlagsAttribute : Attribute
    {
        public ModelActionExecuteFlags Modes { get; private set; }

        public ModelActionExecuteFlagsAttribute(ModelActionExecuteFlags modes)
        {
            Modes = modes;
        }
    }
}
