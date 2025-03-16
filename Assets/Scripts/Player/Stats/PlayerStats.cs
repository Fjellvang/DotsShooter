using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Player
{
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Player/PlayerStats")]
    public class PlayerStats : ScriptableObject
    {
        public float MoveSpeed = 5f;
        // [Tooltip("The cooldown between shots in seconds")]
        // public float AttackSpeed = 1f;
        public float Damage = 1f;
        public float Health = 100f; //TODO: Implement health system
        public float Range = 10f;
        public float ExplosionRadius = 0.25f;
    }

    public struct PlayerStatModifications : IComponentData
    {
        /// <summary>
        /// Cooldown reduction percentage.
        /// </summary>
        public float CooldownMultiplier;
        public int ExtraProjectiles;
        /// <summary>
        /// Percentage increase or decrease in damage.
        /// </summary>
        public float DamageMultiplier;
        public float RangeMultiplier;
        public float AreaOfEffectMultiplier;
        public float MoveSpeedMultiplier;
    }
}