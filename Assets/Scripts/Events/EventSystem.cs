using System;
using DotsShooter.Destruction;
using DotsShooter.Pickup;
using DotsShooter.Player;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsShooter.Events
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EventSystem : SystemBase
    {
        public event Action<float3> OnPlayerDied;
        public event Action OnEnemyDied;
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
                    case EventType.PauseRequested:
                        TogglePause();
                        break;
                    case EventType.ShowPowerupMenu:
                        OnShowPowerUpMenu?.Invoke();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            foreach (var localTransform in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<PlayerTag>()
                         .WithAll<DestroyNextFrame>())
            {
                OnPlayerDied?.Invoke(localTransform.ValueRO.Position);
            }

            foreach (var _ in SystemAPI.Query<RefRO<EnemyTag>>()
                         .WithAll<DestroyNextFrame>())
            {
                OnEnemyDied?.Invoke();
            }

            foreach (var _ in SystemAPI.Query<RefRO<XpPickupComponent>>()
                         .WithAll<DestroyNextFrame>())
            {
                OnXpPickup?.Invoke();
            }
        }

        public void TogglePause()
        {
            OnTogglePause?.Invoke();
        }
    }
}