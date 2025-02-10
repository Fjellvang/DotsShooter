using DotsShooter.Events;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Event = DotsShooter.Events.Event;
using EventType = DotsShooter.Events.EventType;

namespace DotsShooter.Time
{
    public struct SimulationTime : IComponentData
    {
        public float ElapsedTime;
        public float GameTime;
        public bool GameEnded;
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
            
            //TODO: This is a temporary solution. We should have a way to set the game time 
            var time = simulationTime.ValueRW;
            if (time.ElapsedTime >= time.GameTime && !time.GameEnded)
            {
                var eventQueue = SystemAPI.GetSingletonRW<EventQueue>().ValueRW.Value; 
                eventQueue.Enqueue(new Event(){EventType = EventType.PlayerWon});
                time.GameEnded = true;
            }
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
                simulationTime.ValueRW.GameEnded = false;
                ecb.AddComponent<TimeSimulationEnabled>(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
