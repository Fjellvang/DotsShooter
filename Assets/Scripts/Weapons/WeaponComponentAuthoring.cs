using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;

namespace DotsShooter.Weapons
{
    public class WeaponComponentAuthoring : MonoBehaviour
    {
        public float TimeBetweenShots = 0.25f;
        public GameObject ProjectilePrefab;
        public int AttackCount = 1;
        public float Damage = 1;
        public float Range = 25f;
        public float Cooldown = 2f;
        public float AreaOfEffectRadius = 1f;
        [Header("Collision Filter")]
        public PhysicsCategoryTags BelongsTo;
        public PhysicsCategoryTags CollidesWith;

        public class WeaponComponentBaker : Baker<WeaponComponentAuthoring>
        {
            public override void Bake(WeaponComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new WeaponData
                    {
                        TimeBetweenShots = authoring.TimeBetweenShots,
                        AttackCount = authoring.AttackCount,
                        Damage = authoring.Damage,
                        Range = authoring.Range,
                        CollisionFilter = new CollisionFilter
                        {
                            BelongsTo = authoring.BelongsTo.Value, CollidesWith = authoring.CollidesWith.Value
                        },
                        Cooldown = authoring.Cooldown,
                        AreaOfEffectRadius = authoring.AreaOfEffectRadius
                    });
                AddComponent(entity, new WeaponActiveFlag());
                SetComponentEnabled<WeaponActiveFlag>(entity, false);
                AddComponent(entity, new WeaponProjectilePrefab 
                    { Prefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic) }
                );
                AddComponent(entity, new WeaponState());
            }
        }
    }
}