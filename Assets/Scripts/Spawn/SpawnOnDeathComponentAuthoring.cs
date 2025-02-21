using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public class SpawnOnDeathComponentAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        public class SpawnEnemyOnDeathComponentBaker : Baker<SpawnOnDeathComponentAuthoring>
        {
            public override void Bake(SpawnOnDeathComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new SpawnOnDeathComponent
                    {
                        Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic)
                    });
            }
        }
    }
}