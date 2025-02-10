using DotsShooter.State;
using DotsShooter.State.Hierarchical;
using UnityEngine;

namespace DotsShooter
{
    [CreateAssetMenu(fileName = "GameStateTracker", menuName = "Game/GameStateTracker")]
    public class GameStateTracker : ScriptableObject
    {
        private GameStateMachine _stateMachine;
        
        public void Initialize()
        {
            _stateMachine = new GameStateMachine();
            _stateMachine.Initialize(_stateMachine.GameOverState); // Start in game over state
        }
        
        public void Update()
        {
            _stateMachine.Update();
        }
        
        public void StartGame()
        {
            _stateMachine.TransitionTo(_stateMachine.PlayingState);
        }
    }
}