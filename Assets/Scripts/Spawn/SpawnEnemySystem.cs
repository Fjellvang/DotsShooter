using DotsShooter.Common;
using DotsShooter.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsShooter
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct SpawnEnemySystem : ISystem
    {
        private EntityQuery _potentialSpawnPointsQuery;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnEnemyData>();
            state.RequireForUpdate<GameStateComponent>();
            state.RequireForUpdate<SimulationTime>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<GameStateInitializedComponent>();
        }
        
        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gameStateComponent = SystemAPI.GetSingleton<GameStateComponent>();
            if (gameStateComponent.GameEnded)
            {
                return; //TODO: Probably not the cleanest, we could consider disabling this system when game is ended
            }
            var enemyDataEntity = SystemAPI.GetSingletonEntity<SpawnEnemyData>();
            var random = SystemAPI.GetComponentRW<EntityRandom>(enemyDataEntity);
            var spawnEnemyData = SystemAPI.GetSingletonRW<SpawnEnemyData>();
            var simulationTime = SystemAPI.GetSingleton<SimulationTime>();
            var round = gameStateComponent.Round - 1;
            var waveDataBuffer = SystemAPI.GetSingletonBuffer<WaveData>();
            var currentWaveData = waveDataBuffer[math.min(round, waveDataBuffer.Length - 1)];

            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            
            var spawnEventBuffer = SystemAPI.GetBuffer<SpawnEventData>(currentWaveData.PrefabsBufferEntity);
            for (int i = 0; i < spawnEventBuffer.Length; i++)
            {
                var spawnEvent = spawnEventBuffer[i];
                
                if (!(simulationTime.ElapsedTime >= spawnEvent.SpawnAfterSeconds) || spawnEvent.IsSpawned) continue;
                
                ecb.Instantiate(spawnEvent.FormationEntity);
                spawnEvent.IsSpawned = true;
                
                spawnEventBuffer[i] = spawnEvent;
            }
            // HandleSpawnEvents(ref state, currentWaveData.PrefabsBufferEntity, simulationTime, ecb);

            spawnEnemyData.ValueRW.SpawnTimer -= SystemAPI.Time.DeltaTime;
            if (spawnEnemyData.ValueRO.SpawnTimer > 0)
            {
                return;
            }
            
            var parallelEcb = ecb.AsParallelWriter();
            
            var enemiesToSpawn = round * round + (int)(simulationTime.ElapsedTime / 2);
            
            // Prepare enemy weights for random selection
            var currentEnemyBuffer = SystemAPI.GetBuffer<EnemyPrefabData>(currentWaveData.PrefabsBufferEntity);
            var totalEnemyWeights = CalculateTotalWeights(ref currentEnemyBuffer);
            
            // Create a job to both generate data and spawn entities in parallel
            var spawnEnemiesJob = new SpawnEnemiesParallelJob
            {
                RandomSeed = random.ValueRW.Value.NextUInt(),
                MaxX = spawnEnemyData.ValueRO.MaxX,
                MaxY = spawnEnemyData.ValueRO.MaxY,
                EnemyPrefabs = currentEnemyBuffer,
                TotalWeight = totalEnemyWeights,
                CommandBuffer = parallelEcb
            };
            
            spawnEnemiesJob.Schedule(enemiesToSpawn, 32).Complete();
            
            // Reset the spawn timer
            spawnEnemyData.ValueRW.SpawnTimer = currentWaveData.TimeBetweenWaves;
            SystemAPI.SetSingleton(spawnEnemyData.ValueRO);
        }

        private void HandleSpawnEvents(ref SystemState state, Entity waveEntity, SimulationTime simulationTime, EntityCommandBuffer ecb)
        {
            var spawnEventBuffer = SystemAPI.GetBuffer<SpawnEventData>(waveEntity);
            for (int i = 0; i < spawnEventBuffer.Length; i++)
            {
                var spawnEvent = spawnEventBuffer[i];
                
                if (!(simulationTime.ElapsedTime >= spawnEvent.SpawnAfterSeconds) || spawnEvent.IsSpawned) continue;
                
                ecb.Instantiate(spawnEvent.FormationEntity);
                spawnEvent.IsSpawned = true;
            }
        }

        [BurstCompile]
        private int CalculateTotalWeights(ref DynamicBuffer<EnemyPrefabData> enemies)
        {
            int totalWeight = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                totalWeight += enemies[i].Weight;
            }
            return totalWeight;
        }
    }
    
    [BurstCompile]
    public struct SpawnEnemiesParallelJob : IJobParallelFor
    {
        [ReadOnly] public DynamicBuffer<EnemyPrefabData> EnemyPrefabs;
        [ReadOnly] public float MaxX;
        [ReadOnly] public float MaxY;
        [ReadOnly] public int TotalWeight;
        
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        public uint RandomSeed;
        
        [BurstCompile]
        public void Execute(int index)
        {
            // Create a new random state for each parallel job to avoid thread safety issues
            var localRandom = Random.CreateFromIndex((uint)index + RandomSeed);
            
            // Generate random position
            var x = localRandom.NextFloat(-MaxX, MaxX);
            var y = localRandom.NextFloat(-MaxY, MaxY);
            var position = new float3(x, y, 0);
            
            // Select random enemy based on weights
            int enemyIndex = GetWeightedRandomEnemyIndex(localRandom);
            var enemyPrefab = EnemyPrefabs[enemyIndex].Prefab;
            
            // Use the index as sortKey to ensure deterministic results
            var enemy = CommandBuffer.Instantiate(index, enemyPrefab);
            CommandBuffer.SetComponent(index, enemy, LocalTransform.FromPosition(position));
        }
        
        private int GetWeightedRandomEnemyIndex(Random random)
        {
            int randomWeight = random.NextInt(0, TotalWeight);
            int currentWeight = 0;
            
            for (int i = 0; i < EnemyPrefabs.Length; i++)
            {
                currentWeight += EnemyPrefabs[i].Weight;
                if (randomWeight < currentWeight)
                {
                    return i;
                }
            }
            
            // Fallback to last enemy (should never happen if weights are positive)
            return EnemyPrefabs.Length - 1;
        }
    }
}