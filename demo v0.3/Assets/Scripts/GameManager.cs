using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

// Attach to an empty "GameManager" GameObject.
public class GameManager : MonoBehaviour
{
    public CameraFollow cameraFollow;
    public TMP_Text scoreText;      // assign a TextMeshProUGUI (Canvas > UI > Text - TextMeshPro)
    public TMP_Text bestText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverScoreText;

    [Header("Milestone Feedback")]
    [Tooltip("A full-screen UI Image with a CanvasGroup on it (e.g. solid white), alpha 0 by default. Flashed briefly every settings.milestoneIntervalMeters of height climbed.")]
    public CanvasGroup milestoneFlashGroup;
    [Tooltip("Optional text shown during the flash, e.g. '100m!'. Can be a child of the same object as milestoneFlashGroup.")]
    public TMP_Text milestoneText;
    [Tooltip("AudioSource used to play the milestone sound cue. Add an AudioSource component to this GameObject (or anywhere) and assign it here.")]
    public AudioSource audioSource;
    [Tooltip("Sound played each time a milestone is hit.")]
    public AudioClip milestoneSound;
    [Tooltip("How long the flash takes to fade back out.")]
    public float milestoneFlashFadeSeconds = 0.6f;
    [Tooltip("Peak alpha of the flash (0-1).")]
    [Range(0f, 1f)] public float milestoneFlashPeakAlpha = 0.6f;

    private float best = 0f;
    private bool isGameOver = false;
    private int lastMilestoneReached = 0;
    private Coroutine flashRoutine;

    void Start()
    {
        isGameOver = false;
        lastMilestoneReached = 0;

        if (cameraFollow != null)
        {
            // Belt-and-suspenders: explicitly re-run the camera's full reset here, not just rely on
            // its own Awake(). If the camera (or a parent) happens to survive the scene reload -
            // e.g. via DontDestroyOnLoad somewhere else in the project, or a persistent bootstrap
            // scene - Awake() never fires again on restart, and the camera would otherwise just
            // freeze at whatever tight/loose zoom it happened to be at the moment you died. Calling
            // this unconditionally guarantees a correct fresh fit every time, whether or not the
            // camera object was actually recreated.
            cameraFollow.ResetForNewRun();

            // Guard against double subscription if this GameManager (or the camera) persisted
            // across the reload.
            cameraFollow.OnPlayerCaught -= HandleGameOver;
            cameraFollow.OnPlayerCaught += HandleGameOver;
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (milestoneFlashGroup != null) milestoneFlashGroup.alpha = 0f;
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