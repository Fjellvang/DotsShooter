using Unity.Entities;
using UnityEngine;

namespace DotsShooter.SimpleCollision
{
    public struct SimpleCollisionComponent : IComponentData
    {
        
    }
    
    public class SimpleCollisionComponentAuthoring : MonoBehaviour
    {
        public class SimpleCollisionComponentBaker : Baker<SimpleCollisionComponentAuthoring>
        {
            public override void Bake(SimpleCollisionComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<SimpleCollisionComponent>(entity);
                
                AddBuffer<SimpleCollisionEvent>(entity);
            }
        }
    }
}