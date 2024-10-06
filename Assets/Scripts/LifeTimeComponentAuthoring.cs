using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public class LifeTimeComponentAuthoring : MonoBehaviour
    {
        public float LifeTime = 1f;

        public class LifeTimeComponentBaker : Baker<LifeTimeComponentAuthoring>
        {
            public override void Bake(LifeTimeComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new LifeTimeComponent { LifeTime = authoring.LifeTime });
            }
        }
    }
    
    public struct LifeTimeComponent : IComponentData
    {
        public float LifeTime;
    }
}