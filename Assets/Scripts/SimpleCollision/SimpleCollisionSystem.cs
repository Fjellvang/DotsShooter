using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using UnityEngine;

namespace DotsShooter.SimpleCollision
{
    public struct SimpleCollisionEvent : IBufferElementData
    {
        public Entity EntityA;
        public Entity EntityB;

        public SimpleCollisionEvent(TriggerEvent triggerEvent)
        {
            EntityA = triggerEvent.EntityA;
            EntityB = triggerEvent.EntityB;
        }
        
        public Entity GetOtherEntity(Entity entity) => entity == EntityA ? EntityB : EntityA;
    }
    
    [BurstCompile]
    public struct CollectSimpleTriggerEvents : ITriggerEventsJob
    {
        public NativeList<SimpleCollisionEvent> TriggerEvents;
        public void Execute(TriggerEvent triggerEvent) => TriggerEvents.Add(new SimpleCollisionEvent(triggerEvent));
    }
    
    [BurstCompile]
    public struct ConvertEventStreamToDynamicBufferJob : IJob
    {
        public NativeList<SimpleCollisionEvent> TriggerEvents;
        public BufferLookup<SimpleCollisionEvent> BufferLookup;
        
        public void Execute()
        {
            foreach (var triggerEvent in TriggerEvents)
            {
                var addToA = BufferLookup.HasBuffer(triggerEvent.EntityA);
                var addToB = BufferLookup.HasBuffer(triggerEvent.EntityB);
                if (addToA)
                {
                    BufferLookup[triggerEvent.EntityA].Add(triggerEvent);
                }

                if (addToB)
                {
                    BufferLookup[triggerEvent.EntityB].Add(triggerEvent);
                }
            }
        }
    }
    
    public partial struct SimpleCollisionSystem : ISystem
    {
        private NativeList<SimpleCollisionEvent> _triggerEvents;
        private EntityQuery _simpleCollisionQuery;
        public BufferLookup<SimpleCollisionEvent> BufferLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<SimpleCollisionEvent>()
                .WithAll<SimpleCollisionComponent>()
                ;
            
            _triggerEvents = new NativeList<SimpleCollisionEvent>(Allocator.Persistent);
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<SimpleCollisionComponent>();
            
            BufferLookup = state.GetBufferLookup<SimpleCollisionEvent>();
            
            _simpleCollisionQuery = state.GetEntityQuery(builder);
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _triggerEvents.Dispose();
        }
        
        [BurstCompile]
        public partial struct ClearTriggerEventDynamicBufferJob : IJobEntity
        {
            public void Execute(ref DynamicBuffer<SimpleCollisionEvent> eventBuffer) => eventBuffer.Clear();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            BufferLookup.Update(ref state);
            
            // Clear the dynamic buffers
            state.Dependency = new ClearTriggerEventDynamicBufferJob()
                .ScheduleParallel(_simpleCollisionQuery, state.Dependency);
            
            // Clear the trigger events from the previous frame
            _triggerEvents.Clear();
            
            // Collect the trigger events
            state.Dependency = new CollectSimpleTriggerEvents 
            {
                TriggerEvents = _triggerEvents
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
            
            // Ensure the trigger events are converted to dynamic buffers on the entities
            state.Dependency = new ConvertEventStreamToDynamicBufferJob
            {
                TriggerEvents = _triggerEvents,
                BufferLookup = BufferLookup
            }.Schedule(state.Dependency);
        }
    }
}