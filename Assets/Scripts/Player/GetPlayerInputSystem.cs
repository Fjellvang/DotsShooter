using DotsShooter.Events;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

using Event = DotsShooter.Events.Event;
using EventType = DotsShooter.Events.EventType;

namespace DotsShooter.Player
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial class GetPlayerInputSystem : SystemBase
    {
        private PlayerControls _actions;
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
            _actions.Player.Pause.performed += OnPausedPerformed;
        }

        private void OnPausedPerformed(InputAction.CallbackContext obj)
        {
            var events = SystemAPI.GetSingletonRW<EventQueue>();
            var parallelWriter = events.ValueRW.Value.AsParallelWriter();
            parallelWriter.Enqueue(new Event(){ EventType = EventType.PauseRequested});
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
            _actions.Player.Pause.performed -= OnPausedPerformed;

            _actions.Disable();
        }

        protected override void OnUpdate()
        {
        }
    }

}