using Unity.Burst;
using Unity.Entities;

namespace DotsShooter.Damage
{
    public partial struct DamageCooldownSystem : ISystem 
    {

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var cooldownBuffer in SystemAPI.Query<DynamicBuffer<DamageSourceCooldown>>())
            {
                for (int i = cooldownBuffer.Length - 1; i >= 0; i--)
                {
                    var cooldown = cooldownBuffer[i];
                    cooldown.CooldownTimer -= SystemAPI.Time.DeltaTime;
                    if (cooldown.CooldownTimer <= 0)
                    {
                        cooldownBuffer.RemoveAt(i);
                    }
                    else
                    {
                        cooldownBuffer.ElementAt(i) = cooldown;
                    }
                }
            }
        }
    }
}