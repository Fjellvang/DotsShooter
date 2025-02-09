using DotsShooter.Destruction;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace DotsShooter.Pickup
{
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct SpawnGoldPickupsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SpawnGoldPickups>();
            state.RequireForUpdate<XpPickupSpawner>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            var xpPickupSpawner = SystemAPI.GetSingleton<XpPickupSpawner>();

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<DestroyNextFrame, SpawnGoldPickups>())
            {
                var location = transform.ValueRO.Position;
                var entity = ecb.Instantiate(xpPickupSpawner.Prefab);
                ecb.SetComponent(entity, LocalTransform.FromPosition(location));
            }
        }
    }
}