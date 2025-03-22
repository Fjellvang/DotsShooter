using Game.Logic.GameConfigs;
using Game.Logic.PlayerActions;
using Metaplay.Core.Math;
using Metaplay.Core.Model;

namespace Game.Logic
{
    [MetaSerializable]
    public class PlayerStatsModel
    {
        [MetaMember(1)] // These could be implicit, but for now we're explicit to know whats happening.
        public F64 MoveSpeed = F64.FromFloat(1);
        [MetaMember(2)]
        public F64 Cooldown = F64.FromFloat(1);
        [MetaMember(3)]
        public F64 Damage = F64.FromFloat(1f);
        [MetaMember(4)]
        public F64 Health = F64.FromFloat(100f); //TODO: Implement health system
        [MetaMember(5)]
        public F64 Range = F64.FromFloat(1f);
        [MetaMember(6)]
        public F64 ExplosionRadius = F64.FromFloat(1f);
        [MetaMember(7)] 
        public int ExtraProjectiles = 0;

        public PlayerStatsModel()
        {
            
        }

        public void SetInitialStats(SharedGameConfig gameConfig)
        {
            MoveSpeed = F64.FromFloat(gameConfig.InitialStatsConfiguration[StatId.FromString(PlayerStat.MoveSpeed.ToString())].InitialValue);
            Cooldown = F64.FromFloat(gameConfig.InitialStatsConfiguration[StatId.FromString(PlayerStat.Cooldown.ToString())].InitialValue);
            Damage = F64.FromFloat(gameConfig.InitialStatsConfiguration[StatId.FromString(PlayerStat.Damage.ToString())].InitialValue);
            Health = F64.FromFloat(gameConfig.InitialStatsConfiguration[StatId.FromString(PlayerStat.Health.ToString())].InitialValue);
            Range = F64.FromFloat(gameConfig.InitialStatsConfiguration[StatId.FromString(PlayerStat.Range.ToString())].InitialValue);
            ExplosionRadius = F64.FromFloat(gameConfig.InitialStatsConfiguration[StatId.FromString(PlayerStat.ExplosionRadius.ToString())].InitialValue);
            ExtraProjectiles = 0;
        }
        
        public float GetStatAsFloat(PlayerStat stat)
        {
            return stat switch
            {
                PlayerStat.MoveSpeed => MoveSpeed.Float,
                PlayerStat.Cooldown => Cooldown.Float,
                PlayerStat.Damage => Damage.Float,
                PlayerStat.Health => Health.Float,
                PlayerStat.Range => Range.Float,
                PlayerStat.ExplosionRadius => ExplosionRadius.Float,
                PlayerStat.ExtraProjectile => ExtraProjectiles,
                _ => 0
            };
        }
    }
}