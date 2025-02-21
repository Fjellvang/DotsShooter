using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public class GameStateComponentAuthoring : MonoBehaviour
    {
        public class GameStateComponentBaker : Baker<GameStateComponentAuthoring>
        {
            public override void Bake(GameStateComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GameStateComponent {Round = 1});
                AddComponent<GameStateNeedInitializationComponent>(entity);
            }
        }
    }
}