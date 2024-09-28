using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace DotsShooter
{
    public partial struct PlayerMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (playerInput, movement) in 
                     SystemAPI.Query<RefRO<PlayerInput>, RefRW<MovementComponent>>())
            {
                movement.ValueRW.Direction = new float3(playerInput.ValueRO.Move.x, playerInput.ValueRO.Move.y, 0);
            }
        }
    }
}