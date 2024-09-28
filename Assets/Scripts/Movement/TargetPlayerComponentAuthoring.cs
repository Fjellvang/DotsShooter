using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct TargetPlayerComponent : IComponentData
    {
    }
    
    public class TargetPlayerComponentAuthoring : MonoBehaviour
    {
        public class TargetPlayerComponentBaker : Baker<TargetPlayerComponentAuthoring>
        {
            public override void Bake(TargetPlayerComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<TargetPlayerComponent>(entity);
            }
        }
    }
}