// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;

namespace Metaplay.Core.Player
{
    [MetaSerializable]
    public enum PlayerDeletionSource
    {
        Admin = 0,
        User = 1,
        System = 2,
        Unknown = 3,
    }

    /// <summary>
    ///  Deletion status of a player. Possible transitions are:
    ///     None -> ScheduledBy* = Player is scheduled for deletion
    ///     ScheduledBy* -> None = Admin or Player or System cancels scheduled deletion
    ///     ScheduledBy* -> DeletedBy* = Player is deleted by scheduled deletion
    /// <p>
    /// Bit layout is as follows:
    /// - Bits 0-1: Deletion status. Possible values are:
    ///   0: Not deleted, normal player
    ///   1: Deletion scheduled
    ///   2: Deleted
    ///   3: Unused, reserved for future
    /// - When status is "Deletion scheduled"
    ///   - Bits 2-7: Value of PlayerDeletionSource (source of deletion scheduling)
    /// - When status is "Deleted"
    ///   - Bit 2: When set, means that player deletion source is known (always set for new entries,
    ///            unset for legacy entries that did not store this information).
    ///   - Bits 3-7: (Only when Bit 2 is set) Value of PlayerDeletionSource (source of deletion).
    /// </p>
    /// </summary>
    [MetaSerializable]
    public enum PlayerDeletionStatus
    {
        /// <summary>
        /// Normal player status
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Player has been deleted, source of deletion not known (was deleted before source information was maintained).
        /// </summary>
        DeletedByUnknownLegacy = 0x02,

        /// <summary>
        /// Player is scheduled to be deleted by Game Admin via Dashboard
        /// </summary>
        ScheduledByAdmin = (PlayerDeletionSource.Admin << 2) | 0x01,

        /// <summary>
        /// Player has been deleted by Game Admin via Dashboard
        /// </summary>
        DeletedByAdmin = (PlayerDeletionSource.Admin << 3) | 0x06,

        /// <summary>
        /// Player is scheduled to be deleted by user itself
        /// </summary>
        ScheduledByUser = (PlayerDeletionSource.User << 2) | 0x01,

        /// <summary>
        /// Player has been deleted by user itself
        /// </summary>
        DeletedByUser = (PlayerDeletionSource.User << 3) | 0x06,

        /// <summary>
        /// Player is scheduled to be deleted by system (i.e. server logic)
        /// </summary>
        ScheduledBySystem = (PlayerDeletionSource.System << 2) | 0x01,

        /// <summary>
        /// Player has been deleted by system (i.e. server logic)
        /// </summary>
        DeletedBySystem = (PlayerDeletionSource.System << 3) | 0x06,

        /// <summary>
        /// Player has been deleted by an unknown source. Should never be used.
        /// </summary>
        ScheduledByUnknown = (PlayerDeletionSource.Unknown << 3) | 0x01,

        /// <summary>
        /// Player has been deleted by an unknown source. Should never be used.
        /// </summary>
        DeletedByUnknown = (PlayerDeletionSource.Unknown << 3) | 0x06,
    }

    public static class PlayerDeletionStatusExtensions
    {
        /// <summary>
        /// Is enum any ScheduledBy* state.
        /// </summary>
        public static bool IsScheduled(this PlayerDeletionStatus status)
        {
            return ((uint)status & 0x03) == 1;
        }

        /// <summary>
        /// Is enum any DeletedBy* state.
        /// </summary>
        public static bool IsDeleted(this PlayerDeletionStatus status)
        {
            return ((uint)status & 0x03) == 2;
        }

        public static PlayerDeletionSource Source(this PlayerDeletionStatus status)
        {
            // Special handling for legacy value
            if (status == PlayerDeletionStatus.DeletedByUnknownLegacy)
                return PlayerDeletionSource.Unknown;
            int shift = status.IsScheduled() ? 2 : 3;
            return (PlayerDeletionSource)((uint)status >> shift);
        }

        /// Generate DeletedBy* PlayerDeletionStatus value with given source
        public static PlayerDeletionStatus ToDeletedStatus(this PlayerDeletionSource source)
        {
            return (PlayerDeletionStatus)(((uint)source << 3) | 0x06);
        }

        /// Generate ScheduledBy* PlayerDeletionStatus value with given source
        public static PlayerDeletionStatus ToScheduledStatus(this PlayerDeletionSource source)
        {
            return (PlayerDeletionStatus)(((uint)source << 2) | 0x01);
        }
    }
}
