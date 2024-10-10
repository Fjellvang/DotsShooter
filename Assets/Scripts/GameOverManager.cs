using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;  // For reloading the scene
using UnityEngine.UI;  // For manipulating UI elements


public class GameOverManager : MonoBehaviour
{
    [SerializeField] GameObject gameOverUI;  // Reference to the Game Over UI panel
    [SerializeField] Button restartButton;   // The restart button
    
    [SerializeField] GameObject inGameUI;  // Reference to the in-game UI elements (e.g., score, health, etc.)
   

    private bool isGameOver = false;

    void Start()
    {
        // Hide the Game Over UI at the start
        gameOverUI.SetActive(false);
        // Assign the restart function to the button
        restartButton.onClick.AddListener(RestartGame);

    }

    public void GameOver()
    {
        isGameOver = true;
        // Show the Game Over UI
        gameOverUI.SetActive(true);


       

        // Hide the in-game UI
        if (inGameUI != null)
        {
            inGameUI.SetActive(false);
        }

        
    }

    public void RestartGame()
    {
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

       

        // Show the in-game UI again when the game restarts
        if (inGameUI != null)
        {
            inGameUI.SetActive(true);
        }
    }
}
