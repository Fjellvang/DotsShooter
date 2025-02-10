using System;

namespace DotsShooter.State
{
    /// <inheritdoc/>
    public class StateMachine : StateMachine<IState>
    {
    }
    /// <summary>
    /// A generic state machine that can be used to control the state of an object of type T.
    /// </summary>
    [Serializable]
    public class StateMachine<T>
        where T : IState
    {
        /// <summary>
        /// The current state of the state machine.
        /// </summary>
        public T CurrentState { get; private set; }

        /// <summary>
        /// Event that is invoked when the state changes.
        /// </summary>
        public event Action<IState> OnStateChanged;

        /// <summary>
        /// Initialize the state machine with a starting state.
        /// </summary>
        /// <param name="state">The starting state</param>
        public void Initialize(T state)
        {
            CurrentState = state;
            state.OnEnter();

            // Notify listeners that state has changed
            OnStateChanged?.Invoke(state);
        }

        /// <summary>
        /// Transition to a new state by calling Exit() on the current state and Enter() on the next state.
        /// </summary>
        /// <param name="nextState">The state to transition to</param>
        public void TransitionTo(T nextState)
        {
            CurrentState?.OnExit();
            CurrentState = nextState;
            nextState.OnEnter();

            // Notify other objects that state has changed
            OnStateChanged?.Invoke(nextState);
        }

        /// <summary>
        /// Update the current state.
        /// </summary>
        public void Update()
        {
            if (CurrentState != null)
            {
                CurrentState.OnUpdate();
            }
        }
    }
}