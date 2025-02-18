using System;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct EnemyPrefabs : IBufferElementData
    {
        public Entity Prefab;
        public int Weight;
    }
    
    public struct SpawnEnemyData : IComponentData
    {
        public int MaxX;
        public int MaxY;
        public float SpawnTime;
        public float SpawnTimer;
        public UnityObjectRef<GameStateTracker> GameStateTracker; //TODO: Find a cleaner solution. The round could be injected on game start. For now this is a hack
    }
    
    public class SpawnEnemyDataAuthoring : MonoBehaviour
    {
        [Serializable]
        public class EnemyData {
            public GameObject Prefab;
            public int Weight;
        }
        public EnemyData[] Enemies;
        public float SpawnTime;
        public int MaxX = 20;
        public int MaxY = 20;
        public GameStateTracker GameStateTracker;

        public class SpawnEnemyDataBaker : Baker<SpawnEnemyDataAuthoring>
        {
            public override void Bake(SpawnEnemyDataAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var enemyPrefabs = AddBuffer<EnemyPrefabs>(entity);
                AddComponent(entity,
                    new SpawnEnemyData
                    {
                        MaxX = authoring.MaxX,
                        MaxY = authoring.MaxY,
                        SpawnTime = authoring.SpawnTime,
                        SpawnTimer = 0,
                        GameStateTracker = authoring.GameStateTracker
                    });
                foreach (var enemyPrefab in authoring.Enemies)
                {
                    enemyPrefabs.Add(new EnemyPrefabs
                    {
                        Prefab = GetEntity(enemyPrefab.Prefab, TransformUsageFlags.Dynamic),
                        Weight = enemyPrefab.Weight
                    });
                }
            }
        }
    }
}