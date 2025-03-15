using DotsShooter.Destruction;
using Game.Logic.GameConfigs;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Pickup
{
    [RequireComponent(typeof(DestroyableAuthor))]
    public class GoldPickupComponentAuthoring : MonoBehaviour
    {
        [SerializeField] 
        private CoinType coinType;

        public class GoldPickupComponentBaker : Baker<GoldPickupComponentAuthoring>
        {
            public override void Bake(GoldPickupComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GoldPickupComponent { Value = authoring.coinType });
            }
        }
    }
}