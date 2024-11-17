using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsShooter.Pickup
{
    public partial struct SpawnXpPickupsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SpawnXpPickups>();
            state.RequireForUpdate<XpPickupSpawner>();
            
            var locations = new NativeQueue<float3>(Allocator.Persistent);
            state.EntityManager.AddComponentData(state.SystemHandle, new SpawnXpPickups(){Locations = locations});
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spawnLocations = SystemAPI.GetSingletonRW<SpawnXpPickups>().ValueRW.Locations;
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            var xpPickupSpawner = SystemAPI.GetSingleton<XpPickupSpawner>();
            
            while (spawnLocations.TryDequeue(out var location))
            {
                var entity = ecb.Instantiate(xpPickupSpawner.Prefab);
                
                ecb.SetComponent(entity, LocalTransform.FromPosition(location));
            }
        }
    }
}