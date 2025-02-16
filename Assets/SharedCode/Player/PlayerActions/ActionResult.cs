// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;

namespace Game.Logic.PlayerActions
{
    /// <summary>
    /// Game-specific results returned from <see cref="PlayerActionCore.Execute(PlayerModel, bool)"/>.
    /// </summary>
    public static class ActionResult
    {
        // Shadow success result
        public static readonly MetaActionResult Success             = MetaActionResult.Success;
        // Game-specific results
        public static readonly MetaActionResult UnknownError        = new MetaActionResult(nameof(UnknownError));
        public static readonly MetaActionResult NotEnoughGold       = new MetaActionResult(nameof(NotEnoughGold));
    }
}
