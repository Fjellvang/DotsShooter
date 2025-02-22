using DotsShooter.Destruction;
using Game.Logic.GameConfigs;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter.Pickup
{
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
                AddComponent<MarkedForDestruction>(entity);
                AddComponent<DestroyNextFrame>(entity);
                SetComponentEnabled<MarkedForDestruction>(entity, false);
                SetComponentEnabled<DestroyNextFrame>(entity, false);
            }
        }
    }
}