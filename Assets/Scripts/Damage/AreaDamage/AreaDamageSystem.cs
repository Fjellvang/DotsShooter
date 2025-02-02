using DotsShooter.Health;
using DotsShooter.SimpleCollision;
using DotsShooter.SpatialPartitioning;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
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
            var localToWorld = SystemAPI.GetComponentLookup<LocalToWorld>(true);

            _bufferLookup.Update(ref state);

            var deadEntities = SystemAPI.GetSingletonRW<DeadEntities>();
            var grid = SystemAPI.GetSingleton<Grid>();
            var parallelWriter = deadEntities.ValueRW.Value.AsParallelWriter();
            foreach (var (damage, localTransform, entity) in SystemAPI.Query<RefRO<AreaDamage>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<SimpleCollisionEvent>(entity))
                {
                    continue;
                }

                var simpleCollisionBuffer = state.EntityManager.GetBuffer<SimpleCollisionEvent>(entity);

                for (int i = 0; i < simpleCollisionBuffer.Length; i++)
                {
                    var entities = grid.GetEntitiesInRadius(localTransform.ValueRO.Position, damage.ValueRO.Radius, localToWorld);
                    for(int j = 0; j < entities.Length; j++)
                    {
                        var other = entities[j];
                        if (_bufferLookup.HasBuffer(other))
                        {
                            _bufferLookup[other].Add(new DamageData() { Damage = damage.ValueRO.Damage });
                        }
                    }
                    // Destroy the bullet
                    parallelWriter.Enqueue(new DeadEntity() { Entity = entity, EntityType = EntityType.Bullet });
                }
            }
        }
    }
}