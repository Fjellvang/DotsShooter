using Unity.Entities;

namespace DotsShooter
{
    public struct SpawnOnDeathComponent : IComponentData
    {
        public Entity Prefab;
    }
}