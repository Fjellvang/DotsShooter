using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter
{
    public struct AutoShootingComponent : IComponentData
    {
        public float Cooldown;
        public float CooldownTimer;
        public Entity ProjectilePrefab;
        public float ProjectileSpeed;
        public float ProjectileDamage;
        public float ProjectileRadius;
        public float SpawnOffset;
    }
    public class AutoShootingComponentAuthoring : MonoBehaviour
    
    {
        [SerializeField]
        private float cooldown;
        [SerializeField]
        private GameObject projectilePrefab; // TODO Maybe this should be kept in a datastore and a buffer
        [SerializeField]
        private float projectileSpeed;
        [SerializeField]
        private float spawnOffset;
        [SerializeField]
        private float projectileDamage = 5;
        [SerializeField]
        private float projectileRadius = 0.5f;

        public class AutoShootingComponentBaker : Baker<AutoShootingComponentAuthoring>
        {
            public override void Bake(AutoShootingComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new AutoShootingComponent
                    {
                        Cooldown = authoring.cooldown,
                        ProjectileSpeed = authoring.projectileSpeed,
                        SpawnOffset = authoring.spawnOffset,
                        ProjectilePrefab = GetEntity(authoring.projectilePrefab, TransformUsageFlags.Dynamic),
                        ProjectileDamage = authoring.projectileDamage,
                        ProjectileRadius = authoring.projectileRadius
                    });
            }
        }
    }
}