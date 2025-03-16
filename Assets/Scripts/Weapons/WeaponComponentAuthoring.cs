using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;

namespace DotsShooter.Weapons
{
    public class WeaponComponentAuthoring : MonoBehaviour
    {
        public float TimeBetweenShots;
        public GameObject ProjectilePrefab;
        public int AttackCount;
        public float Damage;
        public float Range;
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
                            BelongsTo = authoring.BelongsTo.Value,
                            CollidesWith = authoring.CollidesWith.Value
                        }
                    });
                AddComponent(entity, new WeaponActiveFlag());
                AddComponent(entity, new WeaponProjectilePrefab 
                    { Prefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic) }
                );
            }
        }
    }
}