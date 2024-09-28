using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DotsShooter
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial class GetPlayerInputSystem : SystemBase
    {
        private PlayerControls _actions;
        private Entity _playerEntity;
        
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTag>();
            RequireForUpdate<PlayerInput>();
            
            _actions = new PlayerControls();
        }
        protected override void OnStartRunning()
        {
            _actions.Enable();
            _actions.Player.Move.performed += OnMovePerformed;
            _actions.Player.Move.canceled += OnMoveCancelled;
            _playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
        }

        private void OnMoveCancelled(InputAction.CallbackContext obj)
        {
            SystemAPI.SetSingleton(new PlayerInput
            {
                Move = Vector2.zero
            });
        }

        private void OnMovePerformed(InputAction.CallbackContext obj)
        {
            SystemAPI.SetSingleton(new PlayerInput
            {
                Move = obj.ReadValue<Vector2>()
            });
        }

        protected override void OnStopRunning()
        {
            _actions.Player.Move.performed -= OnMovePerformed;
            _actions.Player.Move.canceled -= OnMoveCancelled;

            _actions.Disable();
            _playerEntity = Entity.Null;
        }

        protected override void OnUpdate()
        {
        }
    }
}