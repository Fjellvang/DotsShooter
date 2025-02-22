using Game.Logic.GameConfigs;
using Unity.Entities;

namespace DotsShooter.Pickup
{
    public struct GoldPickupComponent : IComponentData
    {
        public CoinType Value;
    }
}