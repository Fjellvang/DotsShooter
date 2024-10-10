using System;
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
            
            while (deadEntities.TryDequeue(out var deadEntity))
            {
                Helpers.DestroyEntityHierarchy(deadEntity.Entity, ref ecb, ref childBufferFromEntity);
                switch (deadEntity.EntityType)
                {
                    case EntityType.Bullet:
                        break;
                    case EntityType.Enemy:
                        break;
                    case EntityType.Player:
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