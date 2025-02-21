using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct GameStateComponent : IComponentData
    {
        public int Round;
    }
    
    public struct GameStateInitializedComponent : IComponentData
    {
    }
    
    public struct GameStateNeedInitializationComponent : IComponentData
    {
    }
}