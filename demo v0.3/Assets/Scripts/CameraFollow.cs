using UnityEngine;

// Attach to the Main Camera. Use an orthographic camera for a clean 2D top-down vent view.
// This now owns the whole "relentless rise" mechanic directly: there's no separate flood object -
// the camera itself rises at an ever-increasing (but capped) minimum rate, and death happens the
// moment the player falls fully outside the camera's view (off the bottom edge), not from touching
// anything directly.
public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public GameSettings settings;
    public MazeGenerator mazeGenerator;
    [Tooltip("Optional but recommended: assign the player's PlayerController so the camera can grant turn-padding and post-stun death grace.")]
    public PlayerController playerController;

    [Tooltip("How far above the rising floor the camera keeps its bottom edge when the player is behind.")]
    public float verticalPadding = 3f;
    public float smoothTime = 0.15f;
    [Tooltip("Extra breathing room added to the maze width when fitting the camera, in world units.")]
    public float horizontalPadding = 0.4f;

    public System.Action OnPlayerCaught;

    private Camera cam;
    private Vector3 velocity = Vector3.zero;
    private float centerX;
    private float risingFloorY;   // the relentless minimum - only ever increases
    private float elapsed = 0f;
    private bool gameOver = false;

    public float HeightScore => player != null ? Mathf.Max(0f, player.position.y / settings.cellSize) : 0f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;

        float mazeWidth = settings.columns * settings.cellSize;
        centerX = mazeWidth * 0.5f;

        // Fit the maze width exactly into the current screen's aspect ratio (e.g. 390x844 portrait).
        float aspect = (float)Screen.width / Screen.height;
        cam.orthographicSize = (mazeWidth + horizontalPadding) / (2f * aspect);

        risingFloorY = (player != null ? player.position.y : 0f);
        transform.position = new Vector3(centerX, risingFloorY + verticalPadding, transform.position.z);

        if (playerController == null && player != null)
        {
            playerController = player.GetComponent<PlayerController>();
        }
    }

    void LateUpdate()
    {
        if (gameOver || player == null) return;

        elapsed += Time.deltaTime;
        float riseSpeed = Mathf.Min(
            settings.cameraRiseSpeedBase + settings.cameraRiseRampPerSec * elapsed,
            settings.cameraRiseSpeedMax);
        risingFloorY += riseSpeed * Time.deltaTime;

        if (mazeGenerator != null) mazeGenerator.EnsureGeneratedAhead(risingFloorY);

        // Grant extra padding while the player is moving mostly sideways (navigating a turn) rather
        // than idling or moving straight up - this is what keeps turn-heavy maze sections survivable.
        float dynamicPadding = verticalPadding + ComputeTurnPaddingBonus();

        // Camera follows whichever is higher: the player (when they're ahead) or the relentless
        // rising floor (when the player is lagging behind) - it never moves back down.
        float targetY = Mathf.Max(player.position.y, risingFloorY + dynamicPadding);
        Vector3 targetPos = new Vector3(centerX, targetY, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

        float cameraBottomEdge = transform.position.y - cam.orthographicSize;
        bool inDeathGrace = playerController != null && playerController.IsInDeathGrace;
        if (!inDeathGrace && player.position.y < cameraBottomEdge - settings.offscreenDeathMargin)
        {
            gameOver = true;
            OnPlayerCaught?.Invoke();
        }
    }

    private float ComputeTurnPaddingBonus()
    {
        if (playerController == null) return 0f;
        Vector2 vel = playerController.CurrentVelocity;
        float speed = vel.magnitude;
        if (speed < 0.01f) return 0f;

        // 0 when moving purely vertically, 1 when moving purely horizontally.
        float horizontalness = Mathf.Abs(vel.x) / speed;
        return settings.turnPaddingBonus * horizontalness;
    }
}