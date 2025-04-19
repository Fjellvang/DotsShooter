using DotsShooter.Common;
using DotsShooter.Damage;
using DotsShooter.Destruction;
using DotsShooter.Player;
using DotsShooter.SimpleCollision;
using DotsShooter.VFX;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace DotsShooter.Health
{
    [UpdateInGroup(typeof(AttackSystemGroup))]
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
            var destroyNextFrameLookup = SystemAPI.GetComponentLookup<DestroyNextFrameTag>();
            var graphicsEntityDataLookup = SystemAPI.GetComponentLookup<GraphicsEntityData>();
            var flashColorOnDamageDataLookup = SystemAPI.GetComponentLookup<FlashColorOnDamageData>();
            var flashColorOnDamageTimerLookup = SystemAPI.GetComponentLookup<FlashColorOnDamageTimer>();
            var flashColorOnDamageData = SystemAPI.GetComponentLookup<FlashColorOnDamageData>();
            
            // TODO: Refactor this to parallel jobs?
            foreach (var (health, entity) in SystemAPI.Query<RefRW<HealthComponent>>()
                         .WithEntityAccess().WithNone<PlayerTag>())
            {
                if (HandleDamage(ref state, entity, health, ref destroyNextFrameLookup, ref bufferLookup))
                {
                    HandleFlash(entity, graphicsEntityDataLookup, flashColorOnDamageDataLookup, flashColorOnDamageTimerLookup, flashColorOnDamageData);
                }
            }
            
            foreach (var (health, entity) in SystemAPI.Query<RefRW<HealthComponent>>()
                         .WithEntityAccess().WithAll<PlayerTag>())
            {
                if (HandleDamage(ref state, entity, health, ref destroyNextFrameLookup, ref bufferLookup))
                {
                    SystemAPI.SetComponentEnabled<PlayerWasDamaged>(entity, true);
                    HandleFlash(entity, graphicsEntityDataLookup, flashColorOnDamageDataLookup, flashColorOnDamageTimerLookup, flashColorOnDamageData);
                }
            }
        }

        //TODO: refactor this to use a different system. Maybe a EntityWasDamaged System we can fire event off of
        private static void HandleFlash(Entity entity, 
            ComponentLookup<GraphicsEntityData> graphicsEntityDataLookup,
            ComponentLookup<FlashColorOnDamageData> flashColorOnDamageDataLookup, 
            ComponentLookup<FlashColorOnDamageTimer> flashColorOnDamageTimerLookup,
            ComponentLookup<FlashColorOnDamageData> flashColorOnDamageData)
        {
            if (!graphicsEntityDataLookup.HasComponent(entity)) return;
            
            var graphicsEntity = graphicsEntityDataLookup.GetRefRO(entity).ValueRO.Entity;
            flashColorOnDamageDataLookup.SetComponentEnabled(graphicsEntity, true);
            var flashColorOnDamageTimer = flashColorOnDamageTimerLookup.GetRefRW(graphicsEntity);
            var flashTime = flashColorOnDamageData.GetRefRO(graphicsEntity).ValueRO.FlashTime;
            flashColorOnDamageTimer.ValueRW.Value = flashTime;
        }

        /// <summary>
        /// Handles Damage to an entity, returns true if damage was done
        /// </summary>
        /// <param name="state"></param>
        /// <param name="entity"></param>
        /// <param name="health"></param>
        /// <param name="destroyNextFrameLookup"></param>
        /// <param name="damageBufferFromEntity"></param>
        /// <returns></returns>
        [BurstCompile]
        private static bool HandleDamage(ref SystemState state, 
            in Entity entity, 
            in RefRW<HealthComponent> health, 
            ref ComponentLookup<DestroyNextFrameTag> destroyNextFrameLookup,
            ref BufferLookup<DamageData> damageBufferFromEntity
            )
        {
            if (!damageBufferFromEntity.HasBuffer(entity) || damageBufferFromEntity[entity].IsEmpty)
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
                    destroyNextFrameLookup.SetComponentEnabled(entity, true);
                }
            }
            
            damage.Clear();

            return didDamage;
        }
    }
}