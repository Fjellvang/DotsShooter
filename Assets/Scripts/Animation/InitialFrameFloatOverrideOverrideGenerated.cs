using Unity.Entities;
using UnityEngine;

namespace Unity.Rendering
{
    [MaterialProperty("_InitialFrame")]
    struct InitialFrameFloatOverride : IComponentData, IEnableableComponent
    {
        public float Value;
    }
}
