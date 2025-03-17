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
            state.RequireForUpdate<GameStateComponent>();
        }
        public void OnUpdate(ref SystemState state)
        {
            var simulationTime = SystemAPI.GetSingletonRW<SimulationTime>();
            var gameState = SystemAPI.GetSingletonRW<GameStateComponent>().ValueRW;
            simulationTime.ValueRW.ElapsedTime += SystemAPI.Time.DeltaTime;
            
            //TODO: This is a temporary solution. We should have a way to set the game time 
            var time = simulationTime.ValueRW;
            if (time.ElapsedTime >= gameState.GameTime && !gameState.GameEnded)
            {
                var eventQueue = SystemAPI.GetSingletonRW<EventQueue>().ValueRW.Value; 
                eventQueue.Enqueue(new Event(){EventType = EventType.PlayerWon});
                gameState.GameEnded = true;
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
                ecb.AddComponent<TimeSimulationEnabled>(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
