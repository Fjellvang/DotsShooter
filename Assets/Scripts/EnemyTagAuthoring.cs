using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct EnemyTag : IComponentData
    {
    }
    
    public class EnemyTagAuthoring : MonoBehaviour
    {
        public class EnemyTagBaker : Baker<EnemyTagAuthoring>
        {
            public override void Bake(EnemyTagAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnemyTag>(entity);
            }
        }
    }
}