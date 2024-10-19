using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
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

            // Create entities for each grid cell
            for (int y = 0; y < properties.GridDimensions.y; y++)
            {
                for (int x = 0; x < properties.GridDimensions.x; x++)
                {
                    var cellEntity = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponentData(cellEntity, new GridCell { CellIndex = new int2(x, y) });
                    state.EntityManager.AddBuffer<GridCellContents>(cellEntity);
                }
            }
            
            state.EntityManager.RemoveComponent<GridPropertiesInitializer>(entity);
            state.EntityManager.AddComponentData(entity, new GridPropertiesInitialized());
        }
    }
}