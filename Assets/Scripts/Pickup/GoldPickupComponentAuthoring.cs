using DotsShooter.Destruction;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Pickup
{
    public class GoldPickupComponentAuthoring : MonoBehaviour
    {
        [SerializeField] 
        private int _value = 1;

        public class GoldPickupComponentBaker : Baker<GoldPickupComponentAuthoring>
        {
            public override void Bake(GoldPickupComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GoldPickupComponent { Value = authoring._value });
                AddComponent<MarkedForDestruction>(entity);
                AddComponent<DestroyNextFrame>(entity);
                SetComponentEnabled<MarkedForDestruction>(entity, false);
                SetComponentEnabled<DestroyNextFrame>(entity, false);
            }
        }
    }
}