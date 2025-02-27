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
    [MetaSerializable]
    public class Entry
    {
        [MetaMember(1)]
        public int Kills { get; set; } 
        [MetaMember(2)]
        public int GoldCollected { get; set; }
        [MetaMember(3)]
        public int RoundsCompleted { get; set; } 
        [MetaMember(4)]
        public DateTimeOffset RecordedAt { get; set; }
    }
}