using DotsShooter.Metaplay;
using Unity.Entities;

namespace DotsShooter.Player
{
    public struct StatsNeedsInitialization : IEnableableComponent, IComponentData
    {

    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class StatsSystem : SystemBase
    {

        protected override void OnCreate()
        {
            RequireForUpdate<StatsNeedsInitialization>();
        }


        protected override void OnUpdate()
        {
            var statsData = MetaplayClient.PlayerModel.GameStats;
            foreach (var (autoShooting, movement, autoTargetingPlayer, entity) in
                     SystemAPI.Query<RefRW<AutoShootingComponent>, RefRW<MovementComponent>, RefRW<AutoTargetingPlayer>>()
                         .WithAll<PlayerTag, StatsNeedsInitialization>()
                         .WithEntityAccess())
            {
                autoShooting.ValueRW.ProjectileDamage = statsData.Damage.Float;
                autoShooting.ValueRW.Cooldown = statsData.AttackSpeed.Float;
                autoShooting.ValueRW.ProjectileRadius = statsData.ExplosionRadius.Float;
                movement.ValueRW.Speed = statsData.MoveSpeed.Float;
                autoTargetingPlayer.ValueRW.Range = statsData.Range.Float;
                
                SystemAPI.SetComponentEnabled<StatsNeedsInitialization>(entity, false);
            }
        }
    }
}