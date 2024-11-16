using DotsShooter.Player;
using DotsShooter.SpatialPartitioning;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Grid = DotsShooter.SpatialPartitioning.Grid;

namespace DotsShooter
{
    [UpdateBefore(typeof(ShootingSystem))]
    [UpdateAfter(typeof(UpdateGridSystem))]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TargetingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Grid>();
            state.RequireForUpdate<GridProperties>();
            state.RequireForUpdate<GridPropertiesInitialized>();
            
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<AutoTargetingPlayer>();
            state.RequireForUpdate<EnemyTag>();
        }

        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //TODO: This shouldn't be singleton. We should extend it to be reusable.
            var target = SystemAPI.GetSingletonRW<AutoTargetingPlayer>().ValueRW;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
            var grid = SystemAPI.GetSingleton<Grid>();
            target.Direction = float3.zero;

            var localToWorld = SystemAPI.GetComponentLookup<LocalToWorld>(true);

            // var delta = new float3(1, 0, 0);
            if (grid.FindClosestEntity(playerPosition, localToWorld, out var delta))
            {
                target.Direction = math.normalize(delta); 
                SystemAPI.SetSingleton(target);
            }
        }
    }
}