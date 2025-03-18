using DotsShooter.Common;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Destruction
{
    [UpdateInGroup(typeof(AttackSystemGroup))] // run in attack group to ensure effects trigger
    public partial struct DestroyAllEnemiesSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<GameEndedFlag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<EnemyTag>>().WithEntityAccess())
            {
                ecb.SetComponentEnabled<DestroyNextFrameTag>(entity, true);
            }
        }
    }
}