using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics;

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
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct MovementJob : IJobEntity
    {
        [BurstCompile]
        public void Execute(in MovementComponent movementComponent, ref PhysicsVelocity physicsVelocity)
        {
            var direction = movementComponent.Direction * movementComponent.Speed;
            physicsVelocity.Linear = direction;
        }
    }
}