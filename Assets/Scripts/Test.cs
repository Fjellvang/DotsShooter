using UnityEngine;

namespace DotsShooter
{
    public class Test : MonoBehaviour
    {
        PlayerControls playerControls;

        private void Awake()
        {
            playerControls = new PlayerControls();
            playerControls.Enable();
        }

        private void OnEnable()
        {
            playerControls.Player.Move.performed += ctx => Debug.Log("Move");
        }
    }

// Update in the initilization systems group right after unitys systems, to ensure player 
// input is ready for the gameplay systems
}