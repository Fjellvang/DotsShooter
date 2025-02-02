using Unity.Entities;

namespace DotsShooter.Destruction
{
    /// <summary>
    /// Enables the entity to be marked for destruction.
    /// This allows other systems to react to entities dying - spawning particles, playing sounds, etc.
    /// </summary>
    public struct DestroyNextFrame : IComponentData, IEnableableComponent
    {
        
    }
}