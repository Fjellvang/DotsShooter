using Unity.Entities;
using UnityEngine;

namespace DotsShooter.Player
{
    public struct PlayerInput : IComponentData
    {
        public Vector2 Move;
    }
}