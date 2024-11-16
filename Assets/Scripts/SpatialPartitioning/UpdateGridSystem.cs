using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DotsShooter.SpatialPartitioning
{
    
    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct UpdateGridSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Grid>();
            state.RequireForUpdate<GridProperties>();
            state.RequireForUpdate<GridPropertiesInitialized>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var grid= SystemAPI.GetSingletonRW<Grid>();
            // Clear the grid
            var clearGridJob = new ClearGridJob
            {
                Cells = grid.ValueRW.Cells
            };
            var clearGridJobHandle = clearGridJob.Schedule(grid.ValueRO.Cells.Length, 64);
        
            // Populate the grid
            var entityQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag>()
                .WithAll<LocalToWorld>()
                .Build();
            using var entities = entityQuery.ToEntityArray(Allocator.TempJob);
            using var entityCellMappings = new NativeArray<EntityCellMapping>(entities.Length, Allocator.TempJob);
            var populateGridJob = new PopulateGridJob
            {
                Entities = entities,
                LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                EntityCellMappings = entityCellMappings,
                GridSize = grid.ValueRO.GridSize,
                CellSize = grid.ValueRO.CellSize,
                GridOrigin = grid.ValueRO.GridOrigin
            };

            var populateGridJobHandle = populateGridJob.Schedule(populateGridJob.Entities.Length, 64, clearGridJobHandle);
            
            populateGridJobHandle.Complete();
            state.Dependency.Complete();
            
            #if UNITY_EDITOR
            int count = 0;
            #endif
            // Process the mappings and populate the grid
            for (int i = 0; i < entityCellMappings.Length; i++)
            {
                var mapping = entityCellMappings[i];
                if (mapping.CellIndex != -1)
                {
#if UNITY_EDITOR
                    count++;
#endif

                    grid.ValueRW.Cells[mapping.CellIndex].Entities.Add(mapping.Entity);
                    Debug.Log($"Entity {mapping.Entity} added to cell {mapping.CellIndex}");
                }
#if UNITY_EDITOR
                else
                {
                    Debug.Log($"Entity {mapping.Entity} is out of bounds");
                    var localToWorld = SystemAPI.GetComponent<LocalToWorld>(mapping.Entity);
                    Debug.Log($"Position: {localToWorld.Position}");
                }
#endif
            }
#if UNITY_EDITOR
            // Debug.Log($"Populated {count} entities");
#endif


            // state.Dependency = populateGridJobHandle;
            // foreach (var cell in grid.ValueRW.Cells)
            // {
            //     cell.Entities.Clear();
            // }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    
    [BurstCompile]
    public struct ClearGridJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<GridCell> Cells;
    
        public void Execute(int index)
        {
            Cells[index].Entities.Clear();
        }
    }
    
    // Intermediate structure to hold entity-to-cell mappings
    public struct EntityCellMapping
    {
        public Entity Entity;
        public int CellIndex;
    }
    [BurstCompile]
    public struct PopulateGridJob : IJobParallelFor
    {
        [ReadOnly] 
        public NativeArray<Entity> Entities;
        [ReadOnly] 
        public ComponentLookup<LocalToWorld> LocalToWorldLookup;
        public NativeArray<EntityCellMapping> EntityCellMappings;
        public int2 GridSize;
        public float2 CellSize;
        public float2 GridOrigin;

        public void Execute(int index)
        {
            Entity entity = Entities[index];
            float3 position = LocalToWorldLookup[entity].Position;
            
            // Offset position by grid origin to get local grid coordinates
            float2 localPosition = new float2(
                position.x - GridOrigin.x,
                position.y - GridOrigin.y
            );

            int2 cellIndex = new int2(
                (int)(localPosition.x / CellSize.x),
                (int)(localPosition.y / CellSize.y)
            );

            // Ensure the entity is within the grid bounds
            if (cellIndex.x >= 0 && cellIndex.x < GridSize.x && 
                cellIndex.y >= 0 && cellIndex.y < GridSize.y)
            {
                int flatIndex = cellIndex.y * GridSize.x + cellIndex.x;
                EntityCellMappings[index] = new EntityCellMapping
                {
                    Entity = entity,
                    CellIndex = flatIndex
                };
            }
            else
            {
                EntityCellMappings[index] = new EntityCellMapping
                {
                    Entity = entity,
                    CellIndex = -1
                };
            }
        }
    }

    // [BurstCompile]
    // public partial struct UpdateGridJob : IJobEntity
    // {
    //     [ReadOnly] public GridProperties GridProperties;
    //     [ReadOnly] public ComponentLookup<GridCell> GridCellLookup;
    //     public BufferLookup<GridCellContents> GridContentsLookup;
    //     public EntityCommandBuffer.ParallelWriter ECB;
    //
    //     public void Execute(Entity entity, [EntityIndexInQuery] int index, in LocalTransform transform)
    //     {
    //         int2 cellIndex = new int2(
    //             (int)(transform.Position.x / GridProperties.CellSize),
    //             (int)(transform.Position.y / GridProperties.CellSize)
    //         );
    //
    //         // Find the correct cell entity
    //         foreach (var (gridCell, cellEntity) in GridCellLookup.GetKeyValueEnumerable())
    //         {
    //             if (gridCell.CellIndex.Equals(cellIndex))
    //             {
    //                 // Add this enemy to the cell's contents
    //                 ECB.AppendToBuffer(index, cellEntity, new GridCellContents { Value = entity });
    //                 break;
    //             }
    //         }
    //     }
    // }
}