using DotsShooter.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace DotsShooter
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct SpawnEnemySystem : ISystem
    {
        private Random _random;
        private EntityQuery _potentialSpawnPointsQuery;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateComponent>();
            state.RequireForUpdate<SimulationTime>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<GameStateInitializedComponent>();
            state.RequireForUpdate<EnemyPrefabs>();
            
            _random = Random.CreateFromIndex(1234);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gameStateComponent = SystemAPI.GetSingleton<GameStateComponent>();
            if (gameStateComponent.GameEnded)
            {
                return; //TODO: Probably not the cleanest, we could consider disabling this system when game is ended
            }
            var spawnEnemyData = SystemAPI.GetSingletonRW<SpawnEnemyData>();
            
            var simulationTime = SystemAPI.GetSingleton<SimulationTime>();
            var round = gameStateComponent.Round;
            var buffer = SystemAPI.GetSingletonBuffer<EnemyPrefabs>();
            
            spawnEnemyData.ValueRW.SpawnTimer -= SystemAPI.Time.DeltaTime;
            if (spawnEnemyData.ValueRO.SpawnTimer > 0)
            {
                return;
            }
            
            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            var parallelEcb = ecb.AsParallelWriter();
            
            var enemiesToSpawn = round * round + (int)(simulationTime.ElapsedTime / 2);
            
            // Prepare enemy weights for random selection
            var totalEnemyWeights = CalculateTotalWeights(buffer);
            
            // Create a job to both generate data and spawn entities in parallel
            var spawnEnemiesJob = new SpawnEnemiesParallelJob
            {
                Random = _random,
                MaxX = spawnEnemyData.ValueRO.MaxX,
                MaxY = spawnEnemyData.ValueRO.MaxY,
                EnemyPrefabs = buffer,
                TotalWeight = totalEnemyWeights,
                CommandBuffer = parallelEcb
            };
            
            spawnEnemiesJob.Schedule(enemiesToSpawn, 32).Complete();
            
            // Update the random state for next frame
            _random = spawnEnemiesJob.Random;
            
            // Reset the spawn timer
            spawnEnemyData.ValueRW.SpawnTimer = spawnEnemyData.ValueRO.SpawnTime;
            SystemAPI.SetSingleton(spawnEnemyData.ValueRO);
        }
        
        [BurstCompile]
        private int CalculateTotalWeights(in DynamicBuffer<EnemyPrefabs> enemies)
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
        [ReadOnly] public DynamicBuffer<EnemyPrefabs> EnemyPrefabs;
        [ReadOnly] public float MaxX;
        [ReadOnly] public float MaxY;
        [ReadOnly] public int TotalWeight;
        
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        public Random Random;
        
        [BurstCompile]
        public void Execute(int index)
        {
            // Create a new random state for each parallel job to avoid thread safety issues
            var localRandom = Random.CreateFromIndex((uint)(index + Random.NextUInt()));
            
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