﻿using System;
using DotsShooter.Damage;
using DotsShooter.Health;
using DotsShooter.Player;
using Unity.Collections;
using Unity.Entities;

namespace DotsShooter.UI
{
    public struct HealthEventDetails
    {
        public int Health;
        public int MaxHealth;
    }
    
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class UiSystem : SystemBase
    {
        public event Action<HealthEventDetails> PlayerWasDamaged;
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<PlayerWasDamaged>();
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (healthComponent, entity) in 
                     SystemAPI.Query<RefRO<HealthComponent>>()
                         .WithAll<PlayerTag>()
                         .WithAll<PlayerWasDamaged>()
                         .WithEntityAccess())
            {
                PlayerWasDamaged?.Invoke(new HealthEventDetails
                {
                    Health = healthComponent.ValueRO.Health,
                    MaxHealth = healthComponent.ValueRO.MaxHealth
                });
                ecb.RemoveComponent<PlayerWasDamaged>(entity);
            }
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}