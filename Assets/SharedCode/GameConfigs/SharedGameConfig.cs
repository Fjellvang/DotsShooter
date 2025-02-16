using Metaplay.Core.Config;

namespace Game.Logic.GameConfigs
{
    public class SharedGameConfig : SharedGameConfigBase
    {
        [GameConfigEntry("ShopConfig")]
        public GameConfigLibrary<ShopStatId, ShopStatConfig> ShopConfiguration { get; private set; }
        [GameConfigEntry("InitialStatsConfig")]
        public GameConfigLibrary<StatId, InitialStatsConfig> InitialStatsConfiguration { get; private set; }
    }
}