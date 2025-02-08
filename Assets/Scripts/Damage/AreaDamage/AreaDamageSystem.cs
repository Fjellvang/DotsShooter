using DotsShooter.Destruction;
using DotsShooter.Health;
using DotsShooter.SimpleCollision;
using DotsShooter.SpatialPartitioning;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Grid = DotsShooter.SpatialPartitioning.Grid;

namespace DotsShooter.Damage.AreaDamage
{
    [BurstCompile]
    [UpdateAfter(typeof(SimpleCollisionSystem))]
    [UpdateBefore(typeof(HealthSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct AreaDamageSystem : ISystem
    {
       private BufferLookup<DamageData> _bufferLookup;
       private ComponentLookup<LocalToWorld> _localToWorldLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnExplosionVfx>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<Grid>();
            state.RequireForUpdate<GridProperties>();
            state.RequireForUpdate<GridPropertiesInitialized>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _bufferLookup = state.GetBufferLookup<DamageData>();
            _localToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _localToWorldLookup.Update(ref state);
            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged); 
            var localToWorld = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var markedForDestructionLookup = SystemAPI.GetComponentLookup<MarkedForDestruction>();
            _bufferLookup.Update(ref state);
            var explosionSpawner = SystemAPI.GetSingleton<SpawnExplosionVfx>();
            var grid = SystemAPI.GetSingleton<Grid>();
            foreach (var (damage, localTransform, entity) in SystemAPI
                         .Query<RefRO<AreaDamage>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<SimpleCollisionEvent>(entity))
                {
                    continue;
                }

                // var vfx = SystemAPI.GetSharedComponentTypeHandle<AreaDamageVfx>();
                // var test = SystemAPI.GetComponent<AreaDamageVfx>(entity);
                var simpleCollisionBuffer = state.EntityManager.GetBuffer<SimpleCollisionEvent>(entity);
                for (int i = 0; i < simpleCollisionBuffer.Length; i++)
                {
                    var location = localTransform.ValueRO.Position;
                    SpawnVFX(ref state, ecb, explosionSpawner.Prefab, location, damage.ValueRO.Radius);
                    var entities = grid.GetEntitiesInRadius(location, damage.ValueRO.Radius, localToWorld);
                    for(int j = 0; j < entities.Length; j++)
                    {
                        var other = entities[j];
                        if (_bufferLookup.HasBuffer(other))
                        {
                            _bufferLookup[other].Add(new DamageData() { Damage = damage.ValueRO.Damage });
                        }
                    }
                    // Destroy the bullet
                    markedForDestructionLookup.SetComponentEnabled(entity, true);
                }
            }
        }

        private void SpawnVFX(ref SystemState state, EntityCommandBuffer ecb, in Entity vfxPrefab, in float3 location, in float radius)
        {
            var explosionVfx = ecb.Instantiate(vfxPrefab);
            var existing = SystemAPI.GetComponentRO<LocalTransform>(vfxPrefab);
            var local= LocalTransform.FromPosition(location);
            local.Rotation = existing.ValueRO.Rotation;
            ecb.SetComponent(explosionVfx, new RadiusFloatOverride { Value = radius });
            ecb.SetComponent(explosionVfx, local);
        }
    }
}