using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace DotsShooter
{
    [BurstCompile]
    public partial struct LifeTimeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<LifeTimeComponent>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            var childBufferFromEntity = SystemAPI.GetBufferLookup<Child>(true); 
            
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (lifeTime, entity) in SystemAPI.Query<RefRW<LifeTimeComponent>>().WithEntityAccess())
            {
                lifeTime.ValueRW.LifeTime -= deltaTime;
                if (lifeTime.ValueRO.LifeTime <= 0)
                {
                    Helpers.DestroyEntityHierarchy(entity, ref ecb, ref childBufferFromEntity);
                }
            }
        }
    }
}