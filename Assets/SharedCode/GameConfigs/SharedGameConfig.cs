using Metaplay.Core.Config;

namespace Game.Logic.GameConfigs
{
    public class SharedGameConfig : SharedGameConfigBase
    {
        [GameConfigEntry("ShopConfig")]
        public GameConfigLibrary<ShopStatId, ShopStatConfig> ShopConfiguration { get; private set; }
    }
}