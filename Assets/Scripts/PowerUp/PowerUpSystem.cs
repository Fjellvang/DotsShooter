using DotsShooter.Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.PowerUp
{
    public partial struct PowerUpSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            // state.RequireForUpdate<PowerUpComponent>();
            Debug.Log("PowerUpSystem OnCreate");
            
            var pendingPowerUps = new NativeQueue<PowerUp>(Allocator.Persistent);
            state.EntityManager.AddComponentData(state.SystemHandle, new PowerUpComponent {PendingPowerUps = pendingPowerUps});
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var powerUps = SystemAPI.GetSingletonRW<PowerUpComponent>().ValueRO;
            
            while(powerUps.PendingPowerUps.TryDequeue(out var powerUp))
            {
                // Not really the best way to implement power ups. But it works for now
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
                        default:
                            break;
                    }
                }
            }
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            var deadEntities = SystemAPI.GetSingletonRW<PowerUpComponent>();
            deadEntities.ValueRW.PendingPowerUps.Dispose();
        }
    }
}