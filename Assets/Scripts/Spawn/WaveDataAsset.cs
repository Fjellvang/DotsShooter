using UnityEngine;

namespace DotsShooter
{
    [CreateAssetMenu(fileName = "WaveData", menuName = "DotsShooter/WaveData")]
    public class WaveDataAsset : ScriptableObject
    {
        public EnemyData[] Enemies;
        public float SpawnTime;
    }
}