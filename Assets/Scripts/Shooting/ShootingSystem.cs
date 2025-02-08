using DotsShooter.Damage;
using DotsShooter.Damage.AreaDamage;
using DotsShooter.Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsShooter
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TargetingSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ShootingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<AutoTargetingPlayer>();
            state.RequireForUpdate<EnemyTag>();
        }        
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var targetEnemy = SystemAPI.GetSingletonRW<AutoTargetingPlayer>().ValueRO;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        
            float deltaTime = SystemAPI.Time.DeltaTime;
        
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            foreach (var (shooter, entity) in SystemAPI.Query<RefRW<AutoShootingComponent>>().WithEntityAccess())
            {
                if (targetEnemy.Direction.Equals(float3.zero))
                {
                    continue;
                }
            
                shooter.ValueRW.CooldownTimer -= deltaTime;
                if (shooter.ValueRO.CooldownTimer > 0)
                {
                    continue;
                }
            
                var shootingComponent = shooter.ValueRO;
            
                if (shootingComponent.ProjectilePrefab == Entity.Null)
                {
                    UnityEngine.Debug.LogError($"ProjectilePrefab is null for entity {entity}");
                    continue;
                }

                var bullet = ecb.Instantiate(shootingComponent.ProjectilePrefab);
            
                var bulletPosition = playerPosition + targetEnemy.Direction * shooter.ValueRO.SpawnOffset;
                var bulletRotation = quaternion.Euler(0, 0, math.atan2(targetEnemy.Direction.y, targetEnemy.Direction.x));
            
                ecb.SetComponent(bullet, new LocalTransform
                {
                    Position = bulletPosition,
                    Rotation = bulletRotation,
                    Scale = 1
                });
            
                ecb.SetComponent(bullet, new MovementComponent
                {
                    Direction = targetEnemy.Direction,
                    Speed = shootingComponent.ProjectileSpeed
                });

                // TODO: this system is horrible and highly coupled. We should find another approach
                ecb.SetComponent(bullet, new AreaDamage()
                {
                    Damage = shootingComponent.ProjectileDamage,
                    Radius = shootingComponent.ProjectileRadius
                });
            
                shooter.ValueRW.CooldownTimer = shootingComponent.Cooldown;
            }
        
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}