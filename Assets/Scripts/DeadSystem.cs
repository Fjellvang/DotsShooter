using System;
using DotsShooter.Events;
using DotsShooter.Pickup;
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
        Player,
        XpPickup
    }
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
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
            
            //TODO: this is getting crowded. consider a separate system for each entity type
            var xpQueueParallelWriter = SystemAPI.GetSingletonRW<SpawnXpPickups>().ValueRW.Locations.AsParallelWriter();
            var events = SystemAPI.GetSingletonRW<EventQueue>();
            var parallelWriter = events.ValueRW.Value.AsParallelWriter();
            var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            
            while (deadEntities.TryDequeue(out var deadEntity))
            {
                switch (deadEntity.EntityType)
                {
                    case EntityType.XpPickup:
                        parallelWriter.Enqueue(new Event(){EventType = EventType.XpPickup});
                        break;
                    case EntityType.Bullet:
                        parallelWriter.Enqueue(new Event(){EventType = EventType.BulletDied});
                        break;
                    case EntityType.Enemy:
                        parallelWriter.Enqueue(new Event(){EventType = EventType.EnemyDied});
                        if (localTransformLookup.HasComponent(deadEntity.Entity))
                        {
                            var position = localTransformLookup.GetRefRO(deadEntity.Entity).ValueRO.Position;
                            xpQueueParallelWriter.Enqueue(position);
                        }
                            //SystemAPI.GetComponent<LocalTransform>(deadEntity.Entity).Position;
                            //localTransformLookup.GetRefRO(deadEntity.Entity).ValueRO.Position;
                        break;
                    case EntityType.Player:
                        parallelWriter.Enqueue(new Event()
                        {
                            EventType = EventType.PlayerDied,
                            Location = localTransformLookup.GetRefRO(deadEntity.Entity).ValueRO.Position
                                // SystemAPI.GetComponent<LocalTransform>(deadEntity.Entity).Position
                        });
                        break;
                }
                
                Helpers.DestroyEntityHierarchy(deadEntity.Entity, ref ecb, ref childBufferFromEntity);
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