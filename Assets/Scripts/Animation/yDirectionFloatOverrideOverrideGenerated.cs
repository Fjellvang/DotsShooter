using Unity.Entities;

namespace Unity.Rendering
{
    [MaterialProperty("_yDirection")]
    struct yDirectionFloatOverride : IComponentData
    {
        public float Value;
    }
}
