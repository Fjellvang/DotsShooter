using DotsShooter.Damage;
using DotsShooter.Damage.AreaDamage;
using DotsShooter.Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DotsShooter
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TargetingSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ShootingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<AutoTargetingPlayer>();
            state.RequireForUpdate<EnemyTag>();
        }        
        
        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var targetEnemy = SystemAPI.GetSingletonRW<AutoTargetingPlayer>().ValueRO;
        
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged); 
            foreach (var (shooter, transform, entity) in 
                     SystemAPI.Query<RefRW<AutoShootingComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (targetEnemy.Direction.Equals(float3.zero) || math.isnan(targetEnemy.Direction.x))
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

                var direction = targetEnemy.Direction;
                var position = transform.ValueRO.Position; 
                var offset = shootingComponent.SpawnOffset;
                if (shootingComponent.BulletCount <= 1)
                {
                    SpawnBullet(position, direction, offset, ecb, shootingComponent);
                }
                else
                {
                    var bulletCount = shootingComponent.BulletCount;
                    // Calculate the spread angle based on bullet count
                    // More bullets = wider spread
                    var totalSpreadAngle = math.TORADIANS * 90 * (bulletCount - 1);
                    // float totalSpreadAngle = math.min(shootingComponent.MaxSpreadAngle, 
                    //                                  shootingComponent.BaseSpreadAngle * (bulletCount - 1));
                    float angleStep = totalSpreadAngle / (bulletCount - 1);
            
                    // Calculate perpendicular vector for creating the spread
            
                    // Spawn bullets with appropriate spread
                    for (int i = 0; i < bulletCount; i++)
                    {
                        // Calculate the angle for this bullet
                        float currentAngle;
                        if (bulletCount == 1)
                        {
                            currentAngle = 0; // No spread for single bullet
                        }
                        else
                        {
                            // Convert from [0..bulletCount-1] to [-spreadAngle/2..+spreadAngle/2]
                            currentAngle = (i * angleStep) - (totalSpreadAngle / 2);
                        }
                
                        // Convert the angle to radians
                        var angleRad = math.radians(currentAngle);
                        
                        // Rotate the 2D direction vector
                        var cosAngle = math.cos(angleRad);
                        var sinAngle = math.sin(angleRad);
                        var spreadDirection = new float3(
                            direction.x * cosAngle - direction.y * sinAngle,
                            direction.x * sinAngle + direction.y * cosAngle,
                            0
                        );
                    
                    
                
                        // Spawn the bullet with the calculated direction
                        SpawnBullet(position, spreadDirection, offset, ecb, shootingComponent);
                    }
                }
                shooter.ValueRW.CooldownTimer = shootingComponent.Cooldown;
            }
        }

        private static void SpawnBullet(float3 position, float3 direction, float spawnOffset, EntityCommandBuffer ecb,
            AutoShootingComponent shootingComponent)
        {
            var bulletPosition = position + direction * spawnOffset;
            var bulletRotation = quaternion.Euler(0, 0, math.atan2(direction.y, direction.x));
            var bullet = ecb.Instantiate(shootingComponent.ProjectilePrefab);
            ecb.SetComponent(bullet, new LocalTransform
            {
                Position = bulletPosition,
                Rotation = bulletRotation,
                Scale = 1
            });
            
            ecb.SetComponent(bullet, new MovementComponent
            {
                Direction = direction,
                Speed = shootingComponent.ProjectileSpeed
            });

            // TODO: this system is horrible and highly coupled. We should find another approach
            ecb.SetComponent(bullet, new AreaDamage()
            {
                Damage = shootingComponent.ProjectileDamage,
                Radius = shootingComponent.ProjectileRadius
            });
        }
    }
}