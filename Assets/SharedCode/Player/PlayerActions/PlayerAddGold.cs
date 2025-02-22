using Game.Logic.GameConfigs;
using Metaplay.Core.Model;

namespace Game.Logic.PlayerActions
{
    [ModelAction(ActionCodes.PlayerAddGold)]
    public class PlayerAddGold : PlayerAction
    {
        public CoinType Type { get; private set; }
        public PlayerAddGold() { Type = CoinType.Small; }
        public PlayerAddGold(CoinType type) //TODO: not very safe to let client set amount
        {
            Type = type;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            var config = player.GameConfig.CoinValueConfiguration[Type];

            if (commit)
            {
                player.Gold += config.Value;
                player.ClientListener.OnGoldAdded();
            }

            return ActionResult.Success;
        }
    }
}