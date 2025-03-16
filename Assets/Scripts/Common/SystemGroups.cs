using Unity.Entities;

namespace DotsShooter.Common
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(EffectsSystemGroup))]
    public partial class AttackSystemGroup : ComponentSystemGroup
    {
        
    }

    /// <summary>
    /// System group containing systems that deal with triggering visual and audio effects.
    /// </summary>
    /// <remarks>
    /// Updates at the end of Unity's main SimulationSystemGroup to ensure any effects that need to be triggered are already known about (typically via enableable component) before executing effects systems. Updates before the <see cref="DestructionSystemGroup"/> to ensure an entity isn't destroyed before it plays necessary effects for the current frame.
    /// </remarks>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DestructionSystemGroup))]
    public partial class EffectsSystemGroup : ComponentSystemGroup
    {
        
    }
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class DestructionSystemGroup : ComponentSystemGroup
    {
        
    }
}