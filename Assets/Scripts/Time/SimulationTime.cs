using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Time
{
    public struct SimulationTime : IComponentData
    {
        public float ElapsedTime;
    }

    public struct TimeSimulationEnabled : IComponentData, IEnableableComponent
    {
    }
    
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct SimulationTimeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationTime>();
            state.RequireForUpdate<TimeSimulationEnabled>();
        }
        public void OnUpdate(ref SystemState state)
        {
            var simulationTime = SystemAPI.GetSingletonRW<SimulationTime>();
            simulationTime.ValueRW.ElapsedTime += SystemAPI.Time.DeltaTime;
        }
    }
    public partial struct InitializeSimulationTimeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationTime>();
        }
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (simulationTime, entity) in SystemAPI.Query<RefRW<SimulationTime>>()
                         .WithNone<TimeSimulationEnabled>()
                         .WithEntityAccess())
            {
                Debug.Log($"Initializing simulation time entity with id {entity}");
                simulationTime.ValueRW.ElapsedTime = 0;
                ecb.AddComponent<TimeSimulationEnabled>(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
