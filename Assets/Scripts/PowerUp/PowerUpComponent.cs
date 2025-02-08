using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.PowerUp
{
    public struct PowerUpComponent : IComponentData
    {
        // public PowerUpType Type;
        public NativeQueue<PowerUp> PendingPowerUps;
    }

    public struct PowerUp
    {
        public PowerUpType Type;
    }

    public enum PowerUpType
    {
        AttackSpeed,
        MovementSpeed,
        Damage,
        ExplosionRadius,
    }
}