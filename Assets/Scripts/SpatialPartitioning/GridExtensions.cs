using Unity.Mathematics;

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
    }
}