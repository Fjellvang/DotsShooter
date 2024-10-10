using DotsShooter.Player;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsShooter
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ShootingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<AutoTargetingPlayer>();
            state.RequireForUpdate<EnemyTag>();
        }        
        
        public void OnUpdate(ref SystemState state)
        {
            var targetEnemy = SystemAPI.GetSingletonRW<AutoTargetingPlayer>().ValueRO;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
            // var targetEnemy = SystemAPI.GetComponent<AutoTargetingPlayer>(playerEntity);

            foreach (var shooter in SystemAPI.Query<RefRW<AutoShootingComponent>>())
            {
                if (targetEnemy.Direction.Equals(float3.zero))
                {
                    continue;
                }
                
                shooter.ValueRW.CooldownTimer -= SystemAPI.Time.DeltaTime;
                if (shooter.ValueRO.CooldownTimer > 0)
                {
                    continue;
                }
                
                var shootingComponent = shooter.ValueRO;
                
                var bullet = state.EntityManager.Instantiate(shootingComponent.ProjectilePrefab);
                var bulletTransform = SystemAPI.GetComponent<LocalTransform>(bullet);
                var bulletMovement = SystemAPI.GetComponent<MovementComponent>(bullet);
                
                bulletTransform.Position = playerPosition + targetEnemy.Direction * shooter.ValueRO.SpawnOffset;
                bulletTransform.Rotation = quaternion.Euler(0, 0, math.atan2(targetEnemy.Direction.y, targetEnemy.Direction.x)); 
                bulletMovement.Direction = targetEnemy.Direction;
                bulletMovement.Speed = shootingComponent.ProjectileSpeed;
                shooter.ValueRW.CooldownTimer = shootingComponent.Cooldown;
                
                SystemAPI.SetComponent(bullet, bulletTransform); 
                SystemAPI.SetComponent(bullet, bulletMovement);
            }
            
        }
    }
}