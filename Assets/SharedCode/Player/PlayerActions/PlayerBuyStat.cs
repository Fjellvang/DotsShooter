using System;
using Game.Logic.GameConfigs;
using Metaplay.Core.Math;
using Metaplay.Core.Model;

namespace Game.Logic.PlayerActions
{
    [MetaSerializable]
    public enum PlayerStat
    {
        MoveSpeed = 0,
        Cooldown = 10,
        Damage = 20,
        Health = 30,
        Range = 40,
        ExplosionRadius = 50,
        ExtraProjectile = 60,
    }
    
    [ModelAction(ActionCodes.PlayerBuyStat)]
    public class PlayerBuyStat : PlayerAction
    {
        public PlayerStat Stat { get; private set; }
        
        [MetaDeserializationConstructor]
        public PlayerBuyStat(PlayerStat stat)
        {
            Stat = stat;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            var config = player.GameConfig.ShopConfiguration[ShopStatId.FromString(Stat.ToString())];
            
            if (player.Gold < config.UpgradeCost)
            {
                return ActionResult.NotEnoughGold;
            }

            if (commit)
            {
                player.Gold -= config.UpgradeCost;
                switch (Stat)
                {
                    case PlayerStat.MoveSpeed:
                        player.GameStats.MoveSpeed = CalculateNewStatValue(config, player.GameStats.MoveSpeed);
                        break;
                    case PlayerStat.Cooldown:
                        player.GameStats.Cooldown = CalculateNewStatValue(config, player.GameStats.Cooldown);
                        break;
                    case PlayerStat.Damage:
                        player.GameStats.Damage = CalculateNewStatValue(config, player.GameStats.Damage);
                        break;
                    case PlayerStat.Health:
                        player.GameStats.Health = CalculateNewStatValue(config, player.GameStats.Health);
                        break;
                    case PlayerStat.Range:
                        player.GameStats.Range = CalculateNewStatValue(config, player.GameStats.Range);
                        break;
                    case PlayerStat.ExplosionRadius:
                        player.GameStats.ExplosionRadius = CalculateNewStatValue(config, player.GameStats.ExplosionRadius);
                        break;
                    case PlayerStat.ExtraProjectile:
                        //TODO: this is silly and a hack. 
                        player.GameStats.ExtraProjectiles = (int)CalculateNewStatValue(config, F64.FromInt(player.GameStats.ExtraProjectiles)).Double;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                player.ClientListener.OnStatUpdated();
            }

            return ActionResult.Success;
        }
        
        private static F64 CalculateNewStatValue(ShopStatConfig config, F64 current)
        {
            //TODO: introduce concept of stat level.
            return config.IncreaseOperator == PlayerStatIncreaseOperator.Add
                ? F64.FromDouble(current.Double + config.StatIncreasePerLevel)
                : F64.FromDouble(current.Double * config.StatIncreasePerLevel);
        }
    }
}