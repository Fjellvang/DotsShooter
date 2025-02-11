// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

namespace Metaplay.Core
{
    /// <summary>
    /// Registry for shared code SDK core EntityKinds. Game-specific EntityKinds should be added
    /// in the <c>EntityKindGame</c> registry class.
    /// </summary>
    [EntityKindRegistry(1, 10)]
    public static class EntityKindCore
    {
        public static readonly EntityKind Player    = EntityKind.FromValue(1);
        public static readonly EntityKind Session   = EntityKind.FromValue(2);
        public static readonly EntityKind Guild     = EntityKind.FromValue(3);
        public static readonly EntityKind Division  = EntityKind.FromValue(4);
    }

    /// <summary>
    /// Dummy registry for reserving various value ranges to prevent conflicts from forming.
    /// We're leaving the legacy [30, 50) and the new [100, 300) ranges for customers to use.
    /// More values can be freed up as needed.
    /// </summary>
    // BotClient uses the range [60, 64)
    // EntityKindCloudCore uses range [64, 100)
    [EntityKindRegistry(300, 310)] // AccountApi has this reserved but is not using it
    [EntityKindRegistry(310, 1024)]
    public static class EntityKindReserved
    {
    }
}
