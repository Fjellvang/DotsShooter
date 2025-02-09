using System;
using System.Collections.Generic;

namespace DotsShooter.State.Hierarchical
{
    public abstract class HierarchicalBaseState<TMessageEnum, TStateMachineType> : IHierarchicalState<TMessageEnum> 
        where TMessageEnum : Enum 
        where TStateMachineType : HierarchicalStateMachine<TMessageEnum>
    {
        protected readonly TStateMachineType StateMachine;

        public HierarchicalBaseState(TStateMachineType stateMachine)
        {
            this.StateMachine = stateMachine;
        }
        
        public IHierarchicalState<TMessageEnum> ParentState { get; set; }
        public List<IHierarchicalState<TMessageEnum>> SubStates { get; } = new();
        public IHierarchicalState<TMessageEnum> CurrentSubState { get; private set; }

        public virtual void OnEnter() { }
        public virtual void OnUpdate() 
        {
            // Update current sub-state if it exists
            CurrentSubState?.OnUpdate();
        }
        public virtual void OnExit() { }

        public virtual void SetSubState(IHierarchicalState<TMessageEnum> state)
        {
            if (CurrentSubState != null)
            {
                CurrentSubState.OnExit();
            }

            CurrentSubState = state;
            state.ParentState = this;
        
            if (!SubStates.Contains(state))
            {
                SubStates.Add(state);
            }

            state.OnEnter();
        }

        // Default behavior is to pass message up to parent
        public virtual void HandleMessage<T>(T message) where T : struct, IStateMessage<TMessageEnum>
        {
            SendMessageToParent(message);
        }

        // Send message up the hierarchy until someone handles it
        protected void SendMessageToParent<T>(T message) where T : struct, IStateMessage<TMessageEnum>
        {
            if (ParentState != null)
            {
                ParentState.HandleMessage(message);
            }
            else
            {
                // If we have no parent, send to state machine
                StateMachine.HandleMessage(message);
            }
        }
    }
}