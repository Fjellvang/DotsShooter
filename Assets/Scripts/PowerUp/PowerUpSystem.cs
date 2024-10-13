using DotsShooter.Player;
using Unity.Burst;
using Unity.Entities;
using Unity.VisualScripting;
using UnityEngine;

namespace DotsShooter.PowerUp
{
    public partial struct PowerUpSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<PowerUpComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            foreach (var (powerUp, autoShooting, movement, entity) in 
                     SystemAPI.Query<RefRO<PowerUpComponent>, RefRW<AutoShootingComponent>, RefRW<MovementComponent>>()
                         .WithAll<PlayerTag>()
                         .WithEntityAccess())
            {
                switch (powerUp.ValueRO.Type)
                {
                    case PowerUpType.AttackSpeed:
                        autoShooting.ValueRW.Cooldown *= 0.9f;
                        break;
                    case PowerUpType.Damage:
                        break;
                    case PowerUpType.MovementSpeed:
                        movement.ValueRW.Speed *= 1.1f;
                        break;
                    default:
                        break;
                }

                ecb.RemoveComponent<PowerUpComponent>(entity);
            }
        }
    }
}