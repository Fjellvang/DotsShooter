using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.GraphicsIntegration;
using UnityEngine;

namespace DotsShooter
{
    public struct MovementSpeedComponent : IComponentData {
        public float Speed;
    }
    
    public struct MovementDirectionComponent : IComponentData
    {
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
                
                var data = new MovementSpeedComponent
                {
                    Speed = authoring.speed, // For the player this is overriden by gameconfigs..
                };
                AddComponent(entity, new MovementDirectionComponent()
                {
                    Direction = authoring.initialDirection
                });
                AddComponent(entity, data);
                AddComponent(entity, new PhysicsGraphicalSmoothing{ ApplySmoothing = 1});
            }
        }
    }
}