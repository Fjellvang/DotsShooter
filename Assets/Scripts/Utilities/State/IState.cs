namespace DotsShooter.State
{
    /// <summary>
    /// Interface for a state in a state machine.
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// Code that runs when we enter the state.
        /// </summary>
        void OnEnter();

        /// <summary>
        /// Code that runs every frame while we are in the state. Should include a condition to transition to a new state.
        /// </summary>
        void OnUpdate();

        /// <summary>
        /// Code that runs when we exit the state.
        /// </summary>
        void OnExit();
    }
}