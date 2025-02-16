using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;

namespace Game.Logic.GameConfigs
{
    [MetaSerializable]
    public class StatId : StringId<StatId> { }

    [MetaSerializable]
    public class InitialStatsConfig : IGameConfigData<StatId>
    {
        [MetaMember(1)] 
        public StatId Id; // The unique identifier for this config, ie its name
        /// <summary>
        /// How much should the stat increase per level
        /// </summary>
        [MetaMember(2)]
        public float InitialValue;
        public StatId ConfigKey => Id;
    }
}