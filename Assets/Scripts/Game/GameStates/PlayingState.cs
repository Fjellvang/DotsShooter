namespace DotsShooter
{
    public class PlayingState : GameBaseState 
    {
        public PlayingState(GameStateMachine stateMachine) : base(stateMachine)
        {
        }

        public override void OnEnter()
        {
            SetSubState(StateMachine.ArenaState);
        }

        public override void HandleMessage<T>(T message)
        {
            switch (message)
            {
                case RoundCompletedMessage roundComplete:
                    SetSubState(StateMachine.ShopState);
                    break;
        
                case ShopCompletedMessage shopComplete:
                    SetSubState(StateMachine.ArenaState.IncreaseRound(5f));
                    break;
        
                case PlayerDeathMessage playerDeath:
                    SendMessageToParent(playerDeath); // Pass up to game state
                    break;
        
                default:
                    base.HandleMessage(message);
                    break;
            }
        }

    }
}