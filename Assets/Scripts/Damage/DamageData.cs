using Unity.Entities;

namespace DotsShooter.Damage
{
    public struct DamageData : IBufferElementData
    {
        public float Damage;
        public Entity Source;
    }
    
    public struct DamageSourceCooldown : IBufferElementData
    {
        public Entity Source;         // The entity causing damage
        public float CooldownTimer;   // Current cooldown time remaining
    }
    
    public struct DamageCooldownComponent : IComponentData
    {
        public float CooldownTime;    // The cooldown time for the damage source
    }
}