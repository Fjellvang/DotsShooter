using DotsShooter.Common;
using DotsShooter.Damage.AreaDamage;
using DotsShooter.Player;
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
            state.RequireForUpdate<AreaWeaponTag>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            foreach (var (weaponData, weaponState,  projectilePrefab,  parent, weaponActive, random) 
                     in SystemAPI.Query<RefRW<WeaponData>, RefRW<WeaponState>, RefRO<WeaponProjectilePrefab>, RefRO<Parent>, EnabledRefRW<WeaponActiveFlag>, RefRW<EntityRandom>>()
                         .WithAll<AreaWeaponTag>())
            {
                weaponState.ValueRW.NextAttackTimer -= deltaTime;
                if (weaponState.ValueRW.NextAttackTimer > 0) { continue; }
                
                var position = SystemAPI.GetComponent<LocalTransform>(parent.ValueRO.Value).Position;
                var statModification = SystemAPI.GetComponent<PlayerStatModifications>(parent.ValueRO.Value);
                var overlapHits = new NativeList<DistanceHit>(state.WorldUpdateAllocator);
                var closestDirection = Helpers.FindClosestDirection(physics, position, weaponData.ValueRO, statModification, overlapHits);

                if (closestDirection.Equals(Vector3.zero)) { continue; }
                closestDirection = WeaponDataExtensions.CalculateNewDirectionBasedOnAccuracy(weaponData, closestDirection, random);
                // spawn bullet.
                SpawnBullet(position, closestDirection, statModification, projectilePrefab.ValueRO, weaponData.ValueRO, ecb);

                weaponState.ValueRW.NextAttackTimer = weaponData.ValueRO.TimeBetweenShots;
                weaponState.ValueRW.AttackCounter++;
                var numberOfAttacks = weaponData.ValueRO.AttackCount + statModification.ExtraProjectiles; 
                if (weaponState.ValueRW.AttackCounter < numberOfAttacks) { continue; }
                
                weaponState.ValueRW.AttackCounter = 0;
                weaponActive.ValueRW = false;
            }
        }

        
        private static void SpawnBullet(float3 position, float3 direction, PlayerStatModifications statModifications,
            WeaponProjectilePrefab shootingComponent, WeaponData weaponData, EntityCommandBuffer ecb)
        {
            var bulletPosition = position + direction;
            var bulletRotation = quaternion.Euler(0, 0, math.atan2(direction.y, direction.x));
            var bullet = ecb.Instantiate(shootingComponent.Prefab);
            var transform = LocalTransform.FromPositionRotation(bulletPosition, bulletRotation);
            
            ecb.SetComponent(bullet, transform);
            ecb.SetComponent(bullet, new MovementDirectionComponent()
            {
                Direction = direction,
            });
            ecb.SetComponent(bullet, new AreaDamage()
            {
                Damage = weaponData.Damage * statModifications.DamageMultiplier,
                Radius = weaponData.AreaOfEffectRadius * statModifications.AreaOfEffectMultiplier,
                CollisionFilter = weaponData.CollisionFilter
            });
        }
    }
}