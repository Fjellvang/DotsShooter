using DotsShooter.Destruction;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    [RequireComponent(typeof(DestroyableAuthor))]
    public class LifeTimeComponentAuthoring : MonoBehaviour
    {
        public float LifeTime = 1f;
        public bool StartEnabled = true;

        public class LifeTimeComponentBaker : Baker<LifeTimeComponentAuthoring>
        {
            public override void Bake(LifeTimeComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new LifeTimeComponent
                {
                    LifeTime = authoring.LifeTime,
                    OriginalLifeTime = authoring.LifeTime
                });
                SetComponentEnabled<LifeTimeComponent>(entity, authoring.StartEnabled);
            }
        }
    }
    
    public struct LifeTimeComponent : IComponentData, IEnableableComponent
    {
        public float LifeTime;
        public float OriginalLifeTime;
        public float TimeToDeathNormalized => LifeTime / OriginalLifeTime;
    }
}