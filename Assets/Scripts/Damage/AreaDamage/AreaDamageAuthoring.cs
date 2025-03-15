using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;

namespace DotsShooter.Damage.AreaDamage
{
    public struct AreaDamage : IComponentData
    {
        public float Damage;
        public float Radius;
        public CollisionFilter CollisionFilter;
    }

    public class AreaDamageAuthoring : MonoBehaviour
    {
        public float Radius = 5;
        public float Damage = 10;
        
        public PhysicsCategoryTags BelongsTo;
        public PhysicsCategoryTags CollidesWith;
        
        private class AreaDamageBaker : Baker<AreaDamageAuthoring>
        {
            public override void Bake(AreaDamageAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new AreaDamage
                    {
                        Damage = authoring.Damage, 
                        Radius = authoring.Radius,
                        CollisionFilter = new CollisionFilter()
                        {
                            BelongsTo = authoring.BelongsTo.Value,
                            CollidesWith = authoring.CollidesWith.Value
                        }
                    });
            }
        }
    }
}