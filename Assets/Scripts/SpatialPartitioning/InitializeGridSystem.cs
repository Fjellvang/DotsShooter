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

            //TODO: this shit could probably be moved to baking?
            var gridSingleton = new Grid
            {
                GridSize = properties.GridDimensions,
                CellSize = properties.CellSize,
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
        // protected override void OnDestroy()
        // {
        //     // Clean up native collections
        //     if (HasSingleton<GridSingleton>())
        //     {
        //         var gridSingleton = GetSingleton<GridSingleton>();
        //         for (int i = 0; i < gridSingleton.Cells.Length; i++)
        //         {
        //             gridSingleton.Cells[i].Entities.Dispose();
        //         }
        //         gridSingleton.Cells.Dispose();
        //     }
        // }
    }

}