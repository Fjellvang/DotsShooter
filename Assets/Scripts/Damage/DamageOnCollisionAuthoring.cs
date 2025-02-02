using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Damage
{
    public struct DamageOnCollision : IComponentData
    {
        public float Damage;
        public float Radius;
        public bool DestroyOnCollision;
    }
    
    public class DamageOnCollisionAuthoring : MonoBehaviour
    {
        public float Damage;
        public float Radius;
        public bool DestroyOnCollision;

        public class DamageOnCollisionBaker : Baker<DamageOnCollisionAuthoring>
        {
            public override void Bake(DamageOnCollisionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new DamageOnCollision
                    {
                        Damage = authoring.Damage, 
                        Radius = authoring.Radius,
                        DestroyOnCollision = authoring.DestroyOnCollision
                    });
            }
        }
    }
}