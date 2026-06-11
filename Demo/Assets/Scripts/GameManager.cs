using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI floorNumberText;    // the big gold number
    public TextMeshProUGUI highScoreText;
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverFloorText;
    public TextMeshProUGUI gameOverHighScoreText;

    [Header("Game State")]
    public bool isGameOver = false;
    public int currentFloor = 0;
    public int highScore = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        highScore = PlayerPrefs.GetInt("HighScore", 0);
    }

    private void Start()
    {
        UpdateUI();
        gameOverPanel.SetActive(false);
    }

    public void AddFloor()
    {
        if (isGameOver) return;
        currentFloor++;
        if (currentFloor > highScore)
        {
            highScore = currentFloor;
            PlayerPrefs.SetInt("HighScore", highScore);
        }
        UpdateUI();
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        Time.timeScale = 0f;

        gameOverFloorText.text = "Floors Climbed: " + currentFloor;
        gameOverHighScoreText.text = "Best: " + highScore;
        gameOverPanel.SetActive(true);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void UpdateUI()
    {
        floorNumberText.text = currentFloor.ToString();
        highScoreText.text = "BEST: " + highScore;
    }
}