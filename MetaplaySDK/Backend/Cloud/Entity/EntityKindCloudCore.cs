// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;

namespace Metaplay.Cloud.Entity
{
    /// <summary>
    /// Register SDK CloudCore EntityKinds. Game-specific EntityKinds should be registered
    /// in your game-specific EntityKind registry class <c>EntityKindGame</c>.
    /// </summary>
    [EntityKindRegistry(10, 30)]
    [EntityKindRegistry(64, 100)]
    public static class EntityKindCloudCore
    {
        public static readonly EntityKind Connection                = EntityKind.FromValue(10);
        public static readonly EntityKind WebSocketConnection       = EntityKind.FromValue(25);
        public static readonly EntityKind GlobalStateManager        = EntityKind.FromValue(11);
        public static readonly EntityKind GlobalStateProxy          = EntityKind.FromValue(12);
        public static readonly EntityKind StatsCollectorManager     = EntityKind.FromValue(23);
        public static readonly EntityKind StatsCollectorProxy       = EntityKind.FromValue(24);
        public static readonly EntityKind LiveOpsTimelineManager    = EntityKind.FromValue(68);
        public static readonly EntityKind LiveOpsTimelineProxy      = EntityKind.FromValue(69);
        public static readonly EntityKind PushNotifier              = EntityKind.FromValue(13);
        public static readonly EntityKind InAppValidator            = EntityKind.FromValue(14);
        public static readonly EntityKind AdminApi                  = EntityKind.FromValue(15);
        public static readonly EntityKind DiagnosticTool            = EntityKind.FromValue(16);
        public static readonly EntityKind DatabaseScanCoordinator   = EntityKind.FromValue(17);
        public static readonly EntityKind DatabaseScanWorker        = EntityKind.FromValue(18);
        #if !METAPLAY_DISABLE_GUILDS
        public static readonly EntityKind GuildRecommender          = EntityKind.FromValue(19);
        public static readonly EntityKind GuildSearch               = EntityKind.FromValue(20);
        #endif
        public static readonly EntityKind BackgroundTask            = EntityKind.FromValue(21);
        public static readonly EntityKind SegmentSizeEstimator      = EntityKind.FromValue(22);
        public static readonly EntityKind NftManager                = EntityKind.FromValue(26);
        public static readonly EntityKind LeagueManager             = EntityKind.FromValue(27);
        public static readonly EntityKind UdpPassthrough            = EntityKind.FromValue(28);
        public static readonly EntityKind LoadTracker               = EntityKind.FromValue(29);
        public static readonly EntityKind AutoScaling               = EntityKind.FromValue(64);
        public static readonly EntityKind KeyManager                = EntityKind.FromValue(65);
        public static readonly EntityKind TelemetryManager          = EntityKind.FromValue(66);
        public static readonly EntityKind PlayerIncidentPullService = EntityKind.FromValue(67);
    }
}
