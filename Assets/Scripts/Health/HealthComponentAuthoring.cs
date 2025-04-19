using DotsShooter.Damage;
using DotsShooter.Destruction;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Health
{
    public struct HealthComponent : IComponentData
    {
        public float Health;
        public float MaxHealth;
    }

    [RequireComponent(typeof(DestroyableAuthor))]
    public class HealthComponentAuthoring : MonoBehaviour
    {
        [SerializeField] 
        private float health = 100;
        [SerializeField, Tooltip("Cooldown time between damage events, especially useful for melee attacks")]
        private float DamageCooldown = 0.5f;
        public class HealthComponentBaker : Baker<HealthComponentAuthoring>
        {
            public override void Bake(HealthComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new HealthComponent()
                {
                    Health = authoring.health,
                    MaxHealth = authoring.health
                });
                AddBuffer<DamageData>(entity);
                AddBuffer<DamageSourceCooldown>(entity);
                AddComponent(entity, new DamageCooldownComponent()
                {
                    CooldownTime = authoring.DamageCooldown
                });
            }
        }
    }
}