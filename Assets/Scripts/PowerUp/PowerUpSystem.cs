using DotsShooter.Damage.AreaDamage;
using DotsShooter.Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace DotsShooter.PowerUp
{
    public partial struct PowerUpSystem : ISystem
    {
        const float AreaDamageRadiusMultiplier = 1.1f;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            // state.RequireForUpdate<PowerUpComponent>();
            var pendingPowerUps = new NativeQueue<PowerUp>(Allocator.Persistent);
            state.EntityManager.AddComponentData(state.SystemHandle, new PowerUpComponent {PendingPowerUps = pendingPowerUps});
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var powerUps = SystemAPI.GetSingletonRW<PowerUpComponent>().ValueRW;
            while(powerUps.PendingPowerUps.TryDequeue(out var powerUp))
            {
                //TODO: This system is not efficient, it should be split into multiple systems and cleaned up.
                foreach (var (autoShooting, movement, entity) in 
                         SystemAPI.Query<RefRW<AutoShootingComponent>, RefRW<MovementComponent>>()
                             .WithAll<PlayerTag>()
                             .WithEntityAccess())
                {
                    switch (powerUp.Type)
                    {
                        case PowerUpType.AttackSpeed:
                            autoShooting.ValueRW.Cooldown *= 0.9f;
                            break;
                        case PowerUpType.Damage:
                            autoShooting.ValueRW.ProjectileDamage *= 1.2f;
                            break;
                        case PowerUpType.MovementSpeed:
                            movement.ValueRW.Speed *= 1.1f;
                            break;
                        case PowerUpType.ExplosionRadius:
                            autoShooting.ValueRW.ProjectileRadius *= AreaDamageRadiusMultiplier;
                            break;
                        default:
                            break;
                    }
                }
                
            }
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            var powerUps = SystemAPI.GetSingletonRW<PowerUpComponent>();
            powerUps.ValueRW.PendingPowerUps.Dispose();
        }
    }
}