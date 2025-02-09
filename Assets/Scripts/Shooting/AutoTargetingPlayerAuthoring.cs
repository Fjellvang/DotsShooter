using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DotsShooter
{
    public struct AutoTargetingPlayer : IComponentData
    {
        /// <summary>
        /// Direction to the target enemy, normalized.
        /// </summary>
        public float3 Direction;

        public float Range;
    }
    
    public class AutoTargetingPlayerAuthoring : MonoBehaviour
    {
        public float Range = 10;
        public class AutoTargetingPlayerBaker : Baker<AutoTargetingPlayerAuthoring>
        {
            public override void Bake(AutoTargetingPlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new AutoTargetingPlayer()
                {
                    Range = authoring.Range
                });
            }
        }
    }
}