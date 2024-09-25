using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct RotateTowardsComponent : IComponentData
    {
        public float RotationOffset;
    }

    public class RotateTowardsComponentAuthoring : MonoBehaviour
    {
        [SerializeField] public float rotationOffset;
        public class RotateTowardsComponentBaker : Baker<RotateTowardsComponentAuthoring>
        {
            public override void Bake(RotateTowardsComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RotateTowardsComponent { RotationOffset = authoring.rotationOffset });
            }
        }
    }
}