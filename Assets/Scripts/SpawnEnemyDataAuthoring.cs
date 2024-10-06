using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct SpawnEnemyData : IComponentData
    {
        public int MaxX;
        public int MaxY;
        public Entity Prefab;
        public float SpawnTime;
        public float SpawnTimer;
    }
    
    public class SpawnEnemyDataAuthoring : MonoBehaviour
    {
        public GameObject Prefab;
        public float SpawnTime;
        public int MaxX = 20;
        public int MaxY = 20;

        public class SpawnEnemyDataBaker : Baker<SpawnEnemyDataAuthoring>
        {
            public override void Bake(SpawnEnemyDataAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new SpawnEnemyData
                    {
                        MaxX = authoring.MaxX,
                        MaxY = authoring.MaxY,
                        Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
                        SpawnTime = authoring.SpawnTime
                    });
            }
        }
    }
}