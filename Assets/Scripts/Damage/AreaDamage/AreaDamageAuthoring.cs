using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Damage.AreaDamage
{
    public struct AreaDamage : IComponentData
    {
        public float Damage;
        public float Radius;
    }

    public class AreaDamageAuthoring : MonoBehaviour
    {
        public float Radius = 5;
        public float Damage = 10;
        
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
                    });
            }
        }
    }
}