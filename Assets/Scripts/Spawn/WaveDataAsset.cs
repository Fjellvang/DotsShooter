using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter
{
    [CreateAssetMenu(fileName = "WaveData", menuName = "DotsShooter/WaveData")]
    public class WaveDataAsset : ScriptableObject
    {
        public EnemyData[] Enemies;
        [FormerlySerializedAs("SpawnTime")] 
        public float TimeBetweenWaves;
        public WaveSpawnEventAsset[] WaveSpawnEvents;
    }
    
    [Serializable]
    public class EnemyData
    {
        public GameObject Prefab;
        public int Weight; 
    }
}