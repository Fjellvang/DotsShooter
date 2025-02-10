using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Time
{
    public class SimulationTimeAuthoring : MonoBehaviour
    {
        public float GameTime = 30.0f; //TODO: this should proably be handled differently, but for now it works
        public class SimulationTimeBaker : Baker<SimulationTimeAuthoring>
        {
            public override void Bake(SimulationTimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SimulationTime()
                {
                    GameTime = authoring.GameTime
                });
            }
        }
    }
}