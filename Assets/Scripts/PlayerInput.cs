using Unity.Entities;
using UnityEngine;

namespace DotsShooter
{
    public struct PlayerInput : IComponentData
    {
        public Vector2 Move;
    }
}