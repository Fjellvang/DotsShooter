using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Events
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EventSystem : SystemBase
    {
        public event Action OnPlayerDied;
        public event Action OnEnemyDied;
        public event Action OnBulletDied; // Do bullets die?
        
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            RequireForUpdate<EventQueue>();

            var eventQueue = new NativeQueue<Event>(Allocator.Persistent);
            // Create a singleton entity with the EventQueue component
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new EventQueue() { Value = eventQueue });
        }

        protected override void OnUpdate()
        {
            var events = SystemAPI.GetSingletonRW<EventQueue>().ValueRW.Value;
            while (events.TryDequeue(out var e))
            {
                switch (e.EntityType)
                {
                    case EventType.EnemyDied:
                        OnEnemyDied?.Invoke();
                        break;
                    case EventType.PlayerDied:
                        OnPlayerDied?.Invoke();
                        break;
                    case EventType.BulletDied:
                        OnBulletDied?.Invoke();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}