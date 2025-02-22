using Metaplay.Core;

namespace Game.Logic.TypeCodes
{
    [EntityKindRegistry(100, 300)]
    public static class EntityKindGame
    {
        public static readonly EntityKind Leaderboard = EntityKind.FromValue(100);
    }
}