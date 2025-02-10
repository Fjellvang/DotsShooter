using Unity.Mathematics;

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
            if (Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnPlayerDied += TransitionToGameOver;
            }
        }
        
        public override void OnExit()
        {
            // Unsubscribe from dead and win events
            if (Helpers.TryGetEventSystem(out var eventSystem))
            {
                eventSystem.OnPlayerDied -= TransitionToGameOver;
            }
        }
        
       
        private void TransitionToShop()
        {
            SendMessageToParent(new RoundCompletedMessage());
        }
        
        private void TransitionToGameOver(float3 position)
        {
            SendMessageToParent(new PlayerDeathMessage());
        }
    }
}