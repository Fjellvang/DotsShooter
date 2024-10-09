using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Unity.Rendering
{
    [MaterialProperty("_InitialFrame")]
    struct InitialFrameFloatOverride : IComponentData, IEnableableComponent
    {
        public float Value;
    }

    [BurstCompile]

    public partial struct SetInitialFrameSystem : ISystem
    {
        Random _random;
        private uint _seed;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _random = new Random(1234);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (initialFrameOverride, entity) in SystemAPI.Query<RefRW<InitialFrameFloatOverride>>().WithEntityAccess())
            {
                initialFrameOverride.ValueRW.Value = _random.NextInt(0, 5);
                SystemAPI.SetComponentEnabled<InitialFrameFloatOverride>(entity, false);
            }
        }
    }
}
