using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsShooter
{
    /// <summary>
    /// Asset Describing a spawn event, which is a determinictic event that spawns a wave of enemies at a specific time.
    /// </summary>
    [CreateAssetMenu(fileName = "SpawnFormationAsset", menuName = "DotsShooter/SpawnFormationAsset")]
    public class SpawnFormationAsset : ScriptableObject
    {
        public SpawnFormation Formation;
        [Tooltip("Number of enemies to spawn.")]
        public int Count = 5;
        [Tooltip("Spacing between enemies.")]
        public float Spacing = 1;
        public float InitialSpawnX;
        public float InitialSpawnY;
        [FormerlySerializedAs("FormationData")] [SerializeReference]
        public SpawnFormationAssetData formationAssetData = new CircleFormationAssetData();
    }

    [Serializable]
    public class SpawnFormationAssetData { }
    
    [Serializable]
    public class LineFormationAssetData : SpawnFormationAssetData
    {
        public Direction MovementDirection; 
        public Direction AlignmentDirection;
    }
    [Serializable]
    public class CircleFormationAssetData : SpawnFormationAssetData
    {
        public AngularDirection MovementDirection;
        public float Width = 5f;
        public float Height = 5f;
    }
    public enum SpawnFormation
    {
        Circle,
        Line,
    }
    
    public enum AngularDirection
    {
        Inwards,
        Outwards,
    }
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right,
    }
}