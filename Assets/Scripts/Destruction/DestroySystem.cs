using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace DotsShooter.Destruction
{
    public partial struct DestroySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var childBufferFromEntity = SystemAPI.GetBufferLookup<Child>(true);
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            
            foreach (var (_, entity) in SystemAPI.Query<RefRO<DestroyNextFrame>>().WithEntityAccess())
            {
                Helpers.DestroyEntityHierarchy(entity, ref ecb, ref childBufferFromEntity);
            }
            
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<MarkedForDestruction>>()
                         .WithNone<DestroyNextFrame>()
                         .WithEntityAccess())
            {
                state.EntityManager.SetComponentEnabled<DestroyNextFrame>(entity, true);
                // Is this redundant? 
                state.EntityManager.SetComponentEnabled<MarkedForDestruction>(entity, false);
            }
        }
    }
}