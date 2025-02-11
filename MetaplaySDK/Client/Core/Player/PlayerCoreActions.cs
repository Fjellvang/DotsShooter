// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.InGameMail;
using Metaplay.Core.Localization;
using Metaplay.Core.Model;
using Metaplay.Core.Rewards;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using System;
using System.Runtime.Serialization;
using static System.FormattableString;

namespace Metaplay.Core.Player
{
    /// <summary>
    /// Append a piece of <see cref="MetaInGameMail"/> to the player's <see cref="IPlayerModelBase.MailInbox"/>.
    /// </summary>
    [ModelAction(ActionCodesCore.PlayerAddMail)]
    public class PlayerAddMail : PlayerSynchronizedServerActionCore<IPlayerModelBase>
    {
        public MetaInGameMail MailContents { get; private set; }
        public MetaTime       SentAt       { get; private set; }

        public PlayerAddMail() { }
        public PlayerAddMail(MetaInGameMail mail, MetaTime sentAt) { MailContents = mail; SentAt = sentAt; }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (MailContents == null)
                return MetaActionResult.InvalidMail;
            if (!MailContents.Id.IsValid)
                return MetaActionResult.InvalidMailId;
            if (player.MailInbox.Find(mail => mail.Contents.Id == MailContents.Id) != null)
                return MetaActionResult.DuplicateMailId;

            if (commit)
            {
                // Append to inbox
                PlayerMailItem mail = IntegrationRegistry.Get<InGameMailIntegration>().MakePlayerMailItem(MailContents, SentAt);

                player.MailInbox.Add(mail);
                mail.OnAddedToInbox(player);

                // Inform client
                player.ClientListenerCore.OnNewInGameMail(mail);
            }

