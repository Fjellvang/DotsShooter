using Unity.Entities;
using UnityEngine;

namespace Unity.Rendering
{
    public class IsFlippedAuthoring : MonoBehaviour
    {
        public bool IsFlipped;

        public class IsFlippedBaker : Baker<IsFlippedAuthoring>
        {
            public override void Bake(IsFlippedAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new IsFlipped { Flipped = authoring.IsFlipped });
            }
        }
    }
}