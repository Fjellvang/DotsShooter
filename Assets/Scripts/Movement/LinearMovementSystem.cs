using DotsShooter.Player;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace DotsShooter
{
    [UpdateBefore(typeof(MovementSystem))]
    [BurstCompile]
    public partial struct LinearMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<MoveTowardsPlayerFlag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new LinearMovementJob // TODO: in theory this should only be initialized once.
            {
            }.ScheduleParallel();
        }
    }
    
    [BurstCompile]
    public partial struct LinearMovementJob : IJobEntity
    {
        [BurstCompile]
        public void Execute(ref MovementDirectionComponent movement, in LinearMovementComponent linearMovement)
        {
            movement.Direction = new float3(math.cos(linearMovement.Angle), math.sin(linearMovement.Angle), 0);
        }
    }
}