using Metaplay.Core.Model;

namespace Game.Logic.PlayerActions
{
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
                player.Gold += Amount;
                player.ClientListener.OnGoldAdded();
            }

            return ActionResult.Success;
        }
    }
}