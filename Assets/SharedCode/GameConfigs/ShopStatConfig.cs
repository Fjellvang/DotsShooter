using System;
using Game.Logic.PlayerActions;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;

namespace Game.Logic.GameConfigs
{
    [MetaSerializable]
    public enum PlayerStatIncreaseOperator{
        Add,
        Multiply
    }
    [MetaSerializable]
    public class ShopStatId : StringId<ShopStatId> { }

    [MetaSerializable]
    public class ShopStatConfig : IGameConfigData<ShopStatId>, IGameConfigPostLoad
    {
        [MetaMember(1)] 
        public ShopStatId Id; // The unique identifier for this config, ie its name
        /// <summary>
        /// How much should the stat increase per level
        /// </summary>
        [MetaMember(2)]
        public float StatIncreasePerLevel;
        /// <summary>
        /// how much it costs to upgrade this stat
        /// </summary>
        [MetaMember(3)]
        public int UpgradeCost;
        /// <summary>
        /// How much should the upgrade cost increase per level
        /// </summary>
        [MetaMember(4)]
        public float UpgradeCostPerLevel;
        /// <summary>
        /// Which operator to use when increasing this stat
        /// </summary>
        [MetaMember(5)]
        public PlayerStatIncreaseOperator IncreaseOperator;
        
        //TODO: implement stat levels.
        public ShopStatId ConfigKey => Id;

        public void PostLoad()
        {
            var parsed = Enum.TryParse<PlayerStat>(Id.Value, out var _);
            MetaDebug.Assert(parsed, "ShopStatConfig {0} has invalid Id", Id);
            MetaDebug.Assert(StatIncreasePerLevel > 0, "ShopStatConfig {0} has invalid StatIncreasePerLevel", Id);
            MetaDebug.Assert(UpgradeCost > 0, "ShopStatConfig {0} has invalid UpgradeCost", Id);
            MetaDebug.Assert((IncreaseOperator == PlayerStatIncreaseOperator.Add) ||
                             (IncreaseOperator == PlayerStatIncreaseOperator.Multiply && StatIncreasePerLevel != 0),
                "ShopStatConfig {0} has invalid IncreaseOperator", Id);
        }
    }
}