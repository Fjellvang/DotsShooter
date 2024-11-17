using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Pickup
{
    
    public struct XpPickupSpawner : IComponentData
    {
        public Entity Prefab;
    }
    public class XpPickupSpawnerAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        public class XpPickupSpawnerBaker : Baker<XpPickupSpawnerAuthoring>
        {
            public override void Bake(XpPickupSpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity,
                    new XpPickupSpawner { Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic) });
            }
        }
    }
}