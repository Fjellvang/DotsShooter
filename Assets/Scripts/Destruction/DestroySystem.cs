using DotsShooter.Common;
using Unity.Entities;
using Unity.Transforms;

namespace DotsShooter.Destruction
{
    [UpdateInGroup(typeof(DestructionSystemGroup), OrderLast = true)]
    public partial struct DestroySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var childBufferFromEntity = SystemAPI.GetBufferLookup<Child>(true);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            
            foreach (var (_, entity) in SystemAPI.Query<RefRO<DestroyNextFrameTag>>().WithEntityAccess())
            {
                Helpers.DestroyEntityHierarchy(entity, ref ecb, ref childBufferFromEntity);
            }
        }
    }
}