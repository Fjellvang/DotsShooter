using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_Tint")]
    struct TintVector4Override : IComponentData
    {
        public float4 Value;
    }
}
