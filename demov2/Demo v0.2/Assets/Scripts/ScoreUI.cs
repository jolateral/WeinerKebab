using UnityEngine;
using UnityEngine.UI;

public class ScoreUI : MonoBehaviour
{
    [Header("HUD")]
    public Text scoreText;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public Text finalScoreText;

    private void Start()
    {
        UpdateScore(GameManager.Instance.score);
        GameManager.Instance.OnScoreChanged += UpdateScore;
        GameManager.Instance.OnGameOver     += ShowGameOver;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged -= UpdateScore;
            GameManager.Instance.OnGameOver     -= ShowGameOver;
        }
    }

    private void UpdateScore(int s)
    {
        if (scoreText != null) scoreText.text = "Floors: " + s;
    }

    private void ShowGameOver()
    {
        if (gameOverPanel  != null) gameOverPanel.SetActive(true);
        if (finalScoreText != null) finalScoreText.text = "Floors: " + GameManager.Instance.score;
    }

    public void OnRestartButtonPressed() => GameManager.Instance.RestartGame();
}