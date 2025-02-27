using System;
using Game.Logic.TypeCodes;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;

namespace Game.Server.Leaderboard;

[EntityConfig]
class LeaderboardEntityConfig : PersistedEntityConfig
{
    public override EntityKind EntityKind => EntityKindGame.Leaderboard;
    public override Type EntityActorType => typeof(LeaderboardActor);
    public override EntityShardGroup EntityShardGroup => EntityShardGroup.Workloads;
    public override IShardingStrategy ShardingStrategy => ShardingStrategies.CreateSingletonService();
    public override NodeSetPlacement NodeSetPlacement => NodeSetPlacement.Service;
    public override TimeSpan ShardShutdownTimeout => TimeSpan.FromSeconds(10);
}