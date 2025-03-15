using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter
{
    public struct AutoTargetingPlayer : IComponentData
    {
        /// <summary>
        /// Direction to the target enemy, normalized.
        /// </summary>
        public float3 Direction;
        public float Range;
        /// <summary>
        /// which is the target enemy
        /// </summary>
        public CollisionFilter CollisionFilter;
    }
    
    public class AutoTargetingPlayerAuthoring : MonoBehaviour
    {
        public float Range = 10;
        public PhysicsCategoryTags BelongsTo;
        public PhysicsCategoryTags CollidesWith;

        public class AutoTargetingPlayerBaker : Baker<AutoTargetingPlayerAuthoring>
        {
            public override void Bake(AutoTargetingPlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new AutoTargetingPlayer { Range = authoring.Range, CollisionFilter = new CollisionFilter
                    {
                        BelongsTo = authoring.BelongsTo.Value,
                        CollidesWith = authoring.CollidesWith.Value
                    } });
            }
        }
    }
}