using DotsShooter.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;
namespace DotsShooter
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct SpawnEnemySystem : ISystem
    {
        Random _random;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateComponent>();
            state.RequireForUpdate<SimulationTime>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<GameStateInitializedComponent>();
            _random = new Random(1234);
            state.RequireForUpdate<EnemyPrefabs>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spawnEnemyData = SystemAPI.GetSingletonRW<SpawnEnemyData>();
            var simulationTime = SystemAPI.GetSingleton<SimulationTime>();
            var round = SystemAPI.GetSingleton<GameStateComponent>().Round;
            var buffer = SystemAPI.GetSingletonBuffer<EnemyPrefabs>();
            
            spawnEnemyData.ValueRW.SpawnTimer -= SystemAPI.Time.DeltaTime;
            if (spawnEnemyData.ValueRO.SpawnTimer > 0)
            {
                return;
            }
            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged); 
            
            var enemiesToSpawn = round * round + (int)(simulationTime.ElapsedTime / 2) ;// increase spawn rate over time, 1 enemy every 2 seconds, TODO: make this configurable
            
            for (int i = 0; i < enemiesToSpawn; i++)
            {
                SpawnEnemy(ref state, spawnEnemyData, buffer, ecb);
            }

            spawnEnemyData.ValueRW.SpawnTimer = spawnEnemyData.ValueRO.SpawnTime;
            
            SystemAPI.SetSingleton(spawnEnemyData.ValueRO);
        }

        
        [BurstCompile]
        private void SpawnEnemy(ref SystemState state,
            RefRW<SpawnEnemyData> spawnEnemyData,
            in DynamicBuffer<EnemyPrefabs> enemies,
            EntityCommandBuffer ecb)
        {
            var enemyIndex = GetWeightedRandomEnemyIndex(enemies);
            var enemyPrefab = enemies[enemyIndex].Prefab;
            var enemy = ecb.Instantiate(enemyPrefab); 
            

            var x = _random.NextFloat(-spawnEnemyData.ValueRO.MaxX, spawnEnemyData.ValueRO.MaxX);
            var y = _random.NextFloat(-spawnEnemyData.ValueRO.MaxY, spawnEnemyData.ValueRO.MaxY);

            var position = new float3(x, y, 0);
            
            ecb.SetComponent(enemy, LocalTransform.FromPosition(position));
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