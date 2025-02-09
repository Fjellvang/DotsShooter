using System;
using DotsShooter.State.Hierarchical;

namespace DotsShooter
{
    public class GameStateMachine : HierarchicalStateMachine<StateMessageType>
    {
        public GameOverState GameOverState => new(this);
        public PlayingState PlayingState => new(this);
        public ShopState ShopState => new(this);
        public ArenaState ArenaState => new(this, 1, 30.0f);
        public override void HandleMessage<T>(T message)
        {
            switch (message)
            {
                case PlayerDeathMessage playerDeath:
                    TransitionTo(GameOverState);
                    break;
        
                default:
                    throw new InvalidOperationException($"Message not handled!: {nameof(T)}");
                    break;
            }
        }
    }
}