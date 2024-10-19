using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DotsShooter.SpatialPartitioning
{
    /// <summary>
    /// Singleton component to store grid properties
    /// </summary>
    public struct GridProperties : IComponentData
    {
        public float CellSize;
        public int2 GridDimensions;
    }
    
    public struct GridPropertiesInitializer : IComponentData{ }
    public struct GridPropertiesInitialized : IComponentData{ }
    public class GridPropertiesAuthoring : MonoBehaviour
    {
        public float CellSize;
        public int2 GridDimensions;

        public class GridPropertiesBaker : Baker<GridPropertiesAuthoring>
        {
            public override void Bake(GridPropertiesAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity,
                    new GridProperties { CellSize = authoring.CellSize, GridDimensions = authoring.GridDimensions });
                AddComponent(entity, new GridPropertiesInitializer());
                Debug.Log("Baked GridProperties");
            }
        }
    }
}