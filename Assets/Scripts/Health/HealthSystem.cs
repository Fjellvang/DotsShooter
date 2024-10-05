using DotsShooter.SimpleCollision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace DotsShooter.Health
{
    public partial struct HealthSystem : ISystem
    {
        EntityQuery _healthQuery;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<HealthComponent>()
                // .WithAll<DynamicBuffer<TriggerEvent>>()
                ;
            
            state.RequireForUpdate<HealthComponent>();
            state.RequireForUpdate<SimpleCollisionComponent>();
            
            _healthQuery = state.GetEntityQuery(builder);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (health, entity) in SystemAPI.Query<RefRW<HealthComponent>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<SimpleCollisionEvent>(entity))
                {
                    continue;
                }
                
                var triggerEvents = state.EntityManager.GetBuffer<SimpleCollisionEvent>(entity);
                Debug.Log($"HealthSystem: Collision detected: {triggerEvents.Length}" );
                for (int i = 0; i < triggerEvents.Length; i++)
                {
                    // var triggerEvent = triggerEvents[i];
                    health.ValueRW.Health -= 1;
                }
            }
        }
    }
}