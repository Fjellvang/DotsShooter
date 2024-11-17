using System;
using DotsShooter.Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DotsShooter.Events
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EventSystem : SystemBase
    {
        public event Action<float3> OnPlayerDied;
        public event Action OnEnemyDied;
        public event Action OnBulletDied; // Do bullets die?
        public event Action OnTogglePause;
        public event Action OnShowPowerUpMenu;
        
        public event Action OnXpPickup;

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
                switch (e.EventType)
                {
                    case EventType.EnemyDied:
                        OnEnemyDied?.Invoke();
                        break;
                    case EventType.PlayerDied:
                        OnPlayerDied?.Invoke(e.Location);
                        break;
                    case EventType.BulletDied:
                        OnBulletDied?.Invoke();
                        break;
                    case EventType.PauseRequested:
                        TogglePause();
                        break;
                    case EventType.ShowPowerupMenu:
                        OnShowPowerUpMenu?.Invoke();
                        break;
                    case EventType.XpPickup:
                        OnXpPickup?.Invoke();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void TogglePause()
        {
            OnTogglePause?.Invoke();
        }
    }
}