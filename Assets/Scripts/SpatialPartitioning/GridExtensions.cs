using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DotsShooter.SpatialPartitioning
{
    public static class GridExtensions
    {
        public static bool TryGetCellIndex(this Grid grid, float3 worldPosition, out int2 cellIndex)
        {
            float2 localPosition = new float2(
                worldPosition.x - grid.GridOrigin.x,
                worldPosition.y - grid.GridOrigin.y
            );

            cellIndex = new int2(
                (int)(localPosition.x / grid.CellSize.x),
                (int)(localPosition.y / grid.CellSize.y)
            );

            return cellIndex.x >= 0 && cellIndex.x < grid.GridSize.x && 
                   cellIndex.y >= 0 && cellIndex.y < grid.GridSize.y;
        }

        public static float3 GetWorldPosition(this Grid grid, int2 cellIndex)
        {
            return new float3(
                grid.GridOrigin.x + (cellIndex.x + 0.5f) * grid.CellSize.x,
                grid.GridOrigin.y + (cellIndex.y + 0.5f) * grid.CellSize.y,
                0
            );
        }

        public static bool FindClosestEntity(this Grid grid, float3 position,
            ComponentLookup<LocalToWorld> localToWorldLookup, out float3 delta)
        {
            return FindClosestEntity(grid, position, 10000, localToWorldLookup, out _, out delta);
        }
        public static bool FindClosestEntity(this Grid grid, float3 position,
            ComponentLookup<LocalToWorld> localToWorldLookup, out Entity closestEntity, out float3 delta)
        {
            return FindClosestEntity(grid, position, 10000, localToWorldLookup, out closestEntity, out delta);
        }
        public static bool FindClosestEntity(this Grid grid, float3 position, float maxSearchRadius, 
            ComponentLookup<LocalToWorld> localToWorldLookup, out Entity closestEntity, out float3 delta)
        {
            closestEntity = Entity.Null;
            delta = float3.zero;
            var closestDistanceSq = maxSearchRadius * maxSearchRadius;

            // Get the cell containing the search position
            var centerCell = new int2(
                (int)((position.x - grid.GridOrigin.x) / grid.CellSize.x),
                (int)((position.y - grid.GridOrigin.y) / grid.CellSize.y)
            );

            // Calculate max rings we might need to search
            int maxRings = (int)math.ceil(maxSearchRadius / math.min(grid.CellSize.x, grid.CellSize.y));
            bool foundAny = false;

            // Search in expanding rings
            for (int ring = 0; ring <= maxRings; ring++)
            {
                // Track the closest enemy found in this ring
                float closestInRingSq = float.MaxValue;

                // Check all cells in current ring
                for (int dx = -ring; dx <= ring; dx++)
                {
                    for (int dy = -ring; dy <= ring; dy++)
                    {
                        // Skip cells that aren't on the ring's edge (except when ring = 0)
                        if (ring != 0 && math.abs(dx) != ring && math.abs(dy) != ring)
                            continue;

                        var cellPos = centerCell + new int2(dx, dy);

                        // Skip if outside grid bounds
                        if (cellPos.x < 0 || cellPos.x >= grid.GridSize.x ||
                            cellPos.y < 0 || cellPos.y >= grid.GridSize.y)
                            continue;

                        var cellIndex = cellPos.y * grid.GridSize.x + cellPos.x;
                        var cell = grid.Cells[cellIndex];

                        // Check each entity in the current cell
                        foreach (var entity in cell.Entities)
                        {
                            if (!localToWorldLookup.EntityExists(entity))
                            {
                                // Entity no longer exists
                                continue;
                            }
                            var entityTransform = localToWorldLookup[entity];
                            var direction = entityTransform.Position - position;
                            var distanceSq = math.lengthsq(direction);

                            if (distanceSq < closestDistanceSq)
                            {
                                closestDistanceSq = distanceSq;
                                closestEntity = entity;
                                delta = direction;
                                foundAny = true;
                                closestInRingSq = math.min(closestInRingSq, distanceSq);
                            }
                        }
                    }
                }

                // Early exit condition:
                // If we found an enemy in this ring, and the nearest possible enemy in the next ring
                // would be further away than our current closest, we can stop searching
                if (foundAny && ring > 0)
                {
                    var minNextRingDistSq = ring * ring * 
                                            math.min(grid.CellSize.x * grid.CellSize.x, 
                                                grid.CellSize.y * grid.CellSize.y);
            
                    if (minNextRingDistSq > closestDistanceSq)
                        break;
                }
            }

            return foundAny;
        }
    }
}