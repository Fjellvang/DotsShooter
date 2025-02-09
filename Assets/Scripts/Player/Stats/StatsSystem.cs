using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Player
{
    public struct StatsNeedsInitialization : IEnableableComponent, IComponentData
    {

    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class StatsSystem : SystemBase
    {
        private PlayerStats _statsData;

        protected override void OnCreate()
        {
            _statsData = Resources.Load<PlayerStats>("PlayerStats");
            RequireForUpdate<StatsNeedsInitialization>();
        }


        protected override void OnUpdate()
        {
            foreach (var (autoShooting, movement, autoTargetingPlayer, entity) in
                     SystemAPI.Query<RefRW<AutoShootingComponent>, RefRW<MovementComponent>, RefRW<AutoTargetingPlayer>>()
                         .WithAll<PlayerTag, StatsNeedsInitialization>()
                         .WithEntityAccess())
            {
                autoShooting.ValueRW.ProjectileDamage = _statsData.Damage;
                autoShooting.ValueRW.Cooldown = _statsData.AttackSpeed;
                autoShooting.ValueRW.ProjectileRadius = _statsData.ExplosionRadius;
                movement.ValueRW.Speed = _statsData.MoveSpeed;
                autoTargetingPlayer.ValueRW.Range = _statsData.Range;
                
                SystemAPI.SetComponentEnabled<StatsNeedsInitialization>(entity, false);
            }
        }
    }
}