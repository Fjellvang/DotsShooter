using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;
namespace DotsShooter
{
    public partial struct SpawnEnemySystem : ISystem
    {
        Random _random;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _random = new Random(1234);
            state.RequireForUpdate<EnemyPrefabs>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spawnEnemyData = SystemAPI.GetSingletonRW<SpawnEnemyData>();
            var buffer = SystemAPI.GetSingletonBuffer<EnemyPrefabs>();
            
            spawnEnemyData.ValueRW.SpawnTimer -= SystemAPI.Time.DeltaTime;
            if (spawnEnemyData.ValueRO.SpawnTimer > 0)
            {
                return;
            }

            var enemiesToSpawn = (int)(SystemAPI.Time.ElapsedTime/2);
            
            for (int i = 0; i < enemiesToSpawn; i++)
            {
                SpawnEnemy(ref state, spawnEnemyData, buffer);
            }

            spawnEnemyData.ValueRW.SpawnTimer = spawnEnemyData.ValueRO.SpawnTime;
            
            SystemAPI.SetSingleton(spawnEnemyData.ValueRO);
        }

        
        [BurstCompile]
        private void SpawnEnemy(ref SystemState state, RefRW<SpawnEnemyData> spawnEnemyData, in DynamicBuffer<EnemyPrefabs> enemies)
        {
            var enemyIndex = GetWeightedRandomEnemyIndex(enemies);
            var enemyPrefab = enemies[enemyIndex].Prefab;
            var enemy = state.EntityManager.Instantiate(enemyPrefab);
            var enemyTransform = SystemAPI.GetComponent<LocalTransform>(enemy);

            var spawnTop = _random.NextBool();
            var x = _random.NextFloat(-spawnEnemyData.ValueRO.MaxX, spawnEnemyData.ValueRO.MaxX);//TODO: Remove magic numbers
            
            enemyTransform.Position = spawnTop 
                ? new float3(x, spawnEnemyData.ValueRO.MaxY, 0) 
                : new float3(x, -spawnEnemyData.ValueRO.MaxY, 0);
            
            SystemAPI.SetComponent(enemy, enemyTransform);
        }
        
        private int GetWeightedRandomEnemyIndex(in DynamicBuffer<EnemyPrefabs> enemies)
        {
            int totalWeight = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                totalWeight += enemies[i].Weight;
            }

            int randomWeight = _random.NextInt(0, totalWeight);
            int currentWeight = 0;

            for (int i = 0; i < enemies.Length; i++)
            {
                currentWeight += enemies[i].Weight;
                if (randomWeight < currentWeight)
                {
                    return i;
                }
            }

            // Fallback to last enemy (should never happen if weights are positive)
            return enemies.Length - 1;
        }
    }
}