using DotsShooter.Damage;
using DotsShooter.SimpleCollision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
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
                ;
            
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<HealthComponent>();
            state.RequireForUpdate<SimpleCollisionComponent>();
            
            _healthQuery = state.GetEntityQuery(builder);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            var childBufferFromEntity = SystemAPI.GetBufferLookup<Child>(true); 
            foreach (var (health, entity) in SystemAPI.Query<RefRW<HealthComponent>>()
                         .WithEntityAccess().WithNone<PlayerTag>())
            {
                HandleDamage(ref state, entity, health, ref ecb, ref childBufferFromEntity);
            }
            
            foreach (var (health, entity) in SystemAPI.Query<RefRW<HealthComponent>>()
                         .WithEntityAccess().WithAll<PlayerTag>())
            {
                if (HandleDamage(ref state, entity, health, ref ecb, ref childBufferFromEntity))
                {
                    ecb.AddComponent(entity, new PlayerWasDamaged());
                }
            }
        }

        /// <summary>
        /// Handles Damage to an entity, returns true if damage was done
        /// </summary>
        /// <param name="state"></param>
        /// <param name="entity"></param>
        /// <param name="health"></param>
        /// <param name="ecb"></param>
        /// <param name="childBufferFromEntity"></param>
        /// <returns></returns>
        private static bool HandleDamage(ref SystemState state, 
            Entity entity, 
            RefRW<HealthComponent> health, 
            ref EntityCommandBuffer ecb,
            ref BufferLookup<Child> childBufferFromEntity)
        {
            if (!state.EntityManager.HasComponent<SimpleCollisionEvent>(entity))
            {
                return false;
            }
                
            var didDamage = false;
            var triggerEvents = state.EntityManager.GetBuffer<SimpleCollisionEvent>(entity);
            for (int i = 0; i < triggerEvents.Length; i++)
            {
                // var triggerEvent = triggerEvents[i];
                // TODO: add a "damage" component
                health.ValueRW.Health -= 1;
                didDamage = true;
                // TODO: add a "dead system"
                if (health.ValueRW.Health <= 0)
                {
                    // use ECB to destroy entity
                    Helpers.DestroyEntityHierarchy(entity, ref ecb, ref childBufferFromEntity);
                }
            }

            return didDamage;
        }
    }
}