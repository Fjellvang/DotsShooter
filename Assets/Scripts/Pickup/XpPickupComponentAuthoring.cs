using DotsShooter.Destruction;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter.Pickup
{
    public class XpPickupComponentAuthoring : MonoBehaviour
    {
        [SerializeField] 
        private float xp = 5f;

        public class XpPickupComponentBaker : Baker<XpPickupComponentAuthoring>
        {
            public override void Bake(XpPickupComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new XpPickupComponent { Xp = authoring.xp });
                AddComponent<MarkedForDestruction>(entity);
                AddComponent<DestroyNextFrame>(entity);
                SetComponentEnabled<MarkedForDestruction>(entity, false);
                SetComponentEnabled<DestroyNextFrame>(entity, false);
            }
        }
    }
}