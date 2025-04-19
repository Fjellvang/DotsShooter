using DotsShooter.Destruction;
using DotsShooter.Health;
using DotsShooter.SimpleCollision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace DotsShooter.Damage
{
    [BurstCompile]
    [UpdateAfter(typeof(SimpleCollisionSystem))]
    public partial struct DamageOnCollisionSystem : ISystem
    {
        private BufferLookup<DamageData> _bufferLookup;
        private BufferLookup<DamageSourceCooldown> _damageCooldownLookup;
        // private EntityQuery _damageQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _bufferLookup = state.GetBufferLookup<DamageData>();
            _damageCooldownLookup = state.GetBufferLookup<DamageSourceCooldown>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _bufferLookup.Update(ref state);
            _damageCooldownLookup.Update(ref state);
            var markedForDestructionLookup = SystemAPI.GetComponentLookup<DestroyNextFrameTag>();
            var enemyTagLookup = SystemAPI.GetComponentLookup<EnemyTag>();
            var healthLookup = SystemAPI.GetComponentLookup<DamageCooldownComponent>();

            foreach (var (damage, simpleCollisionBuffer, entity) in 
                     SystemAPI.Query<RefRO<DamageOnCollision>, DynamicBuffer<SimpleCollisionEvent>>()
                         .WithEntityAccess())
            {
                for (int i = 0; i < simpleCollisionBuffer.Length; i++)
                {
                    var simpleCollisionEvent = simpleCollisionBuffer[i];
                    var other = simpleCollisionEvent.GetOtherEntity(entity);
                    
                    // HACK: This is a hack to prevent enemies from damaging each other
                    if (enemyTagLookup.HasComponent(other) && enemyTagLookup.HasComponent(entity))
                    {
                        continue;
                    }
                    
                    if (_bufferLookup.HasBuffer(other)) {
                        // Check if the damage cooldown is active
                        bool canTakeDamage = true;
                        if (_damageCooldownLookup.HasBuffer(other))
                        {
                            var cooldownBuffer = _damageCooldownLookup[other];
                            canTakeDamage = !IsSourceInCooldown(cooldownBuffer, entity);
                            if (canTakeDamage)
                            {
                                // Add the cooldown to the buffer
                                cooldownBuffer.Add(new DamageSourceCooldown()
                                {
                                    Source = entity,
                                    CooldownTimer = healthLookup[other].CooldownTime
                                });
                            }
                        }

                        if (canTakeDamage)
                        {
                            _bufferLookup[other].Add(new DamageData()
                            {
                                Damage = damage.ValueRO.Damage,
                                Source = entity,
                            });
                        }
                    }

                    if (damage.ValueRO.DestroyOnCollision)
                    {
                        markedForDestructionLookup.SetComponentEnabled(entity, true);
                    }
                }
            }
        }

        // Helper method to check if a source is in cooldown
        private static bool IsSourceInCooldown(in DynamicBuffer<DamageSourceCooldown> cooldownBuffer, Entity source)
        {
            for (int i = 0; i < cooldownBuffer.Length; i++)
            {
                if (cooldownBuffer[i].Source == source)
                {
                    return true;
                }
            }
            return false;
        }
    }
}