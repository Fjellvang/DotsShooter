using DotsShooter.Common;
using DotsShooter.Destruction;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace DotsShooter
{
    [UpdateInGroup(typeof(EffectsSystemGroup))]
    public partial struct SpawnOnDeathSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged); 
            foreach (var (spawnEnemyOnDeath, transform) in 
                     SystemAPI.Query<RefRO<SpawnOnDeathComponent>, RefRO<LocalTransform>>()
                         .WithNone<DisableSpawnOnDeathFlag>()
                         .WithAll<DestroyNextFrameTag>())
            {
                var enemy = ecb.Instantiate(spawnEnemyOnDeath.ValueRO.Prefab);
                ecb.SetComponent(enemy, LocalTransform.FromPosition(transform.ValueRO.Position));
            }
        }
    }
}