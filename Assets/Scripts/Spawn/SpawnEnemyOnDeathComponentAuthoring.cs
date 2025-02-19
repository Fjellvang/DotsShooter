using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public class SpawnEnemyOnDeathComponentAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        public class SpawnEnemyOnDeathComponentBaker : Baker<SpawnEnemyOnDeathComponentAuthoring>
        {
            public override void Bake(SpawnEnemyOnDeathComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new SpawnEnemyOnDeathComponent
                    {
                        Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic)
                    });
            }
        }
    }
}