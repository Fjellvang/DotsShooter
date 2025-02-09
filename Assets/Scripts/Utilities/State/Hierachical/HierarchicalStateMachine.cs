using System;
using System.Collections.Generic;

namespace DotsShooter.State.Hierarchical
{
    public abstract class HierarchicalStateMachine<TMessageEnum> where TMessageEnum : Enum
    {
        private IHierarchicalState<TMessageEnum> _rootState;

        public void Initialize(IHierarchicalState<TMessageEnum> state)
        {
            _rootState = state;
            state.OnEnter();
        }

        public void TransitionTo(IHierarchicalState<TMessageEnum> newState)
        {
            // Find common ancestor
            var commonAncestor = FindCommonAncestor(_rootState, newState);
        
            // Exit all states up to common ancestor
            var currentState = _rootState;
            while (currentState != commonAncestor)
            {
                currentState.OnExit();
                currentState = currentState.ParentState;
            }

            // Enter new state and its parents
            var statesToEnter = new Stack<IHierarchicalState<TMessageEnum>>();
            currentState = newState;
            while (currentState != commonAncestor)
            {
                statesToEnter.Push(currentState);
                currentState = currentState.ParentState;
            }

            while (statesToEnter.Count > 0)
            {
                var state = statesToEnter.Pop();
                state.OnEnter();
            }

            _rootState = newState;
        }

        public void Update()
        {
            _rootState?.OnUpdate();
        }

        private IHierarchicalState<TMessageEnum> FindCommonAncestor(IHierarchicalState<TMessageEnum> state1, IHierarchicalState<TMessageEnum> state2)
        {
            var ancestors = new HashSet<IHierarchicalState<TMessageEnum>>();
            var current = state1;
        
            while (current != null)
            {
                ancestors.Add(current);
                current = current.ParentState;
            }

            current = state2;
            while (current != null)
            {
                if (ancestors.Contains(current))
                {
                    return current;
                }
                current = current.ParentState;
            }

            return null;
        }

        public abstract void HandleMessage<T>(T message) where T : struct, IStateMessage<TMessageEnum>;
    }
}