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
            foreach (var (buffer, entity) in SystemAPI.Query<DynamicBuffer<SimpleCollisionEvent>>()
                         .WithAll<GoldPickupComponent>()
                         .WithEntityAccess()
                         .WithNone<DestroyNextFrameTag>()
                     )
            {
                if (buffer.Length > 0)
                {
                    SystemAPI.SetComponentEnabled<DestroyNextFrameTag>(entity, true);
                }
            }
        }
    }
}