using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DotsShooter.SpatialPartitioning
{
    public struct Grid : IComponentData
    {
        public NativeArray<GridCell> Cells;
        public int2 GridSize;
        public float2 CellSize;
        
    }

    // Structure to represent a single grid cell
    public struct GridCell
    {
        public NativeList<Entity> Entities;
    }
}