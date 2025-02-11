// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if !METAPLAY_DISABLE_GUILDS

namespace Metaplay.Core.Guild
{
    /// <summary>
    /// Server-side guild event listener interface, used by Metaplay core callbacks.
    /// Game-specific callbacks should go to game-specific <c>IGuildModelServerListener</c>.
    /// </summary>
    public interface IGuildModelServerListenerCore
    {
        // \note IMPORTANT: If you change the parameters of an existing method here in this interface,
        //       you should leave the overload with the old parameters as [Obsolete("...", error: true)].
        //       This way, uses of the old method will get notified by the compiler, instead of the change
        //       going unnoticed due to the default implementations here.

        /// <summary>
        /// Data returned by GuildActor.CreateGuildDiscoveryInfo has changed and should
        /// be updated. This allows guild recommender and search results to be updated
        /// quickly. If this listener is not called, the discovery data is updated only
        /// eventually.
        /// </summary>
        void GuildDiscoveryInfoChanged() { }

        /// <summary>
        /// Called after Member was removed from the Members with the intention of
        /// kicking the player. This call delivers the kick reason to the player and
        /// informs the kicked player's session (if any) that the guild has kicked it.
        /// </summary>
        void PlayerKicked(EntityId kickedPlayerId, GuildMemberBase kickedMember, EntityId kickingPlayerId, IGuildMemberKickReason kickReasonOrNull) { }

        /// <summary>
        /// Called after the player data of a guild member has been updated by <see cref="GuildMemberPlayerDataBase.ApplyOnMember"/>.
        /// This is also called when a new member joins the guild.
        /// </summary>
        void MemberPlayerDataUpdated(EntityId memberId) { }

        /// <summary>
        /// Called after the member's role has changed.
        /// </summary>
        void MemberRoleChanged(EntityId memberId) { }

        /// <summary>
        /// Called after the member leaves or is removed from the guild.
        /// </summary>
        void MemberRemoved(EntityId memberId) { }
    }

    /// <summary>
    /// Empty implementation of <see cref="IGuildModelServerListenerCore"/>.
    /// </summary>
    public class EmptyGuildModelServerListenerCore : IGuildModelServerListenerCore
    {
        public static readonly EmptyGuildModelServerListenerCore Instance = new EmptyGuildModelServerListenerCore();
    }

    /// <summary>
    /// Client-side guild event listener interface, used by Metaplay core callbacks.
    /// Game-specific callbacks should go to game-specific <c>IGuildModelClientListener</c>.
    /// </summary>
    public interface IGuildModelClientListenerCore
    {
        // \note IMPORTANT: If you change the parameters of an existing method here in this interface,
        //       you should leave the overload with the old parameters as [Obsolete("...", error: true)].
        //       This way, uses of the old method will get notified by the compiler, instead of the change
        //       going unnoticed due to the default implementations here.

        /// <summary>
        /// <inheritdoc cref="IGuildModelServerListenerCore.MemberPlayerDataUpdated(EntityId)"/>
        /// </summary>
        void MemberPlayerDataUpdated(EntityId memberId) { }

        /// <inheritdoc cref="IGuildModelServerListenerCore.MemberRoleChanged(EntityId)"/>
        void MemberRoleChanged(EntityId memberId) { }

        void GuildNameAndDescriptionUpdated() { }
    }

    /// <summary>
    /// Empty implementation of <see cref="IGuildModelClientListenerCore"/>.
    /// </summary>
    public class EmptyGuildModelClientListenerCore : IGuildModelClientListenerCore
    {
        public static readonly EmptyGuildModelClientListenerCore Instance = new EmptyGuildModelClientListenerCore();
    }
}

#endif
