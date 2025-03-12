using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsShooter
{
    [BurstCompile]
    public partial struct RotateTowardsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MovementComponent>();
            state.RequireForUpdate<RotateTowardsComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            // Schedule the job
            new RotateTowardsJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct RotateTowardsJob : IJobEntity
    {
        public float DeltaTime;
        
        [BurstCompile]
        public void Execute(in MovementComponent movementComponent, ref LocalTransform transform, in RotateTowardsComponent rotateTowardsComponent)
        {
            var direction = movementComponent.Direction;
            if (math.all(direction == float3.zero))
            {
                return;
            }

            var offset = math.TORADIANS * rotateTowardsComponent.RotationOffset;
            var angle = math.atan2(direction.y, direction.x);
            var targetRotation = quaternion.Euler(0, 0, angle + offset);
            var speed = rotateTowardsComponent.RotationSpeed;
            var rotation = math.slerp(transform.Rotation, targetRotation, DeltaTime * speed);
            transform.Rotation = rotation;
        }
    }
}