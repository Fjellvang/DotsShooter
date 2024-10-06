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
    }
    
    public class AutoTargetingPlayerAuthoring : MonoBehaviour
    {
        public class AutoTargetingPlayerBaker : Baker<AutoTargetingPlayerAuthoring>
        {
            public override void Bake(AutoTargetingPlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<AutoTargetingPlayer>(entity);
            }
        }
    }
}