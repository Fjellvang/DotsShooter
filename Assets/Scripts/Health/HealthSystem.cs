using DotsShooter.Damage;
using DotsShooter.Destruction;
using DotsShooter.Player;
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
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<HealthComponent>();
            state.RequireForUpdate<SimpleCollisionComponent>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var bufferLookup = SystemAPI.GetBufferLookup<DamageData>();
            var markedForDestructionLookup = SystemAPI.GetComponentLookup<MarkedForDestruction>();
            
            // var deadEntities = SystemAPI.GetSingletonRW<DeadEntities>();
            // var parallelWriter = deadEntities.ValueRW.Value.AsParallelWriter();
            // TODO: Refactor this to parallel jobs?
            foreach (var (health, entity) in SystemAPI.Query<RefRW<HealthComponent>>()
                         .WithEntityAccess().WithNone<PlayerTag>())
            {
                HandleDamage(ref state, entity, health, ref markedForDestructionLookup, ref bufferLookup);
            }
            
            foreach (var (health, entity) in SystemAPI.Query<RefRW<HealthComponent>>()
                         .WithEntityAccess().WithAll<PlayerTag>())
            {
                if (HandleDamage(ref state, entity, health, ref markedForDestructionLookup, ref bufferLookup))
                {
                    SystemAPI.SetComponentEnabled<PlayerWasDamaged>(entity, true);
                }
            }
        }

        /// <summary>
        /// Handles Damage to an entity, returns true if damage was done
        /// </summary>
        /// <param name="state"></param>
        /// <param name="entity"></param>
        /// <param name="health"></param>
        /// <param name="markedForDestructionLookup"></param>
        /// <param name="damageBufferFromEntity"></param>
        /// <returns></returns>
        [BurstCompile]
        private static bool HandleDamage(ref SystemState state, 
            in Entity entity, 
            in RefRW<HealthComponent> health, 
            ref ComponentLookup<MarkedForDestruction> markedForDestructionLookup,
            ref BufferLookup<DamageData> damageBufferFromEntity
            )
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
                health.ValueRW.Health -= damageComponent.Damage;
                didDamage = true;
                if (health.ValueRW.Health <= 0)
                {
                    markedForDestructionLookup.SetComponentEnabled(entity, true);
                }
            }
            
            damage.Clear();

            return didDamage;
        }
    }
}