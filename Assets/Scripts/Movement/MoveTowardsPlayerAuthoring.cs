using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct MoveTowardsPlayerFlag : IComponentData
    {
    }
    
    public class MoveTowardsPlayerAuthoring : MonoBehaviour
    {
        public class MoveTowardsPlayerBaker : Baker<MoveTowardsPlayerAuthoring>
        {
            public override void Bake(MoveTowardsPlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<MoveTowardsPlayerFlag>(entity);
            }
        }
    }
    
}