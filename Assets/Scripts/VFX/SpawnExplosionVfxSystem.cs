using DotsShooter.Damage.AreaDamage;
using DotsShooter.Destruction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.UI;

namespace DotsShooter.VFX
{
    [BurstCompile]
    public partial struct SpawnExplosionVfxSystem : ISystem
    {
        [BurstCompile]
        void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SpawnExplosionVfx>();

        }

        [BurstCompile]
        void OnUpdate(ref SystemState state)
        {
            var explosionSpawner = SystemAPI.GetSingleton<SpawnExplosionVfx>();
            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var prefabRotation = SystemAPI.GetComponentRO<LocalTransform>(explosionSpawner.Prefab).ValueRO.Rotation;
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            var parallelEcb = ecb.AsParallelWriter();    
            new Job()
                {
                    ExplosionSpawner = explosionSpawner,
                    CommandBuffer = parallelEcb,
                    PrefabRotation = prefabRotation
                }
                .ScheduleParallel();
        }
    }
    
    [WithAll(typeof(DestroyNextFrame))]
    public partial struct Job : IJobEntity
    {
        public SpawnExplosionVfx ExplosionSpawner;
        public EntityCommandBuffer.ParallelWriter  CommandBuffer;
        public quaternion PrefabRotation;    
        public void Execute(
            [EntityIndexInQuery] int sortKey, 
            ref LocalTransform transform, 
            ref AreaDamage damage
            )
        {
            var radius = damage.Radius;
            var explosionVfx = CommandBuffer.Instantiate(sortKey, ExplosionSpawner.Prefab);
            // Get the existing rotation from the prefab
            var local = LocalTransform.FromPositionRotation(transform.Position, PrefabRotation);
            // local.Rotation = existing.ValueRO.Rotation;
            CommandBuffer.SetComponent(sortKey, explosionVfx, new RadiusFloatOverride { Value = radius });
            CommandBuffer.SetComponent(sortKey, explosionVfx, local); 
        }
    }
}