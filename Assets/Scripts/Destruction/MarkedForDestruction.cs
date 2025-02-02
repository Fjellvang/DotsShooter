using Unity.Entities;

namespace DotsShooter.Destruction
{
    /// <summary>
    /// Enables the entity to be marked for destruction.
    /// Mulitple systems can mark an entity for destruction, the DestroySystem will mark it DestroyNextFrame.
    /// </summary>
    public struct MarkedForDestruction : IComponentData, IEnableableComponent
    {
        
    }
}