using System;
using Metaplay.Core.Math;
using Metaplay.Core.Model;

namespace Game.Logic.PlayerActions
{
    [MetaSerializable]
    public enum PlayerStat
    {
        MoveSpeed = 0,
        AttackSpeed = 10,
        Damage = 20,
        Health = 30,
        Range = 40,
        ExplosionRadius = 50
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
            //TODO: Gold cost should be dynamic and determined by gameconfig. For now it works
            if (player.Gold < 10)
            {
                return ActionResult.NotEnoughGold;
            }

            if (commit)
            {
                player.Gold -= 10;
                //TODO: eh, not super clean, double switching. Works for now.
                switch (Stat)
                {
                    case PlayerStat.MoveSpeed:
                        player.GameStats.MoveSpeed = CalculateNewStatValue(Stat, player.GameStats.MoveSpeed);
                        break;
                    case PlayerStat.AttackSpeed:
                        player.GameStats.AttackSpeed = CalculateNewStatValue(Stat, player.GameStats.AttackSpeed);
                        break;
                    case PlayerStat.Damage:
                        player.GameStats.Damage = CalculateNewStatValue(Stat, player.GameStats.Damage);
                        break;
                    case PlayerStat.Health:
                        player.GameStats.Health = CalculateNewStatValue(Stat, player.GameStats.Health);
                        break;
                    case PlayerStat.Range:
                        player.GameStats.Range = CalculateNewStatValue(Stat, player.GameStats.Range);
                        break;
                    case PlayerStat.ExplosionRadius:
                        player.GameStats.ExplosionRadius = CalculateNewStatValue(Stat, player.GameStats.ExplosionRadius);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                player.ClientListener.OnStatUpdated();
            }

            return ActionResult.Success;
        }
        
        //TODO: This is a very simple example, we should move this to game config, and reconsider the rules
        private static F64 CalculateNewStatValue(PlayerStat stat, F64 current)
        {
            return stat switch
            {
                PlayerStat.MoveSpeed => current + F64.FromDouble(1),
                PlayerStat.AttackSpeed => current * F64.FromDouble(0.9),
                PlayerStat.Damage => current + F64.FromDouble(1),
                PlayerStat.Health => current * F64.FromDouble(1.1),
                PlayerStat.Range => current + F64.FromDouble(1),
                PlayerStat.ExplosionRadius => current * F64.FromDouble(1.05),
                _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null)
            };
        }
    }
}