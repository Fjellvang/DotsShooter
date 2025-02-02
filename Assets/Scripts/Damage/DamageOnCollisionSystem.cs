using DotsShooter.SimpleCollision;
using DotsShooter.SpatialPartitioning;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;
using Grid = DotsShooter.SpatialPartitioning.Grid;

namespace DotsShooter.Damage
{
    [BurstCompile]
    [UpdateAfter(typeof(SimpleCollisionSystem))]
    public partial struct DamageOnCollisionSystem : ISystem
    {
        private BufferLookup<DamageData> _bufferLookup;
        // private EntityQuery _damageQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Grid>();
            state.RequireForUpdate<GridProperties>();
            state.RequireForUpdate<GridPropertiesInitialized>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _bufferLookup = state.GetBufferLookup<DamageData>();

            var builder = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<DamageOnCollision>()
                    .WithAll<SimpleCollisionEvent>() // Add this line
                ;

            // _damageQuery = state.GetEntityQuery(builder);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _bufferLookup.Update(ref state);

            var deadEntities = SystemAPI.GetSingletonRW<DeadEntities>();
            var parallelWriter = deadEntities.ValueRW.Value.AsParallelWriter();
            var localToWorld = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var grid = SystemAPI.GetSingleton<Grid>();

            // var handleDamageJob = new HandleDamageJob()
            // {
            //     ECB = ecb.AsParallelWriter(),
            //     BufferLookup = SystemAPI.GetBufferLookup<DamageData>()//_bufferLookup
            // };
            //
            // state.Dependency = handleDamageJob.ScheduleParallel(
            //     _damageQuery,
            //     state.Dependency
            // );
            // var childBufferFromEntity = SystemAPI.GetBufferLookup<Child>(true); 
            foreach (var (damage, localTransform, entity) in SystemAPI.Query<RefRW<DamageOnCollision>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<SimpleCollisionEvent>(entity))
                {
                    continue;
                }

                var simpleCollisionBuffer = state.EntityManager.GetBuffer<SimpleCollisionEvent>(entity);

                for (int i = 0; i < simpleCollisionBuffer.Length; i++)
                {
                    // var simpleCollisionEvent = simpleCollisionBuffer[i];
                    // var other = simpleCollisionEvent.GetOtherEntity(entity);
                    var entities = grid.GetEntitiesInRadius(localTransform.ValueRO.Position, damage.ValueRO.Radius, ref localToWorld);
                    Debug.Log($"Entities in radius: {entities.Length}, radius: {damage.ValueRO.Radius}, damage: {damage.ValueRO.Damage}");
                    foreach (var other in entities)
                    {
                        if (_bufferLookup.HasBuffer(other)) {
                            _bufferLookup[other].Add(new DamageData() { Damage = damage.ValueRO.Damage });
                        }
                    }

                    if (damage.ValueRO.DestroyOnCollision)
                    {
                        parallelWriter.Enqueue(new DeadEntity() { Entity = entity, EntityType = EntityType.Bullet });
                    }
                }
            }
        }


        [BurstCompile]
        public partial struct HandleDamageJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            [ReadOnly, NativeDisableParallelForRestriction]
            public BufferLookup<DamageData> BufferLookup;

            void Execute(
                Entity entity,
                [ReadOnly] in DamageOnCollision damage,
                [ReadOnly] in DynamicBuffer<SimpleCollisionEvent> collisionBuffer,
                [ChunkIndexInQuery] int sortKey)
            {
                for (int i = 0; i < collisionBuffer.Length; i++)
                {
                    var simpleCollisionEvent = collisionBuffer[i];
                    var other = simpleCollisionEvent.GetOtherEntity(entity);

                    if (BufferLookup.HasBuffer(other))
                    {
                        ECB.AppendToBuffer(sortKey, other, new DamageData() { Damage = damage.Damage });
                    }

                    if (damage.DestroyOnCollision)
                    {
                        ECB.DestroyEntity(sortKey, entity);
                        break;
                    }
                }
            }
        }
    }
}