using DotsShooter.Destruction;
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
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (lifeTime, entity) in SystemAPI.Query<RefRW<LifeTimeComponent>>()
                         .WithEntityAccess())
            {
                lifeTime.ValueRW.LifeTime -= deltaTime;
                if (lifeTime.ValueRO.LifeTime <= 0)
                {
                    SystemAPI.SetComponentEnabled<MarkedForDestruction>(entity, true);
                }
            }
        }
    }
}