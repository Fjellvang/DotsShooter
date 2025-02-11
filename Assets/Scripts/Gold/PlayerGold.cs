using System;
using DotsShooter.Metaplay;
using Game.Logic;
using UnityEngine;

namespace DotsShooter.Gold
{
    [CreateAssetMenu(fileName = "PlayerGold", menuName = "Player/PlayerGold")]
    public class PlayerGold : ScriptableObject
    {
        public event Action OnGoldChanged;
        

        public int Gold
        {
            // not sure this is the strongest implementation. but eh, works for now.
            get => MetaplayClient.PlayerModel.Gold;
            set
            {
                MetaplayClient.PlayerContext.ExecuteAction(new PlayerAddGold(value));
                OnGoldChanged?.Invoke();
            }
        }
    }
}