using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public class SpawnExplosionVfxAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        public class SpawnExplosionVfxBaker : Baker<SpawnExplosionVfxAuthoring>
        {
            public override void Bake(SpawnExplosionVfxAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity,
                    new SpawnExplosionVfx { Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Renderable) });
            }
        }
    }
}