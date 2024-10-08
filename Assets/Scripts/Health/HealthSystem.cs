using DotsShooter.Damage;
using DotsShooter.SimpleCollision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace DotsShooter.Health
{
    [UpdateAfter(typeof(DamageOnCollisionSystem))]
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
            var bufferLookup = SystemAPI.GetBufferLookup<DamageData>();
            foreach (var (health, entity) in SystemAPI.Query<RefRW<HealthComponent>>()
                         .WithEntityAccess().WithNone<PlayerTag>())
            {
                HandleDamage(ref state, entity, health, ref ecb,ref bufferLookup, ref childBufferFromEntity);
            }
            
            foreach (var (health, entity) in SystemAPI.Query<RefRW<HealthComponent>>()
                         .WithEntityAccess().WithAll<PlayerTag>())
            {
                if (HandleDamage(ref state, entity, health, ref ecb,ref bufferLookup, ref childBufferFromEntity))
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
        /// <param name="damageBufferFromEntity"></param>
        /// <param name="childBufferFromEntity"></param>
        /// <returns></returns>
        [BurstCompile]
        private static bool HandleDamage(ref SystemState state, 
            in Entity entity, 
            in RefRW<HealthComponent> health, 
            ref EntityCommandBuffer ecb,
            ref BufferLookup<DamageData> damageBufferFromEntity,
            ref BufferLookup<Child> childBufferFromEntity)
        {
            if (!damageBufferFromEntity.HasBuffer(entity))
            {
                return false;
            }
                
            var didDamage = false;
            var damage = damageBufferFromEntity[entity];
            
            for (int i = 0; i < damage.Length; i++)
            {
                var damageComponent = damage[i];
                // TODO: add a "damage" component
                health.ValueRW.Health -= damageComponent.Damage;
                didDamage = true;
                // TODO: add a "dead system"
                if (health.ValueRW.Health <= 0)
                {
                    // use ECB to destroy entity
                    Helpers.DestroyEntityHierarchy(entity, ref ecb, ref childBufferFromEntity);
                }
            }
            
            damage.Clear();

            return didDamage;
        }
    }
}