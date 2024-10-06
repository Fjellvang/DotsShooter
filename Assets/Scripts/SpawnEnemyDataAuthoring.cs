using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct SpawnEnemyData : IComponentData
    {
        public Entity Prefab;
        public float SpawnTime;
        public float SpawnTimer;
    }
    
    public class SpawnEnemyDataAuthoring : MonoBehaviour
    {
        public GameObject Prefab;
        public float SpawnTime;

        public class SpawnEnemyDataBaker : Baker<SpawnEnemyDataAuthoring>
        {
            public override void Bake(SpawnEnemyDataAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new SpawnEnemyData
                    {
                        Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
                        SpawnTime = authoring.SpawnTime
                    });
            }
        }
    }
}