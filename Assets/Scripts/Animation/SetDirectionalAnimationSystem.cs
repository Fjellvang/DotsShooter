using DotsShooter;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Rendering
{
    [BurstCompile]
    public partial struct SetDirectionalAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (yDirectionOverride, parent, isFlipped, localTransform) 
                     in SystemAPI.Query<RefRW<yDirectionFloatOverride>, RefRO<Parent>, RefRW<IsFlipped>, RefRW<LocalTransform>>())
            {
                if (!SystemAPI.HasComponent<MovementComponent>(parent.ValueRO.Value)) continue;
                
                var movementComponent = SystemAPI.GetComponent<MovementComponent>(parent.ValueRO.Value);

                if (movementComponent.Direction.Equals(float3.zero))
                {
                    continue;
                }
                
                yDirectionOverride.ValueRW.Value = movementComponent.Direction.y;

                if (movementComponent.Direction.x < 0 && !isFlipped.ValueRO.Flipped)
                {
                    isFlipped.ValueRW.Flipped = true;
                    localTransform.ValueRW.Rotation = math.mul(localTransform.ValueRO.Rotation, quaternion.RotateZ(math.PI));
                }
                else if (movementComponent.Direction.x > 0 && isFlipped.ValueRO.Flipped)
                {
                    isFlipped.ValueRW.Flipped = false;
                    localTransform.ValueRW.Rotation = math.mul(localTransform.ValueRO.Rotation, quaternion.RotateZ(math.PI));                
                }
            }
        }
    }
}