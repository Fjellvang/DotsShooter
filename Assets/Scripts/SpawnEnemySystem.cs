using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;
namespace DotsShooter
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(TargetingSystem))]
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
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var enemiesToSpawn = (int)(SystemAPI.Time.ElapsedTime/2);
            
            for (int i = 0; i < enemiesToSpawn; i++)
            {
                SpawnEnemy(ref state, spawnEnemyData, buffer, ecb);
            }

            spawnEnemyData.ValueRW.SpawnTimer = spawnEnemyData.ValueRO.SpawnTime;
            
            SystemAPI.SetSingleton(spawnEnemyData.ValueRO);
            
            // Playback and dispose ECB
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
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
            

            var spawnTop = _random.NextBool();
            var x = _random.NextFloat(-spawnEnemyData.ValueRO.MaxX, spawnEnemyData.ValueRO.MaxX);//TODO: Remove magic numbers
            
            var position = spawnTop 
                ? new float3(x, spawnEnemyData.ValueRO.MaxY, 0) 
                : new float3(x, -spawnEnemyData.ValueRO.MaxY, 0);
            
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