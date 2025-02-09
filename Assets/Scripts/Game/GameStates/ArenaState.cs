namespace DotsShooter
{
    public class ArenaState : GameBaseState
    {
        private int _round;
        private float _duration;

        public ArenaState(GameStateMachine stateMachine, int round, float duration) : base(stateMachine)
        {
            _round = round;
            _duration = duration;
        }
        
        public ArenaState IncreaseRound(float durationIncrease)
        {
            _round++;
            _duration += durationIncrease;
            return this;
        }
        public override void OnEnter()
        {
            // Subscribe to dead and win events
        }
        
        public override void OnExit()
        {
            // Unsubscribe from dead and win events
        }
        
       
        private void TransitionToShop()
        {
            SendMessageToParent(new RoundCompletedMessage());
        }
        
        private void TransitionToGameOver()
        {
            SendMessageToParent(new PlayerDeathMessage());
        }
    }
}