using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DotsShooter
{
    public struct MovementComponent : IComponentData{
        public float Speed;
        public float3 Direction;
    }

    public class MovementComponentAuthoring : MonoBehaviour
    {
        [SerializeField]
        private float speed = 5f;

        [SerializeField] 
        private Vector3 initialDirection = Vector3.zero;
        public class MovementComponentBaker : Baker<MovementComponentAuthoring>
        {
            public override void Bake(MovementComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                var data = new MovementComponent
                {
                    Speed = authoring.speed,
                    Direction = authoring.initialDirection
                };
                AddComponent(entity, data);
            }
        }
    }
}