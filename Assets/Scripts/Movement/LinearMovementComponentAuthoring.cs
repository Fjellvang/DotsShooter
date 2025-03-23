using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct LinearMovementComponent : IComponentData
    {
        public float Angle;
    }
    public class LinearMovementComponentAuthoring : MonoBehaviour
    {
        public float Angle;

        public class LinearMovementComponentBaker : Baker<LinearMovementComponentAuthoring>
        {
            public override void Bake(LinearMovementComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new LinearMovementComponent { Angle = authoring.Angle });
            }
        }
    }

}