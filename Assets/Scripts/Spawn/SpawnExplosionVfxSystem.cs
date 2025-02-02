using DotsShooter.Damage.AreaDamage;
using DotsShooter.Destruction;
using DotsShooter.Pickup;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsShooter
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct SpawnExplosionVfxSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnExplosionVfx>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // var spawnLocations = SystemAPI.GetSingletonRW<SpawnXpPickups>().ValueRW.Locations;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var explosionSpawner = SystemAPI.GetSingleton<SpawnExplosionVfx>();

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<DestroyNextFrame, AreaDamage>())
            {
                var location = transform.ValueRO.Position;
                var entity = ecb.Instantiate(explosionSpawner.Prefab);

                //TODO: Hardcoded rotation, not great - find a better soluiton
                var existing = SystemAPI.GetComponentRO<LocalTransform>(explosionSpawner.Prefab);
                var localTransform = LocalTransform.FromPosition(location);
                localTransform.Rotation = existing.ValueRO.Rotation;
                ecb.SetComponent(entity, localTransform);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}