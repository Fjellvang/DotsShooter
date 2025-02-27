using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Metaplay.Cloud.Persistence;

namespace Game.Server.Leaderboard;

[Table("Leaderboards")]
public class PersistedLeaderboardModel : IPersistedEntity
{
    [Key]
    [PartitionKey]
    [Required]
    [MaxLength(64)]
    [Column(TypeName = "varchar(64)")]
    public string EntityId { get; set; }

    [Required]
    [Column(TypeName = "DateTime")]
    public DateTime PersistedAt { get; set; }

    [Required]
    public byte[] Payload { get; set; }

    [Required]
    public int SchemaVersion { get; set; }

    [Required]
    public bool IsFinal { get; set; }
}