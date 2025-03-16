using DotsShooter;
using DotsShooter.Common;
using DotsShooter.Destruction;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Rendering
{
    [RequireComponent(typeof(LifeTimeComponent))]
    public class DissolveOnDestructionFlagAuthoring : MonoBehaviour
    {
        public class DissolveOnDestructionFlagBaker : Baker<DissolveOnDestructionFlagAuthoring>
        {
            public override void Bake(DissolveOnDestructionFlagAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<DissolveOnDestructionFlag>(entity);
            }
        }
    }

    public partial struct DissolveOnDestructionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifeTime, dissolveFloatOverride) in SystemAPI
                         .Query<RefRO<LifeTimeComponent>, RefRW<DissolveFloatOverride>>())
            {
                dissolveFloatOverride.ValueRW.Value = (1 - lifeTime.ValueRO.TimeToDeathNormalized);
            }
        }
    }
    
    [UpdateInGroup(typeof(EffectsSystemGroup))]
    public partial struct SpawnDissolveAnimationOnDestructionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);  
            foreach (var (graphics, transform) in SystemAPI
                         .Query<RefRO<GraphicsEntityData>, RefRO<LocalTransform>>()
                         .WithAll<DestroyNextFrameTag>()
                     )
            {
                var entity = graphics.ValueRO.Entity;
                if(!SystemAPI.HasComponent<DissolveOnDestructionFlag>(entity)) continue;
                
                var dissolveEntity = ecb.Instantiate(graphics.ValueRO.Entity);
                var originalTransform = SystemAPI.GetComponentRO<LocalTransform>(graphics.ValueRO.Entity); // TODO: FIX VIA QUAD
                var lifeTime = SystemAPI.GetComponentRO<LifeTimeComponent>(entity);
                var spawnedtransform = LocalTransform
                    .FromPositionRotationScale(
                        transform.ValueRO.Position, 
                        originalTransform.ValueRO.Rotation, 
                        originalTransform.ValueRO.Scale);
                ecb.SetComponent(dissolveEntity,  spawnedtransform);
                ecb.SetComponent(dissolveEntity, new LifeTimeComponent()
                {
                    LifeTime = lifeTime.ValueRO.LifeTime,
                    OriginalLifeTime = lifeTime.ValueRO.OriginalLifeTime
                });
                ecb.SetComponentEnabled<LifeTimeComponent>(dissolveEntity, true);
            }
        }
    }
}