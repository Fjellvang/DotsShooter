using System;
using DotsShooter.Common;
using DotsShooter.Destruction;
using DotsShooter.Pickup;
using DotsShooter.Player;
using Game.Logic.GameConfigs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DotsShooter.Events
{
    [UpdateInGroup(typeof(EffectsSystemGroup))]
    public partial class EventSystem : SystemBase
    {
        public event Action<float3> OnPlayerDied;
        public event Action OnEnemyDied;
        public event Action OnTogglePause;
        public event Action<CoinType> OnGoldPickup;
        public event Action OnPlayerWon;

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
                    case EventType.PlayerWon:
                        Debug.Log("Player won");
                        OnPlayerWon?.Invoke();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            foreach (var localTransform in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<PlayerTag>()
                         .WithAll<DestroyNextFrameTag>())
            {
                OnPlayerDied?.Invoke(localTransform.ValueRO.Position);
            }

            foreach (var _ in SystemAPI.Query<RefRO<EnemyTag>>()
                         .WithAll<DestroyNextFrameTag>())
            {
                OnEnemyDied?.Invoke();
            }

            foreach (var component in SystemAPI.Query<RefRO<GoldPickupComponent>>()
                         .WithAll<DestroyNextFrameTag>())
            {
                OnGoldPickup?.Invoke(component.ValueRO.Value);
            }
        }

        public void TogglePause()
        {
            OnTogglePause?.Invoke();
        }
    }

}