using DotsShooter.State;
using DotsShooter.State.Hierarchical;
using UnityEngine;

namespace DotsShooter
{
    [CreateAssetMenu(fileName = "GameStateTracker", menuName = "Game/GameStateTracker")]
    public class GameStateTracker : ScriptableObject
    {
        private GameStateMachine _stateMachine;
    }


}