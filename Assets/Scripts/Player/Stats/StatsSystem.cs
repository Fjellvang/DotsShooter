using DotsShooter.Metaplay;
using Unity.Burst;
using Unity.Entities;

namespace DotsShooter.Player
{
    public struct StatsNeedsInitializationFlag : IEnableableComponent, IComponentData
    {
    }
    public struct StatsInitializedFlag : IEnableableComponent, IComponentData
    {
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct StatsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StatsNeedsInitializationFlag>();
        }


        public void OnUpdate(ref SystemState state)
        {
            var statsData = MetaplayClient.PlayerModel.GameStats;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (statModifications, entity) in
                     SystemAPI.Query<RefRW<PlayerStatModifications>>()
                         .WithAll<PlayerTag, StatsNeedsInitializationFlag>()
                         .WithEntityAccess())
            {
                statModifications.ValueRW.MoveSpeedMultiplier = statsData.MoveSpeed.Float;
                statModifications.ValueRW.DamageMultiplier = statsData.Damage.Float;
                statModifications.ValueRW.CooldownMultiplier = statsData.Cooldown.Float;
                statModifications.ValueRW.RangeMultiplier = statsData.Range.Float;
                // statModifications.ValueRW.Health = statsData.Health.Float;
                statModifications.ValueRW.AreaOfEffectMultiplier = statsData.ExplosionRadius.Float;
                statModifications.ValueRW.ExtraProjectiles = statsData.ExtraProjectiles;
            
                SystemAPI.SetComponentEnabled<StatsNeedsInitializationFlag>(entity, false);
                ecb.AddComponent<StatsInitializedFlag>(entity);
            }
            ecb.Playback(state.EntityManager);
        }
    }
}