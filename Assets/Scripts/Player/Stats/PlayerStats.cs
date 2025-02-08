using UnityEngine;

namespace DotsShooter.Player
{
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Player/PlayerStats")]
    public class PlayerStats : ScriptableObject
    {
        public float MoveSpeed = 5f;
        [Tooltip("The cooldown between shots in seconds")]
        public float AttackSpeed = 1f;
        public float Damage = 1f;
        public float Health = 100f; //TODO: Implement health system
        public float ExplosionRadius = 0.25f;
    }
}