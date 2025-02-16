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
    public class ShopStatConfig : IGameConfigData<ShopStatId>
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
        public float UpgradeCost;
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
    }
}