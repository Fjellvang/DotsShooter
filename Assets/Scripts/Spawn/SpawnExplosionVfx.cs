using Unity.Entities;

namespace DotsShooter
{
    public struct SpawnExplosionVfx : IComponentData
    {
        public Entity Prefab;
    }
}