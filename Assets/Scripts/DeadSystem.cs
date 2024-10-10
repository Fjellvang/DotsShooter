using System;
using DotsShooter.Events;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace DotsShooter
{
    public struct DeadEntities : IComponentData
    {
        public NativeQueue<DeadEntity> Value;
    }

    public struct DeadEntity
    {
        public EntityType EntityType;
        public Entity Entity;
    }


    public enum EntityType
    {
        Bullet,
        Enemy,
        Player
    }
    public partial struct DeadSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<DeadEntities>();
            
            var deadEntities = new NativeQueue<DeadEntity>(Allocator.Persistent);
            state.EntityManager.AddComponentData(state.SystemHandle, new DeadEntities(){Value = deadEntities});
        } 
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deadEntities = SystemAPI.GetSingletonRW<DeadEntities>().ValueRW.Value;
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            var childBufferFromEntity = SystemAPI.GetBufferLookup<Child>(true);
            
            var events = SystemAPI.GetSingletonRW<EventQueue>();
            var parallelWriter = events.ValueRW.Value.AsParallelWriter();
            
            while (deadEntities.TryDequeue(out var deadEntity))
            {
                Helpers.DestroyEntityHierarchy(deadEntity.Entity, ref ecb, ref childBufferFromEntity);
                switch (deadEntity.EntityType)
                {
                    case EntityType.Bullet:
                        parallelWriter.Enqueue(new Event(){EntityType = EventType.BulletDied});
                        break;
                    case EntityType.Enemy:
                        parallelWriter.Enqueue(new Event(){EntityType = EventType.EnemyDied});
                        break;
                    case EntityType.Player:
                        parallelWriter.Enqueue(new Event()
                        {
                            EntityType = EventType.PlayerDied,
                            Location = SystemAPI.GetComponent<LocalTransform>(deadEntity.Entity).Position
                        });
                        break;
                }
            }
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            var deadEntities = SystemAPI.GetSingletonRW<DeadEntities>();
            deadEntities.ValueRW.Value.Dispose();
        }
    }
}