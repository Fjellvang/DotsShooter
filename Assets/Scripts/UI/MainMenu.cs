using System.Collections;
using System.Collections.Generic;
using DotsShooter;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField]
    GameStateTracker _gameStateTracker;
    public void StartGame()
    {
        _gameStateTracker.StartGame();
        SceneManager.LoadSceneAsync(1);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}