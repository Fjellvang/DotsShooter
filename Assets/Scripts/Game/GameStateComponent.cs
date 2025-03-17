using Unity.Entities;

namespace DotsShooter
{
    public struct GameStateComponent : IComponentData
    {
        public int Round;
        public float GameTime;
        public bool GameEnded;
    }
    
    public struct GameStateInitializedComponent : IComponentData
    {
    }
    
    public struct GameStateNeedInitializationComponent : IComponentData
    {
    }
    
    public struct GameEndedFlag : IComponentData { }
}