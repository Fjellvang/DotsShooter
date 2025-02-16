using System;
using Game.Logic.PlayerActions;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;

namespace Game.Logic.GameConfigs
{
    [MetaSerializable]
    public class StatId : StringId<StatId> { }

    [MetaSerializable]
    public class InitialStatsConfig : IGameConfigData<StatId>, IGameConfigPostLoad
    {
        [MetaMember(1)] 
        public StatId Id; // The unique identifier for this config, ie its name
        /// <summary>
        /// How much should the stat increase per level
        /// </summary>
        [MetaMember(2)]
        public float InitialValue;
        public StatId ConfigKey => Id;
        
        public void PostLoad()
        {
            var parsed = Enum.TryParse<PlayerStat>(Id.Value, out var _);
            MetaDebug.Assert(parsed, "ShopStatConfig {0} has invalid Id", Id);
            MetaDebug.Assert(InitialValue > 0, "ShopStatConfig {0} has negative or zero InitialValue", Id);
        }
    }
}