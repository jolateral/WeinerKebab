using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Attach to an empty "GameManager" GameObject.
public class GameManager : MonoBehaviour
{
    public RisingFlood flood;
    public TMP_Text scoreText;      // assign a TextMeshProUGUI (Canvas > UI > Text - TextMeshPro)
    public TMP_Text bestText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverScoreText;

    private float best = 0f;
    private bool isGameOver = false;

    void Start()
    {
        if (flood != null) flood.OnPlayerCaught += HandleGameOver;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    void Update()
    {
        if (isGameOver || flood == null) return;
        int height = Mathf.RoundToInt(flood.HeightScore);
        if (scoreText != null) scoreText.text = height + "m";
    }

    private void HandleGameOver()
    {
        isGameOver = true;
        int finalHeight = Mathf.RoundToInt(flood.HeightScore);
        best = Mathf.Max(best, finalHeight);

        if (bestText != null) bestText.text = "best " + best + "m";
        if (gameOverScoreText != null) gameOverScoreText.text = "You reached " + finalHeight + "m before the steam caught you.";
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // Hook this up to a UI Button's OnClick().
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
