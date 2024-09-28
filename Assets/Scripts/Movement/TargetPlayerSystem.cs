using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsShooter
{
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct TargetPlayerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
            foreach (var (movement, transform) in 
                     SystemAPI.Query<RefRW<MovementComponent>, RefRO<LocalTransform>>()
                         .WithAll<TargetPlayerComponent>())
            {
                var direction = playerPosition - transform.ValueRO.Position;
                var distance = math.length(direction);
                if (distance > 0.1f)
                {
                    direction /= distance;
                    movement.ValueRW.Direction = direction;
                }
                else
                {
                    movement.ValueRW.Direction = float3.zero;
                }
            }
        } 
    }
}