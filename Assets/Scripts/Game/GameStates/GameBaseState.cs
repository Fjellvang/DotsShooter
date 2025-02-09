using DotsShooter.State.Hierarchical;

namespace DotsShooter
{
    public abstract class GameBaseState : HierarchicalBaseState<StateMessageType, GameStateMachine>
    {
        public GameBaseState(GameStateMachine stateMachine) : base(stateMachine)
        {
        }
    }
}