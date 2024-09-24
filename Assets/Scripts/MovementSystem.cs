using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace DotsShooter
{
    public partial struct MovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {

            foreach (var (movingObject, transform) in 
                     SystemAPI.Query<RefRO<MovementComponent>, RefRW<LocalTransform>>())
            {
                var direction =  movingObject.ValueRO.Direction * movingObject.ValueRO.Speed * SystemAPI.Time.DeltaTime;

                transform.ValueRW.Position += direction;
            }
        }
    }
}