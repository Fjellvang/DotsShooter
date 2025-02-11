// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Core.Player;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Metaplay.Server.PlayerDeletion
{
    public class PlayerDeletionRecords
    {
        /// <summary>
        /// Database-persisted player-deletion entry.
        /// </summary>
        [Table("PlayerDeletionRecords")]
        [NonPartitioned]
        public class PersistedPlayerDeletionRecord : IPersistedItem
        {
            [Key]
            [Required]
            [MaxLength(64)]
            [Column(TypeName = "varchar(64)")]
            public string PlayerId { get; set; }

            [Required]
            [Column(TypeName = "DateTime")]
            public DateTime ScheduledDeletionAt { get; set; }

            [Column(TypeName = "varchar(128)")]
            public string DeletionSource { get; set; }

            public PersistedPlayerDeletionRecord() { }
            public PersistedPlayerDeletionRecord(EntityId playerId, MetaTime scheduledDeletionAt, PlayerDeletionSource source, string sourceInfo)
            {
                PlayerId            = playerId.ToString();
                ScheduledDeletionAt = scheduledDeletionAt.ToDateTime();
                DeletionSource      = PackSourceInfo(source, sourceInfo);
            }

            // Custom packing of source info, for backwards compatibility
            public static string PackSourceInfo(PlayerDeletionSource source, string sourceInfo)
            {
                string ret = source.ToString();
                if (sourceInfo != null)
                    ret += " " + sourceInfo;
                return ret;
            }

            public static (PlayerDeletionSource, string) UnpackSourceInfo(string deletionSource)
            {
                // Legacy data
                if (deletionSource == "Player")
                    return (PlayerDeletionSource.User, null);

                // Note: legacy entries may contain "Delete API" and "Redelete API" string in deletionSource. These
                // legacy values simply get mapped to the default "PlayerDeletionSource.Admin" value here.
                int    sepIndex   = deletionSource.IndexOf(' ');
                string sourceStr  = (sepIndex > 0) ? deletionSource.Substring(0, sepIndex) : deletionSource;
                string infoStr    = (sepIndex > 0) ? deletionSource.Substring(sepIndex + 1) : null;

                if (Enum.TryParse(sourceStr, out PlayerDeletionSource source))
                    return (source, infoStr);

                // Legacy data
                return (PlayerDeletionSource.Admin, null);
            }
        }
    }
}
