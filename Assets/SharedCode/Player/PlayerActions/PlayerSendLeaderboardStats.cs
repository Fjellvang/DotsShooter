using Metaplay.Core.Model;

namespace Game.Logic.PlayerActions
{
    [ModelAction(ActionCodes.PlayerSendLeaderboardStats)]
    public class PlayerSendLeaderboardStats : PlayerAction
    {
        public int Kills { get; private set; }
        public int Gold { get; private set; }
        public int RoundsCompleted { get; private set; }

        [MetaDeserializationConstructor]
        public PlayerSendLeaderboardStats(int kills, int gold, int roundsCompleted)
        {
            Kills = kills;
            Gold = gold;
            RoundsCompleted = roundsCompleted;
        }
        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
            {
                player.ServerListener.OnCollectLeaderboardStats(Kills, Gold, RoundsCompleted);
            }

            return ActionResult.Success;
        }
    }
}