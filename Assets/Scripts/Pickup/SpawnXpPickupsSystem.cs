using DotsShooter.Destruction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DotsShooter.Pickup
{
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct SpawnXpPickupsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SpawnXpPickups>();
            state.RequireForUpdate<XpPickupSpawner>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // var spawnLocations = SystemAPI.GetSingletonRW<SpawnXpPickups>().ValueRW.Locations;
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            var xpPickupSpawner = SystemAPI.GetSingleton<XpPickupSpawner>();

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<DestroyNextFrame, SpawnXpPickups>())
            {
                var location = transform.ValueRO.Position;
                var entity = ecb.Instantiate(xpPickupSpawner.Prefab);
                ecb.SetComponent(entity, LocalTransform.FromPosition(location));
            }
        }
    }
}