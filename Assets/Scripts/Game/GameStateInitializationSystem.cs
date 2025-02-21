using Unity.Entities;

namespace DotsShooter
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class GameStateInitializationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<GameStateNeedInitializationComponent>();
        }

        protected override void OnUpdate()
        {
            var round = GameManager.Instance.GetCurrentRound();
            SystemAPI.TryGetSingletonEntity<GameStateComponent>(out var entity);
            var gameStateComponent = SystemAPI.GetSingletonRW<GameStateComponent>();
            gameStateComponent.ValueRW.Round = round;

            // Might be better with ecb, but its such a small operation that it should be okay...
            EntityManager.RemoveComponent<GameStateNeedInitializationComponent>(entity);
            EntityManager.AddComponent<GameStateInitializedComponent>(entity);
        }
    }
}