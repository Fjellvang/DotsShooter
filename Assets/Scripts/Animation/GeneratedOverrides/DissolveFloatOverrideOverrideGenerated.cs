using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_Dissolve")]
    struct DissolveFloatOverride : IComponentData
    {
        public float Value;
    }
}
