// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using Metaplay.Core.Player;

namespace Game.Logic
{
    /// <summary>
    /// Game-specific player action class, which attaches all game-specific actions to <see cref="PlayerModel"/>.
    /// </summary>
    public abstract class PlayerAction : PlayerActionCore<PlayerModel>
    {
    }

    /// <summary>
    /// Registry for game-specific ActionCodes, used by the individual PlayerAction classes.
    /// </summary>
    public static class ActionCodes
    {
        public const int PlayerAddGold = 5000;
    }

    /// <summary>
    /// Game-specific results returned from <see cref="PlayerActionCore.Execute(PlayerModel, bool)"/>.
    /// </summary>
    public static class ActionResult
    {
        // Shadow success result
        public static readonly MetaActionResult Success             = MetaActionResult.Success;

        // Game-specific results
        public static readonly MetaActionResult UnknownError        = new MetaActionResult(nameof(UnknownError));
    }

    // Game-specific example action: bump PlayerModel.NumClicked, triggered by button tap

    [ModelAction(ActionCodes.PlayerAddGold)]
    public class PlayerAddGold : PlayerAction
    {
        public int Amount { get; private set; }
        public PlayerAddGold() { Amount = 1; }
        public PlayerAddGold(int amount) //TODO: not very safe to let client set amount
        {
            Amount = amount;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
            {
                player.Gold += 1;
                player.Log.Info("Button clicked!");
            }

            return ActionResult.Success;
        }
    }
}
