using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DotsShooter.Pickup
{
    public struct SpawnXpPickups : IComponentData
    {
        public NativeQueue<float3> Locations;
    }
}