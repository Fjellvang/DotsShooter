using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;

namespace Game.Server.Leaderboard;

[Table("LeaderboardEntries")]
public class LeaderboardEntry : IPersistedItem
{
    public LeaderboardEntry()
    {
    }

    public LeaderboardEntry(EntityId playerId, int kills, int goldCollected, DateTime recordedAt)
    {
        PlayerId = playerId.ToString();
        Kills = kills;
        GoldCollected = goldCollected;
        RecordedAt = recordedAt;
    }
    [Key]
    [PartitionKey]
    [Required]
    public int Id { get; set; }
    
    [Required]
    [Column(TypeName = "varchar(64)")]
    public string PlayerId { get; set; }
    // Helper accessor for parsing the PlayerId back to an EntityId
    public EntityId PlayerEntityId => EntityId.ParseFromString(PlayerId);
    
    [Required]
    public int Kills { get; set; }
    [Required]
    public int GoldCollected { get; set; }
    [Required]
    public int RoundsCompleted { get; set; }
    [Required]
    public DateTime RecordedAt { get; set; }
}