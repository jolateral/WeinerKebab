using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [HideInInspector] public bool isGameOver = false;
    [HideInInspector] public int score = 0;

    public event Action<int> OnScoreChanged;
    public event Action OnGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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
        OnGameOver?.Invoke();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}