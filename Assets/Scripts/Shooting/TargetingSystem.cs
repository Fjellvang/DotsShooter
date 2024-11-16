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
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
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

            if (grid.FindClosestEntity(playerPosition, localToWorld, out var delta))
            {
                target.Direction = math.normalize(delta); 
                SystemAPI.SetSingleton(target);
            }

            
            // // TODO: This is the naive implementation, we can optimize this by using a quadtree or a spatial hash, or maybe just a simple grid
            // // The simple grid would be the easiest to implement, we can just divide the world into a grid and store the entities in the grid
            // // Then we can just check the entities in the same grid cell and the adjacent cells
            // // This might also be the most memory efficient solution, as we can just store the entities in a list in the grid cell
            // foreach (var (position,entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<EnemyTag>().WithEntityAccess())
            // {
            //     var delta = position.ValueRO.Position - playerPosition;
            //     var distanceToNewTarget = math.lengthsq(delta);
            //     
            //     if (target.Direction.Equals(float3.zero) || distanceToNewTarget < closestDistance)
            //     {
            //         closestDistance = distanceToNewTarget;
            //         target.Direction = math.normalize(delta);
            //     }
            // }
            //
        }
    }
}