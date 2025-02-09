using System;
using System.Collections.Generic;

namespace DotsShooter.State.Hierarchical
{
    public interface IHierarchicalState<TMessageType> : IState where TMessageType : Enum
    {
        IHierarchicalState<TMessageType> ParentState { get; set; }
        List<IHierarchicalState<TMessageType>> SubStates { get; }
        IHierarchicalState<TMessageType> CurrentSubState { get; }
    
        void SetSubState(IHierarchicalState<TMessageType> state);
        void HandleMessage<T>(T message) where T : struct, IStateMessage<TMessageType>; // use struct to avoid boxing
    }
    
    
    public interface IStateMessage<out TMessageType> where TMessageType : Enum
    {
        TMessageType Type { get; }
    }
    
    public enum TestMessageType
    {
        TestMessage1,
        TestMessage2
    }
    
    public readonly struct TestMessage : IStateMessage<TestMessageType>
    {
        public readonly TestMessageType Type { get; }
    
        public TestMessage(TestMessageType type)
        {
            Type = type;
        }
    }
}