using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Damage
{
    public struct DamageOnCollision : IComponentData
    {
        public int Damage;
        public bool DestroyOnCollision;
    }
    
    public class DamageOnCollisionAuthoring : MonoBehaviour
    {
        public int Damage;
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
                        DestroyOnCollision = authoring.DestroyOnCollision
                    });
            }
        }
    }
}