using System;
using DotsShooter.Damage;
using DotsShooter.Health;
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
        
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (damaged, healthComponent, entity) in 
                     SystemAPI.Query<RefRO<PlayerWasDamaged>, RefRO<HealthComponent>>()
                         .WithAll<PlayerTag>()
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