using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Single source of truth for "is the run over" and "how many floors
// has the player cleared". Every other script reads GameManager.Instance
// instead of tracking its own copy of game state.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [HideInInspector] public bool isGameOver = false;
    [HideInInspector] public int score = 0;

    // UI (or anything else) can subscribe instead of polling every frame.
    public event Action<int> OnScoreChanged;
    public event Action OnGameOver;

    private void Awake()
    {
        // Simple singleton guard. Not using DontDestroyOnLoad because
        // RestartGame() reloads the whole scene, which gives us a clean
        // GameManager for free.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddScore(int amount = 1)
    {
        if (isGameOver) return;
        score += amount;
        OnScoreChanged?.Invoke(score);
    }

public void GameOver()
{
    if (isGameOver) return;
    isGameOver = true;
    Debug.Log("Game Over triggered by: " + UnityEngine.StackTraceUtility.ExtractStackTrace());
    OnGameOver?.Invoke();
}

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
