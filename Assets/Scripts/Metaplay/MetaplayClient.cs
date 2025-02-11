using Game.Logic;
using Metaplay.Unity.DefaultIntegration;

namespace DotsShooter.Metaplay
{
    /// <summary>
    /// Declare concrete MetaplayClient class for type-safe access to player context from game code.
    /// </summary>
    public class MetaplayClient : MetaplayClientBase<PlayerModel>
    {
    }
}