using DotsShooter.SimpleCollision;
using Unity.Burst;
using Unity.Entities;

namespace DotsShooter.Pickup
{
    public partial struct XpPickupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deadEntities = SystemAPI.GetSingletonRW<DeadEntities>();
            var parallelWriter = deadEntities.ValueRW.Value.AsParallelWriter();

            foreach (var (xpPickupComponent, entity) in SystemAPI.Query<RefRO<XpPickupComponent>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<SimpleCollisionEvent>(entity))
                {
                    continue;
                }
                
                var simpleCollisionBuffer = state.EntityManager.GetBuffer<SimpleCollisionEvent>(entity);

                for (int i = 0; i < simpleCollisionBuffer.Length; i++)
                {
                    var simpleCollisionEvent = simpleCollisionBuffer[i];
                    var other = simpleCollisionEvent.GetOtherEntity(entity);

                    // Destroy the pickup.
                    parallelWriter.Enqueue(new DeadEntity(){Entity = entity, EntityType = EntityType.XpPickup});
                }
            }
        }
    }
}