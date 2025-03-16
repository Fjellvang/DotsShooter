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
            foreach (var (weaponData, weaponState,  projectilePrefab, parent, weaponActive) 
                     in SystemAPI.Query<RefRW<WeaponData>, RefRW<WeaponState>, RefRO<WeaponProjectilePrefab>, RefRO<Parent>, EnabledRefRW<WeaponActiveFlag>>()
                         .WithAll<AreaWeaponTag>())
            {
                weaponState.ValueRW.NextAttackTimer -= deltaTime;
                if (weaponState.ValueRW.NextAttackTimer > 0) { continue; }
                
                var position = SystemAPI.GetComponent<LocalTransform>(parent.ValueRO.Value).Position;
                var overlapHits = new NativeList<DistanceHit>(state.WorldUpdateAllocator);
                var closestDirection = Helpers.FindClosestDirection(physics, position, weaponData.ValueRO, overlapHits);

                if (closestDirection.Equals(Vector3.zero)) { continue; }
                // spawn bullet.
                SpawnBullet(position, closestDirection, 0, projectilePrefab.ValueRO, weaponData.ValueRO, ecb);

                weaponState.ValueRW.NextAttackTimer = weaponData.ValueRO.TimeBetweenShots;
                weaponState.ValueRW.AttackCounter++;
                var numberOfAttacks = weaponData.ValueRO.AttackCount; // TODO: Make this effected by player stats
                if (weaponState.ValueRW.AttackCounter < numberOfAttacks) { continue; }
                
                weaponState.ValueRW.AttackCounter = 0;
                weaponActive.ValueRW = false;
            }
        }

        
        private static void SpawnBullet(float3 position, float3 direction, float spawnOffset,
            WeaponProjectilePrefab shootingComponent, WeaponData weaponData, EntityCommandBuffer ecb)
        {
            var bulletPosition = position + direction * spawnOffset;
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
                Damage = weaponData.Damage,
                Radius = weaponData.AreaOfEffectRadius,
                CollisionFilter = weaponData.CollisionFilter
            });
        }
    }
}