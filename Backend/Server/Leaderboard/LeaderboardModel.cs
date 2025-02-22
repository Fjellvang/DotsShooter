using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Server.Leaderboard;

[MetaSerializable]
[SupportedSchemaVersions(1,1)]
public class LeaderboardModel : ISchemaMigratable
{
    // [MetaMember(1)]
    // public List<LeaderboardEntry> Entries = new List<LeaderboardEntry>();
}

[Table("LeaderboardEntries")]
public class PersistedLeaderboardModel : IPersistedItem
{
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
}