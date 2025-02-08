using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Time
{
    public class SimulationTimeAuthoring : MonoBehaviour
    {
        public class SimulationTimeBaker : Baker<SimulationTimeAuthoring>
        {
            public override void Bake(SimulationTimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<SimulationTime>(entity);
            }
        }
    }
}