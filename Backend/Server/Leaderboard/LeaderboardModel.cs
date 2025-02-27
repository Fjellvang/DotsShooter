using System;
using System.Collections.Generic;
using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Server.Leaderboard;

[MetaSerializable]
[SupportedSchemaVersions(1,1)]
public class LeaderboardModel : ISchemaMigratable // For future schema migrations 
{
    [MetaMember(1)]
    public Dictionary<EntityId, Entry> Entries = new ();
    public record Entry(int Kills, int GoldCollected, int RoundsCompleted, DateTime RecordedAt);
}