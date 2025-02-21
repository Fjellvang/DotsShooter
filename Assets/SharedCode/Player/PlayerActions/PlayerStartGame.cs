using Metaplay.Core.Model;

namespace Game.Logic.PlayerActions
{
    [ModelAction(ActionCodes.PlayerStartGame)]
    public class PlayerStartGame : PlayerAction
    {
        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
            {
                player.Gold = 0; // Reset gold
                player.GameStats.SetInitialStats(player.GameConfig);
            }

            return ActionResult.Success;
        }
    }
}