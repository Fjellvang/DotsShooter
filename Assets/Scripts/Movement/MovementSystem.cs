using DotsShooter.Player;
using Unity.Burst;
using Unity.Entities;
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
            state.RequireForUpdate<MovementSpeedComponent>();
            state.RequireForUpdate<MovementDirectionComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MovementJob().ScheduleParallel();
            new MovementJobWithModifications().ScheduleParallel();
        }
    }

    [BurstCompile]
    [WithNone(typeof(PlayerTag))]
    public partial struct MovementJob : IJobEntity
    {
        [BurstCompile]
        public void Execute(in MovementDirectionComponent movementComponent, in MovementSpeedComponent speedComponent, ref PhysicsVelocity physicsVelocity)
        {
            var direction = movementComponent.Direction * speedComponent.Speed;
            physicsVelocity.Linear = direction;
        }
    }
    
    [BurstCompile]
    public partial struct MovementJobWithModifications : IJobEntity
    {
        [BurstCompile]
        public void Execute(in MovementDirectionComponent movementComponent,
            in MovementSpeedComponent speedComponent,
            in PlayerStatModifications modifications,
            ref PhysicsVelocity physicsVelocity)
        {
            var direction = movementComponent.Direction * speedComponent.Speed * modifications.MoveSpeedMultiplier;
            physicsVelocity.Linear = direction;
        }
    }
}