using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public class GameStateComponentAuthoring : MonoBehaviour
    {
        /// <summary>
        /// Duration of the game in seconds
        /// </summary>
        public float GameTime = 60f;
        public class GameStateComponentBaker : Baker<GameStateComponentAuthoring>
        {
            public override void Bake(GameStateComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GameStateComponent
                {
                    Round = 1,
                    GameTime = authoring.GameTime,
                    GameEnded = false,
                });
                AddComponent<GameStateNeedInitializationComponent>(entity);
            }
        }
    }
}