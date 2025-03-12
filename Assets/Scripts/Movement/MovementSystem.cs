using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

namespace DotsShooter
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct MovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MovementComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            // Schedule the job
            new MovementJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct MovementJob : IJobEntity
    {
        public float DeltaTime;
        
        [BurstCompile]
        public void Execute(ref LocalTransform transform, in MovementComponent movementComponent)
        {
            var direction = movementComponent.Direction * movementComponent.Speed * DeltaTime;
            transform.Position += direction;
        }
    }
}