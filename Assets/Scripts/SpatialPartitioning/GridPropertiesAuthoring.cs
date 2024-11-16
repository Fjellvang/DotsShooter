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

        [Header("Debug")]
        public Color GridColor = new Color(0.2f, 0.2f, 1f, 0.3f); 
        public bool ShowCellLabels = true;
        
        public class GridPropertiesBaker : Baker<GridPropertiesAuthoring>
        {
            public override void Bake(GridPropertiesAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity,
                    new GridProperties { CellSize = authoring.CellSize, GridDimensions = authoring.GridDimensions });
                AddComponent(entity, new GridPropertiesInitializer());
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Calculate grid origin (top-left corner)
            float2 gridOrigin = new float2(
                -(GridDimensions.x * CellSize) / 2f,
                -(GridDimensions.y * CellSize) / 2f
            );

            // Store original Gizmos color
            Color originalColor = Gizmos.color;
            Gizmos.color = GridColor;

            // Draw horizontal lines (X axis)
            for (int y = 0; y <= GridDimensions.y; y++)
            {
                float yPos = gridOrigin.y + (y * CellSize);
                Vector3 startPos = transform.position + new Vector3(gridOrigin.x, yPos, 0);
                Vector3 endPos = transform.position + new Vector3(gridOrigin.x + (GridDimensions.x * CellSize), yPos, 0);
                Gizmos.DrawLine(startPos, endPos);
            }

            // Draw vertical lines (Y axis)
            for (int x = 0; x <= GridDimensions.x; x++)
            {
                float xPos = gridOrigin.x + (x * CellSize);
                Vector3 startPos = transform.position + new Vector3(xPos, gridOrigin.y, 0);
                Vector3 endPos = transform.position + new Vector3(xPos, gridOrigin.y + (GridDimensions.y * CellSize), 0);
                Gizmos.DrawLine(startPos, endPos);
            }

#if UNITY_EDITOR
            // Draw cell indices (optional)
            if (ShowCellLabels)
            {
                for (int x = 0; x < GridDimensions.x; x++)
                {
                    for (int y = 0; y < GridDimensions.y; y++)
                    {
                        Vector3 cellCenter = transform.position + new Vector3(
                            gridOrigin.x + (x + 0.5f) * CellSize,
                            gridOrigin.y + (y + 0.5f) * CellSize,
                            0
                        );

                        // Draw cell index
                        UnityEditor.Handles.Label(cellCenter, $"({x},{y}), {y * GridDimensions.x + x}");
                    }
                }
            }
#endif

            // Highlight center cell
            int centerX = GridDimensions.x / 2;
            int centerY = GridDimensions.y / 2;
            Vector3 centerCellOrigin = transform.position + new Vector3(
                gridOrigin.x + centerX * CellSize,
                gridOrigin.y + centerY * CellSize,
                0
            );

            // Draw center cell in different color
            Color centerColor = new Color(1f, 0.5f, 0f, 0.2f); // Orange-ish
            Gizmos.color = centerColor;
            Gizmos.DrawCube(
                centerCellOrigin + new Vector3(CellSize/2, CellSize/2, 0), 
                new Vector3(CellSize, CellSize, 0.1f)
            );

            // Draw world origin marker
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.2f);

            // Draw grid bounds
            Vector3 center = transform.position + new Vector3(
                gridOrigin.x + (GridDimensions.x * CellSize) / 2f,
                gridOrigin.y + (GridDimensions.y * CellSize) / 2f,
                0
            );

            Vector3 size = new Vector3(
                GridDimensions.x * CellSize,
                GridDimensions.y * CellSize,
                0.1f
            );

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f); // Yellow-ish
            Gizmos.DrawWireCube(center, size);
            
            // Restore original Gizmos color
            Gizmos.color = originalColor;
        }
    }
}