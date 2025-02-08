using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_Radius")]
    struct RadiusFloatOverride : IComponentData
    {
        public float Value;
    }
}
