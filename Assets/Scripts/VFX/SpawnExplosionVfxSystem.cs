using DotsShooter.Common;
using DotsShooter.Damage.AreaDamage;
using DotsShooter.Destruction;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace DotsShooter.VFX
{
    [BurstCompile]
    [UpdateInGroup(typeof(EffectsSystemGroup))]
    public partial struct SpawnExplosionVfxSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SpawnExplosionVfx>();

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var explosionSpawner = SystemAPI.GetSingleton<SpawnExplosionVfx>();
            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            var parallelEcb = ecb.AsParallelWriter();    
            new Job()
                {
                    ExplosionSpawner = explosionSpawner,
                    CommandBuffer = parallelEcb,
                }
                .ScheduleParallel();
        }
    }
    
    [WithAll(typeof(DestroyNextFrameTag))]
    public partial struct Job : IJobEntity
    {
        public SpawnExplosionVfx ExplosionSpawner;
        public EntityCommandBuffer.ParallelWriter  CommandBuffer;
        public void Execute(
            [EntityIndexInQuery] int sortKey, 
            ref LocalTransform transform, 
            ref AreaDamage damage
            )
        {
            var radius = damage.Radius;
            var explosionVfx = CommandBuffer.Instantiate(sortKey, ExplosionSpawner.Prefab);
            var local = LocalTransform.FromPositionRotationScale(transform.Position, Quaternion.identity, radius * 2);
            CommandBuffer.SetComponent(sortKey, explosionVfx, local); 
        }
    }
}