using DotsShooter.Damage;
using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Health
{
    public struct HealthComponent : IComponentData
    {
        public float Health;
        public float MaxHealth;
    }

    public class HealthComponentAuthoring : MonoBehaviour
    {
        [SerializeField] 
        private float health = 100;
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
            }
        }
    }
}