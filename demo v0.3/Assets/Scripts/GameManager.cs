using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Attach to an empty "GameManager" GameObject.
public class GameManager : MonoBehaviour
{
    public CameraFollow cameraFollow;
    public TMP_Text scoreText;      // assign a TextMeshProUGUI (Canvas > UI > Text - TextMeshPro)
    public TMP_Text bestText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverScoreText;

    private float best = 0f;
    private bool isGameOver = false;

    void Start()
    {
        if (cameraFollow != null) cameraFollow.OnPlayerCaught += HandleGameOver;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    void Update()
    {
        if (isGameOver || cameraFollow == null) return;
        int height = Mathf.RoundToInt(cameraFollow.HeightScore);
        if (scoreText != null) scoreText.text = height + "m";
    }

    private void HandleGameOver()
    {
        isGameOver = true;
        int finalHeight = Mathf.RoundToInt(cameraFollow.HeightScore);
        best = Mathf.Max(best, finalHeight);

        if (bestText != null) bestText.text = "best " + best + "m";
        if (gameOverScoreText != null) gameOverScoreText.text = "You reached " + finalHeight + "m before falling off screen.";
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // Hook this up to a UI Button's OnClick().
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
