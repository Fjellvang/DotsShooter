using DotsShooter.Metaplay;
using Game.Logic.GameConfigs;
using UnityEngine;

namespace DotsShooter
{
    public class PlayerStatisticsTracker 
    {
        public int Kills { get; private set; }
        public int GoldCollected { get; private set; }
        public int RoundsCompleted { get; private set; }
        
        public void IncreaseKills() => Kills++;

        public void IncreaseGoldCollected(CoinType type)
        {
            // maybe a bit too coupled on metaplay, but it works for now.
            var value = MetaplayClient.PlayerModel.GameConfig.CoinValueConfiguration[type].Value;
            GoldCollected += value;
        }
        public void IncreaseRoundsCompleted() => RoundsCompleted++;
        
        public void Reset()
        {
            Kills = 0;
            GoldCollected = 0;
            RoundsCompleted = 0;
        }
    }
}