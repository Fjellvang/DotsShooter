using DotsShooter.Damage.AreaDamage;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace DotsShooter.Weapons.Area
{
    [RequireComponent(typeof(WeaponComponentAuthoring))]
    public class AreaWeaponAuthoring : MonoBehaviour
    {
        public class AreaWeaponBaker : Baker<AreaWeaponAuthoring>
        {
            public override void Bake(AreaWeaponAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new AreaWeaponTag() );
            }
        }
    }
    
    public struct AreaWeaponTag : IComponentData
    {
    }

    public partial struct AreaWeaponSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (weaponData, weaponState, transform, projectilePrefab) 
                     in SystemAPI.Query<RefRW<WeaponData>, RefRW<WeaponState>, RefRO<LocalTransform>, RefRO<WeaponProjectilePrefab>>()
                         .WithAll<AreaWeaponTag>())
                
            {
                weaponState.ValueRW.NextAttackTimer -= deltaTime;
                if (weaponState.ValueRW.NextAttackTimer > 0) continue;
                
                var position = transform.ValueRO.Position;
                var overlapHits = new NativeList<DistanceHit>(state.WorldUpdateAllocator);
                var closestDirection = FindClosestDirection(physics, position, weaponData.ValueRO, overlapHits);
                
                // spawn bullet.
                // set next attacktimer if more than 0
            }
        }

        private static float3 FindClosestDirection(PhysicsWorldSingleton physics, in float3 position, in WeaponData weaponData,
            NativeList<DistanceHit> overlapHits)
        {
            var closest = float3.zero;
            if (physics.OverlapSphere(position, weaponData.Range, ref overlapHits, weaponData.CollisionFilter))
            {
                var closestDistance = overlapHits[0].Distance;
                var closestHit = overlapHits[0];
                for (int i = 1; i < overlapHits.Length; i++)
                {
                    if (!(overlapHits[i].Distance < closestDistance)) continue;
                    
                    closestDistance = overlapHits[i].Distance;
                    closestHit = overlapHits[i];
                }
                closest = math.normalize(closestHit.Position - position);
            }

            return closest;
        }
        
        private static void SpawnBullet(float3 position, float3 direction, float spawnOffset, EntityCommandBuffer ecb,
            AutoShootingComponent shootingComponent, ComponentLookup<AreaDamage> areaDamageLookup)
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

            var collisionFilter = areaDamageLookup.GetRefRO(shootingComponent.ProjectilePrefab).ValueRO.CollisionFilter;
            
            // TODO: this system is horrible and highly coupled. We should find another approach
            ecb.SetComponent(bullet, new AreaDamage()
            {
                Damage = shootingComponent.ProjectileDamage,
                Radius = shootingComponent.ProjectileRadius,
                CollisionFilter = collisionFilter,
            });
        }
    }
}