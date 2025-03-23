using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public class SpawnOnDeathComponentAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        public class SpawnEnemyOnDeathComponentBaker : Baker<SpawnOnDeathComponentAuthoring>
        {
            public override void Bake(SpawnOnDeathComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new SpawnOnDeathComponent
                    {
                        Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic)
                    });
                AddComponent<DisableSpawnOnDeathFlag>(entity);
                SetComponentEnabled<DisableSpawnOnDeathFlag>(entity, false);
            }
        }
    }
    /// <summary>
    /// Flag to indicate whether an entity should spawn a prefab when it dies.
    /// useful when we're clearing the map, then we want to disable spawning rewards.
    /// </summary>
    public struct DisableSpawnOnDeathFlag : IComponentData, IEnableableComponent
    {
    }
}