using DotsShooter.Common;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace DotsShooter.Weapons
{
    public struct WeaponState : IComponentData
    {
        /// <summary>
        /// Cooldown timer for the weapon
        /// </summary>
        public float CooldownTimer;
        /// <summary>
        /// Timer tracking time between attacks
        /// </summary>
        public float NextAttackTimer;
        /// <summary>
        /// the number of attacks the weapon has performed
        /// </summary>
        public int AttackCounter;
    }
    public struct WeaponData: IComponentData
    {
        /// <summary>
        /// Time between shots
        /// </summary>
        public float TimeBetweenShots;
        /// <summary>
        /// Cooldown time for the weapon
        /// </summary>
        public float Cooldown;
        /// <summary>
        /// the number of attacks the weapon can perform
        /// </summary>
        public int AttackCount;
        public float Damage;
        public float Range;
        public CollisionFilter CollisionFilter;
    }

    public struct WeaponActiveFlag : IComponentData, IEnableableComponent {}
    
    public struct WeaponProjectilePrefab : IComponentData
    {
        public Entity Prefab;
    }
    
    [UpdateInGroup(typeof(AttackSystemGroup))]
    public partial struct WeaponActivationSystem : ISystem
    {
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
                // var cooldownModifier = SystemAPI.GetComponent<CharacterStatModificationState>(parent.Value).AttackCooldown;
                weaponState.ValueRW.CooldownTimer = weaponData.ValueRO.Cooldown;// * cooldownModifier;
            }
        }
    }
}