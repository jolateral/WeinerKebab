using UnityEngine;
using UnityEngine.UI;

// Attach to any object in your Canvas. Uses legacy UI Text so there's
// no dependency on importing TextMeshPro for the demo -- swap to TMP_Text
// later if you'd prefer.
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
        GameManager.Instance.OnGameOver += ShowGameOver;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged -= UpdateScore;
            GameManager.Instance.OnGameOver -= ShowGameOver;
        }
    }

    private void UpdateScore(int newScore)
    {
        if (scoreText != null) scoreText.text = "Floors Cleared: " + newScore;
    }

    private void ShowGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (finalScoreText != null) finalScoreText.text = "Floors Cleared: " + GameManager.Instance.score;
    }

    // Hook this up to your Restart button's OnClick.
    public void OnRestartButtonPressed()
    {
        GameManager.Instance.RestartGame();
    }
}