            return MetaActionResult.Success;
        }
    }

    [ModelAction(ActionCodesCore.PlayerDebugAddMailToSelf)]
    [DevelopmentOnlyAction]
    public class PlayerDebugAddMailToSelf : PlayerActionCore<IPlayerModelBase>
    {
        public MetaInGameMail MailContents { get; private set; }

        public PlayerDebugAddMailToSelf() { }
        public PlayerDebugAddMailToSelf(MetaInGameMail mail) { MailContents = mail; }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            return new PlayerAddMail(MailContents, player.CurrentTime).Execute(player, commit);
        }
    }

    /// <summary>
    /// Consume a given piece of <see cref="MetaInGameMail"/> in <see cref="IPlayerModelBase.MailInbox"/>
    /// with id <see cref="MailId"/>. The effect (if any) of the given mail is also applied to the player's
    /// state. For example, any gems received alongside the mail are added to player's inventory.
    /// </summary>
    [ModelAction(ActionCodesCore.PlayerConsumeMail)]
    public class PlayerConsumeMail : PlayerActionCore<IPlayerModelBase>
    {
        public MetaGuid MailId { get; private set; }

        public PlayerConsumeMail() { }
        public PlayerConsumeMail(MetaGuid id) { MailId = id; }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            PlayerMailItem mail = player.MailInbox.Find(mail => mail.Contents.Id == MailId);
            if (mail == null)
                return MetaActionResult.InvalidMailId;
            if (mail.HasBeenConsumed)
                return MetaActionResult.MailAlreadyConsumed;

            if (commit)
            {
                player.Log.Info("Consuming mail: {Mail}", PrettyPrint.Compact(mail.Contents));
                mail.ConsumeRewards(player);
            }

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Toggle the `IsRead` status of a mail item. Stores the timestamp of the first time
    /// the `IsRead` status is being set to `true`.
    /// </summary>
    [ModelAction(ActionCodesCore.PlayerToggleMailIsRead)]
    public class PlayerToggleMailIsRead : PlayerActionCore<IPlayerModelBase>
    {
        public MetaGuid MailId { get; private set; }
        public bool IsRead { get; private set; }

        public PlayerToggleMailIsRead() { }
        public PlayerToggleMailIsRead(MetaGuid id, bool isRead) { MailId = id; IsRead = isRead; }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            PlayerMailItem mail = player.MailInbox.Find(mail => mail.Contents.Id == MailId);
            if (mail == null)
                return MetaActionResult.InvalidMailId;
            if (mail.IsRead == IsRead)
                return MetaActionResult.Success;

            if (commit)
            {
                mail.ToggleIsRead(player, IsRead);
            }

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Client action for deleting a <see cref="MetaInGameMail"/> in <see cref="IPlayerModelBase.MailInbox"/>
    /// with id <see cref="MailId"/>. Will fail with <see cref="MetaActionResult.MailNotConsumed"/> if mail item
    /// needs consuming and hasn't been consumed yet.
    /// </summary>
    [ModelAction(ActionCodesCore.PlayerDeleteMail)]
    [MetaImplicitMembersRange(110, 120)]
    public sealed class PlayerDeleteMail : PlayerActionCore<IPlayerModelBase>
    {
        public MetaGuid MailId { get; private set; }

        PlayerDeleteMail() { }
        public PlayerDeleteMail(MetaGuid id)
        {
            MailId = id;
        }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            PlayerMailItem mail = player.MailInbox.Find(needle => needle.Contents.Id == MailId);
            if (mail == null)
                return MetaActionResult.InvalidMailId;
            if (mail.Contents.MustBeConsumed && !mail.HasBeenConsumed)
                return MetaActionResult.MailNotConsumed;

            if (commit)
            {
                player.MailInbox.RemoveAll(needle => needle.Contents.Id == MailId);
                mail.OnDeletedFromInbox(player);
            }

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Synchronized server action for deleting a <see cref="MetaInGameMail"/> in <see cref="IPlayerModelBase.MailInbox"/>
    /// with id <see cref="MailId"/>. Mail item will be deleted regardless of whether is has been consumed or not.
    /// </summary>
    [ModelAction(ActionCodesCore.PlayerForceDeleteMail)]
    [MetaImplicitMembersRange(110, 120)]
    public sealed class PlayerForceDeleteMail : PlayerSynchronizedServerActionCore<IPlayerModelBase>
    {
        public MetaGuid MailId { get; private set; }

        PlayerForceDeleteMail() { }
        public PlayerForceDeleteMail(MetaGuid id)
        {
            MailId = id;
        }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            PlayerMailItem mail = player.MailInbox.Find(needle => needle.Contents.Id == MailId);
            if (mail == null)
                return MetaActionResult.InvalidMailId;
            if (commit)
            {
                player.MailInbox.RemoveAll(needle => needle.Contents.Id == MailId);
                mail.OnDeletedFromInbox(player);
            }
            return MetaActionResult.Success;
        }
    }

    [ModelAction(ActionCodesCore.PlayerSetIsOnline)]
    public class PlayerSetIsOnline : PlayerUnsynchronizedServerActionCore<IPlayerModelBase>
    {
        public bool IsOnline { get; private set; }

        public PlayerSetIsOnline() { }
        public PlayerSetIsOnline(bool isOnline) { IsOnline = isOnline; }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (commit)
            {
                player.IsOnline = IsOnline;
            }

            return MetaActionResult.Success;
        }
    }

    [ModelAction(ActionCodesCore.PlayerChangeLanguage)]
    public class PlayerChangeLanguage : PlayerActionCore<IPlayerModelBase>
    {
        public LanguageInfo Language            { get; private set; }
        public ContentHash  LocalizationVersion { get; private set; }

        PlayerChangeLanguage() { }
        public PlayerChangeLanguage(LanguageInfo language, ContentHash localizationVersion)
        {
            Language = language;
            LocalizationVersion = localizationVersion;
        }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (Language == null)
                return MetaActionResult.InvalidLanguage;

            if (commit)
            {
                player.Language = Language.LanguageId;
                player.LanguageSelectionSource = LanguageSelectionSource.UserSelected;

                player.ServerListenerCore.LanguageChanged(Language, LocalizationVersion);
            }

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Ban or un-ban the player.
    /// A banned player won't be able to start a game session.
    /// </summary>
    [ModelAction(ActionCodesCore.PlayerSetIsBanned)]
    public class PlayerSetIsBanned : PlayerUnsynchronizedServerActionCore<IPlayerModelBase>
    {
        public bool IsBanned { get; private set; }

        public PlayerSetIsBanned() { }
        public PlayerSetIsBanned(bool isBanned) { IsBanned = isBanned; }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (commit)
            {
                // Update state
                player.IsBanned = IsBanned;

                // Callback
                player.ServerListenerCore.OnPlayerBannedStatusChanged(IsBanned);
            }

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Change the player's name. Validation will have already taken place before
    /// this action is posted, so the the Name file can be trusted
    /// </summary>
    [ModelAction(ActionCodesCore.PlayerChangeName)]
    public class PlayerChangeName : PlayerUnsynchronizedServerActionCore<IPlayerModelBase>
    {
        public string NewName { get; private set; }

        public PlayerChangeName() { }
        public PlayerChangeName(string newName)
        {
            NewName = newName;
        }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (commit)
            {
                // Update name
                player.PlayerName = NewName;

                // Inform listeners
                player.ServerListenerCore.OnPlayerNameChanged(NewName);
                player.ClientListenerCore.OnPlayerNameChanged(NewName);
                #if !METAPLAY_DISABLE_GUILDS
                player.ServerListenerCore.GuildMemberPlayerDataChanged();
                #endif
            }

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Schedule a player for deletion (by admin or user)
    /// </summary>
    [ModelAction(ActionCodesCore.PlayerSetIsScheduledForDeletionAt)]
    public class PlayerSetIsScheduledForDeletionAt : PlayerUnsynchronizedServerActionCore<IPlayerModelBase>
    {
        public MetaTime             ScheduledForDeletionAt { get; private set; }
        public PlayerDeletionSource ScheduledForDeletionBy { get; private set; }

        PlayerSetIsScheduledForDeletionAt() { }
        public PlayerSetIsScheduledForDeletionAt(MetaTime scheduledForDeletionAt, PlayerDeletionSource scheduledBy)
        {
            ScheduledForDeletionAt = scheduledForDeletionAt;
            ScheduledForDeletionBy = scheduledBy;
        }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (commit)
            {
                // Transition from:
                //  * None if scheduling for deletion and not already scheduled
                //  * ScheduledBy* if updating time
                player.DeletionStatus = ScheduledForDeletionBy.ToScheduledStatus();
                player.ScheduledForDeletionAt = ScheduledForDeletionAt;
                player.ClientListenerCore.OnPlayerScheduledForDeletionChanged();
            }

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Unschedule a player for deletion (by admin or user)
    /// </summary>
    [ModelAction(ActionCodesCore.PlayerSetUnscheduledForDeletion)]
    public class PlayerSetUnscheduledForDeletion : PlayerUnsynchronizedServerActionCore<IPlayerModelBase>
    {
        public PlayerSetUnscheduledForDeletion() { }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (commit)
            {
                player.DeletionStatus = PlayerDeletionStatus.None;
                player.ClientListenerCore.OnPlayerScheduledForDeletionChanged();
            }

            return MetaActionResult.Success;
        }
    }
}
