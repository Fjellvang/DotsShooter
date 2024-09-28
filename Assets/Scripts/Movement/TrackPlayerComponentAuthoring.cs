using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct TrackPlayerComponent : IComponentData
    {
    }
    
    public class TrackPlayerComponentAuthoring : MonoBehaviour
    {
        public class TargetPlayerComponentBaker : Baker<TrackPlayerComponentAuthoring>
        {
            public override void Bake(TrackPlayerComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<TrackPlayerComponent>(entity);
            }
        }
    }
}