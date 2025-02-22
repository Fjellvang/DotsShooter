using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;

namespace Game.Logic.GameConfigs
{
    [MetaSerializable]
    public enum CoinType{
        Small,
        Medium,
        Large
    }
    
    [MetaSerializable]
    public class CoinValueConfig : IGameConfigData<CoinType>, IGameConfigPostLoad
    {
        [MetaMember(1)] 
        public CoinType Id; // The unique identifier for this config, ie its name
        /// <summary>
        /// How much this coin is worth
        /// </summary>
        [MetaMember(2)]
        public int Value;
        public CoinType ConfigKey => Id;
        
        public void PostLoad()
        {
            MetaDebug.Assert(Value > 0, "CoinValueConfig {0} has negative or zero Value", Id);
        }
    }
}