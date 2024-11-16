using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DotsShooter.SpatialPartitioning
{
    [BurstCompile]
    public partial struct InitializeGridSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GridProperties>();
            state.RequireForUpdate<GridPropertiesInitializer>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Initialize(ref state);
        }

        private void Initialize(ref SystemState state)
        {
            Debug.Log("initializing grid system");
            // Do nothing
            // Create and set up the grid properties entity
            var success = SystemAPI.TryGetSingletonEntity<GridProperties>(out var entity);
            if (!success)
            {
                Debug.LogError("GridProperties entity not found");
                return;
            }
            var properties = state.EntityManager.GetComponentData<GridProperties>(entity);
            
            // Calculate grid origin (top-left corner of the grid)
            float2 gridOrigin = new float2(
                -(properties.GridDimensions.x * properties.CellSize) / 2f,
                -(properties.GridDimensions.y * properties.CellSize) / 2f
            );
            
            var gridSingleton = new Grid
            {
                GridSize = properties.GridDimensions,
                CellSize = properties.CellSize,
                GridOrigin = gridOrigin,
                Cells = new NativeArray<GridCell>(properties.GridDimensions.x * properties.GridDimensions.y, Allocator.Persistent)
            };
            
            for (int i = 0; i < gridSingleton.Cells.Length; i++)
            {
                gridSingleton.Cells[i] = new GridCell
                {
                    Entities = new NativeList<Entity>(Allocator.Persistent)
                };
            }
            
            state.EntityManager.AddComponentData(entity, gridSingleton);
            state.EntityManager.RemoveComponent<GridPropertiesInitializer>(entity);
            state.EntityManager.AddComponentData(entity, new GridPropertiesInitialized());
        }
        // TODO: Consider adding OnDestroy method to clean up native collections
        [BurstCompile]
        public void OnDestroy(ref SystemState state) 
        {
            // Clean up native collections
            Debug.Log("Cleaning up grid system");
            var grid= SystemAPI.GetSingletonRW<Grid>().ValueRW;
            for (int i = 0; i < grid.Cells.Length; i++)
            {
                grid.Cells[i].Entities.Dispose();
            }
            grid.Cells.Dispose();
        }
    }

}