using DotsShooter.Destruction;
using DotsShooter.SimpleCollision;
using Unity.Burst;
using Unity.Entities;

namespace DotsShooter.Pickup
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GoldPickupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (_, entity) in SystemAPI.Query<RefRO<GoldPickupComponent>>()
                         .WithEntityAccess()
                         .WithNone<DestroyNextFrameTag>()
                     )
            {
                if (!state.EntityManager.HasComponent<SimpleCollisionEvent>(entity))
                {
                    continue;
                }
                
                var simpleCollisionBuffer = state.EntityManager.GetBuffer<SimpleCollisionEvent>(entity);
                if (simpleCollisionBuffer.Length > 0)
                {
                    SystemAPI.SetComponentEnabled<DestroyNextFrameTag>(entity, true);
                }
            }
        }
    }
}