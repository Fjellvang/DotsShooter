using Unity.Entities;
using UnityEngine;

namespace Unity.Rendering
{
    public class GraphicsEntityDataAuthoring : MonoBehaviour
    {
        public GameObject Entity;

        public class GraphicsEntityDataBaker : Baker<GraphicsEntityDataAuthoring>
        {
            public override void Bake(GraphicsEntityDataAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new GraphicsEntityData { Entity = GetEntity(authoring.Entity, TransformUsageFlags.Dynamic) });
            }
        }
    }
}