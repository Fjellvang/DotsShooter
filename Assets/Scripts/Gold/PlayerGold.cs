using System;
using UnityEngine;

namespace DotsShooter.Gold
{
    [CreateAssetMenu(fileName = "PlayerGold", menuName = "Player/PlayerGold")]
    public class PlayerGold : ScriptableObject
    {
        public event Action OnGoldChanged;
        
        [SerializeField] // Serialized, so we can inspect it in the editor. Also, we can set it to a default value.
        private int _gold;

        public int Gold
        {
            get => _gold;
            set
            {
                _gold = value;
                OnGoldChanged?.Invoke();
            }
        }
    }
}