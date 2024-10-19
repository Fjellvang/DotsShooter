using Unity.Entities;
using Unity.Mathematics;

namespace DotsShooter.SpatialPartitioning
{
    public struct GridCell : IComponentData
    {
        public int2 CellIndex;
    }
    
    /// <summary>
    /// Buffer element to store the entity in the grid cell
    /// </summary>
    public struct GridCellContents : IBufferElementData
    {
        public Entity Value;

        // Implicit conversions for easier use
        public static implicit operator Entity(GridCellContents e) => e.Value;
        public static implicit operator GridCellContents(Entity e) => new GridCellContents { Value = e };
    }
}