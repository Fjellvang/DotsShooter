using Unity.Entities;

namespace DotsShooter
{
    public struct SpawnEnemyOnDeathComponent : IComponentData
    {
        public Entity Prefab;
    }
}