using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsShooter
{
    public partial struct RotateTowardsSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (movingObject, transform, rotateTowardsComponent) in 
                     SystemAPI.Query<RefRO<MovementComponent>, RefRW<LocalTransform>, RefRO<RotateTowardsComponent>>())
            {
                var direction = movingObject.ValueRO.Direction;
                if (direction.Equals(float3.zero))
                {
                    continue;
                }

                var offset = math.TORADIANS * rotateTowardsComponent.ValueRO.RotationOffset;
                var angle = math.atan2(direction.y, direction.x);
                var targetRotation = quaternion.Euler(0, 0, angle + offset);
                var speed = rotateTowardsComponent.ValueRO.RotationSpeed;
                var rotation = math.slerp(transform.ValueRO.Rotation, targetRotation, deltaTime * speed);
                // TODO: Maybe smoooth it?
                transform.ValueRW.Rotation = rotation;
            }
        }
    }
}