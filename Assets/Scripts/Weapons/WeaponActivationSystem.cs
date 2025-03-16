using DotsShooter.Common;
using DotsShooter.Player;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace DotsShooter.Weapons
{
    [UpdateInGroup(typeof(AttackSystemGroup))]
    public partial struct WeaponActivationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StatsInitializedFlag>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (weaponState, weaponData, parent, entity) 
                     in SystemAPI.Query<RefRW<WeaponState>, RefRO<WeaponData>, RefRO<Parent>>()
                         .WithNone<WeaponActiveFlag>().WithEntityAccess())
            {
                weaponState.ValueRW.CooldownTimer -= deltaTime;
                if (weaponState.ValueRO.CooldownTimer > 0f) continue;
                SystemAPI.SetComponentEnabled<WeaponActiveFlag>(entity, true);
                
                // TODO: Introduce modifiers for attack speed
                var cooldownModifier = SystemAPI.GetComponent<PlayerStatModifications>(parent.ValueRO.Value).CooldownMultiplier;
                weaponState.ValueRW.CooldownTimer = weaponData.ValueRO.Cooldown * (cooldownModifier);
            }
        }
    }
}