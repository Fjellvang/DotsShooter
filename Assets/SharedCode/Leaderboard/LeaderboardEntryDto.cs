using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic.Leaderboard;

[MetaSerializable]
public class LeaderboardEntryDto
{
    [MetaMember(1)] public string PlayerId { get; set; }
    [MetaMember(2)] public int Kills { get; set; }
    [MetaMember(3)] public int GoldCollected { get; set; }
    [MetaMember(4)] public int RoundsCompleted { get; set; }
    [MetaMember(5)] public MetaTime RecordedAt { get; set; }
    
    // Helper property (not serialized)
    public EntityId PlayerEntityId => EntityId.ParseFromString(PlayerId);
}