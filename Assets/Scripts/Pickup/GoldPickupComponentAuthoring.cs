using DotsShooter.Destruction;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter.Pickup
{
    public class GoldPickupComponentAuthoring : MonoBehaviour
    {
        [SerializeField] 
        private float xp = 5f;

        public class GoldPickupComponentBaker : Baker<GoldPickupComponentAuthoring>
        {
            public override void Bake(GoldPickupComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GoldPickupComponent { Xp = authoring.xp });
                AddComponent<MarkedForDestruction>(entity);
                AddComponent<DestroyNextFrame>(entity);
                SetComponentEnabled<MarkedForDestruction>(entity, false);
                SetComponentEnabled<DestroyNextFrame>(entity, false);
            }
        }
    }
}