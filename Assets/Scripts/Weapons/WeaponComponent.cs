using Unity.Entities;
using Unity.Physics;

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
        public float AreaOfEffectRadius;
        /// <summary>
        /// 0-1 value representing the accuracy of the weapon, 1 being 100% accurate
        /// </summary>
        public float WeaponAccuracy;
        public CollisionFilter CollisionFilter;
    }

    public struct WeaponActiveFlag : IComponentData, IEnableableComponent {}
    
    public struct WeaponProjectilePrefab : IComponentData
    {
        public Entity Prefab;
    }
}