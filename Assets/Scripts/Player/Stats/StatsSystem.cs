using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Player
{
    public struct StatsNeedsInitialization : IEnableableComponent, IComponentData
    {

    }

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
            foreach (var (autoShooting, movement, entity) in
                     SystemAPI.Query<RefRW<AutoShootingComponent>, RefRW<MovementComponent>>()
                         .WithAll<PlayerTag, StatsNeedsInitialization>()
                         .WithEntityAccess())
            {
                Debug.Log("StatsSystem OnUpdate");
                autoShooting.ValueRW.ProjectileDamage = _statsData.Damage;
                autoShooting.ValueRW.Cooldown = _statsData.AttackSpeed;
                autoShooting.ValueRW.ProjectileRadius = _statsData.ExplosionRadius;
                movement.ValueRW.Speed = _statsData.MoveSpeed;
                
                SystemAPI.SetComponentEnabled<StatsNeedsInitialization>(entity, false);
            }
        }
    }
}