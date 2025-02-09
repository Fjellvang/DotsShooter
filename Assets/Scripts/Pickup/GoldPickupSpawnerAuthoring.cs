using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Pickup
{
    
    public struct XpPickupSpawner : IComponentData
    {
        public Entity Prefab;
    }
    public class GoldPickupSpawnerAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        public class XpPickupSpawnerBaker : Baker<GoldPickupSpawnerAuthoring>
        {
            public override void Bake(GoldPickupSpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity,
                    new XpPickupSpawner { Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Renderable) });
            }
        }
    }
}