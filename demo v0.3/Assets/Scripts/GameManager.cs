using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    public CameraFollow cameraFollow;
    public PlayerController player;
    public TMP_Text scoreText;      
    public TMP_Text bestText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverScoreText;

    [Header("Gun UI Controls")]
    public Button fireButton;               // UI Red Button for shooting
    public Image fireButtonImage;           // Target Image on button to set greyed out state
    public TMP_Text ammoText;               // Displays current ammo count (e.g. "Ammo: 3/5")
    public Color normalButtonColor = Color.red;
    public Color disabledButtonColor = Color.gray;

    [Header("Milestone Feedback")]
    public CanvasGroup milestoneFlashGroup;
    public TMP_Text milestoneText;
    public AudioSource audioSource;
    public AudioClip milestoneSound;
    public float milestoneFlashFadeSeconds = 0.6f;
    [Range(0f, 1f)] public float milestoneFlashPeakAlpha = 0.6f;

    private float best = 0f;
    private bool isGameOver = false;
    private int lastMilestoneReached = 0;
    private Coroutine flashRoutine;

    private const string BestScoreKey = "VentCrawler_BestHeight";

    void Start()
    {
        isGameOver = false;
        lastMilestoneReached = 0;

        // Load the persisted best score and show it immediately, rather than leaving whatever
        // placeholder text was left on the TMP object in the Editor until the first death.
        best = PlayerPrefs.GetFloat(BestScoreKey, 0f);
        if (bestText != null) bestText.text = "best " + Mathf.RoundToInt(best) + "m";

        if (cameraFollow != null)
        {
            cameraFollow.ResetForNewRun();
            cameraFollow.OnPlayerCaught -= HandleGameOver;
            cameraFollow.OnPlayerCaught += HandleGameOver;
        }

        if (player != null)
        {
            player.OnAmmoChanged -= UpdateAmmoUI;
            player.OnAmmoChanged += UpdateAmmoUI;
            UpdateAmmoUI(player.CurrentAmmo);
        }

        if (fireButton != null && player != null)
        {
            fireButton.onClick.AddListener(player.Shoot);
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (milestoneFlashGroup != null) milestoneFlashGroup.alpha = 0f;
    }

    private void UpdateAmmoUI(int ammoCount)
    {
        if (ammoText != null && player != null && player.settings != null)
        {
            ammoText.text = ammoCount + " / " + player.settings.maxAmmo;
        }

        bool hasAmmo = ammoCount > 0;
        if (fireButton != null)
        {
            fireButton.interactable = hasAmmo;
        }

        if (fireButtonImage != null)
        {
            fireButtonImage.color = hasAmmo ? normalButtonColor : disabledButtonColor;
        }
    }

    void Update()
    {
        if (isGameOver || cameraFollow == null) return;
        int height = Mathf.RoundToInt(cameraFollow.HeightScore);
        if (scoreText != null) scoreText.text = height + "m";

        CheckMilestone(height);
    }

    private void CheckMilestone(int height)
    {
        float interval = cameraFollow.settings != null ? cameraFollow.settings.milestoneIntervalMeters : 100f;
        if (interval <= 0f) return;

        int currentMilestone = Mathf.FloorToInt(height / interval) * Mathf.RoundToInt(interval);
        if (currentMilestone > lastMilestoneReached && currentMilestone > 0)
        {
            lastMilestoneReached = currentMilestone;
            TriggerMilestoneFeedback(currentMilestone);
        }
    }

    private void TriggerMilestoneFeedback(int milestoneValue)
    {
        if (milestoneText != null) milestoneText.text = milestoneValue + "m!";
        if (audioSource != null && milestoneSound != null) audioSource.PlayOneShot(milestoneSound);

        if (milestoneFlashGroup != null)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        milestoneFlashGroup.alpha = milestoneFlashPeakAlpha;
        float t = 0f;
        while (t < milestoneFlashFadeSeconds)
        {
            t += Time.deltaTime;
            milestoneFlashGroup.alpha = Mathf.Lerp(milestoneFlashPeakAlpha, 0f, t / milestoneFlashFadeSeconds);
            yield return null;
        }
        milestoneFlashGroup.alpha = 0f;
    }

    private void HandleGameOver()
    {
        isGameOver = true;
        int finalHeight = Mathf.RoundToInt(cameraFollow.HeightScore);

        bool isNewBest = finalHeight > best;
        best = Mathf.Max(best, finalHeight);
        if (isNewBest)
        {
            PlayerPrefs.SetFloat(BestScoreKey, best);
            PlayerPrefs.Save();
        }

        if (bestText != null) bestText.text = "best " + Mathf.RoundToInt(best) + "m";
        if (gameOverScoreText != null) gameOverScoreText.text = "You reached " + finalHeight + "m before falling off screen.";
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}