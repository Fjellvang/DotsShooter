using DotsShooter.Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace DotsShooter
{
    [UpdateBefore(typeof(ShootingSystem))]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TargetingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<AutoTargetingPlayer>();
            state.RequireForUpdate<EnemyTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //TODO: This shouldn't be singleton. We should extend it to be reusable.
            var target = SystemAPI.GetSingletonRW<AutoTargetingPlayer>().ValueRW;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

            var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var overlapHits = new NativeList<DistanceHit>(state.WorldUpdateAllocator);
            target.Direction = float3.zero;
            if (physics.OverlapSphere(playerPosition, target.Range, ref overlapHits, target.CollisionFilter))
            {
                var closestDistance = overlapHits[0].Distance;
                var closestHit = overlapHits[0];
                for (int i = 1; i < overlapHits.Length; i++)
                {
                    if (!(overlapHits[i].Distance < closestDistance)) continue;
                    
                    closestDistance = overlapHits[i].Distance;
                    closestHit = overlapHits[i];
                }
                target.Direction = math.normalize(closestHit.Position - playerPosition);
            }

            SystemAPI.SetSingleton(target);
        }
    }
}