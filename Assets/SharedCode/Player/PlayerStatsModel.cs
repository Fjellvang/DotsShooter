using Metaplay.Core.Math;
using Metaplay.Core.Model;

namespace Game.Logic
{
    [MetaSerializable]
    public class PlayerStatsModel
    {
        [MetaMember(1)] // These could be implicit, but for now we're explicit to know whats happening.
        public F64 MoveSpeed = F64.FromFloat(5);
        [MetaMember(2)]
        public F64 AttackSpeed = F64.FromFloat(1);
        [MetaMember(3)]
        public F64 Damage = F64.FromFloat(1f);
        [MetaMember(4)]
        public F64 Health = F64.FromFloat(100f); //TODO: Implement health system
        [MetaMember(5)]
        public F64 Range = F64.FromFloat(10f);
        [MetaMember(6)]
        public F64 ExplosionRadius = F64.FromFloat(0.25f);

        public PlayerStatsModel()
        {
            
        }

        // TODO: Implement this, once we support game config
        // public void SetInitialStats(SharedGameConfig gameConfig)
        // {
        // }
    }
}