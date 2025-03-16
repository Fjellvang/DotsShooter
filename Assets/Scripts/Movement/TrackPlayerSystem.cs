using DotsShooter.Player;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

namespace DotsShooter
{
    [UpdateBefore(typeof(MovementSystem))]
    [BurstCompile]
    public partial struct TrackPlayerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<TrackPlayerComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

            // Schedule the job with captured player position
            new TrackPlayerJob
            {
                PlayerPosition = playerPosition
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct TrackPlayerJob : IJobEntity
    {
        [ReadOnly] 
        public float3 PlayerPosition;

        [BurstCompile]
        public void Execute(ref MovementDirectionComponent movement, in LocalTransform transform, in TrackPlayerComponent trackPlayer)
        {
            var direction = PlayerPosition - transform.Position;
            var distance = math.length(direction);
            
            if (distance > 0.1f)
            {
                direction /= distance;
                movement.Direction = direction;
            }
            else
            {
                movement.Direction = float3.zero;
            }
        }
    }
}