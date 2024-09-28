using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct RotateTowardsComponent : IComponentData
    {
        public float RotationOffset;
        public float RotationSpeed;
    }

    public class RotateTowardsComponentAuthoring : MonoBehaviour
    {
        [SerializeField] public float rotationOffset;
        [SerializeField] public float rotationSpeed = 2;
        public class RotateTowardsComponentBaker : Baker<RotateTowardsComponentAuthoring>
        {
            public override void Bake(RotateTowardsComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RotateTowardsComponent
                {
                    RotationOffset = authoring.rotationOffset,
                    RotationSpeed = authoring.rotationSpeed
                });
            }
        }
    }
}