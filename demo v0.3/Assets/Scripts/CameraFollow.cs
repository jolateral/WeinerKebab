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

    [Header("Smoothing (jitter reduction)")]
    [Tooltip("How quickly the turn-padding bonus itself fades in/out as the player changes direction. Higher = smoother but laggier response to turns. This is what stops the camera from visibly jumping every time the player snaps from vertical to horizontal movement.")]
    public float paddingSmoothTime = 0.35f;

    [Header("Subtle Zoom")]
    [Tooltip("Enable a gentle, continuous zoom that eases out slightly when the camera is working to catch up to the player, and eases back in when it's comfortably keeping pace.")]
    public bool enableSubtleZoom = true;
    [Tooltip("Max extra orthographic size added at full 'catch-up' tension, as a fraction of the base size. Keep this small (0.05-0.15) so it reads as subtle.")]
    [Range(0f, 0.3f)] public float zoomTensionFraction = 0.08f;
    [Tooltip("World-unit vertical gap between camera and its target that counts as 'full tension' for zoom purposes.")]
    public float zoomTensionRange = 2.5f;
    public float zoomSmoothTime = 0.6f;

    public System.Action OnPlayerCaught;

    private Camera cam;
    private Vector3 velocity = Vector3.zero;
    private float centerX;
    private float risingFloorY;   // the relentless minimum - only ever increases
    private float elapsed = 0f;
    private bool gameOver = false;

    private float smoothedHorizontalness = 0f;
    private float horizontalnessVelocity = 0f;

    private float baseOrthoSize;
    private float zoomVelocity = 0f;

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
        baseOrthoSize = cam.orthographicSize;

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

        // Smooth the raw horizontalness signal itself (rather than smoothing its downstream effect)
        // so the padding bonus fades in/out gradually instead of snapping whenever the player's
        // direction flips instantly between vertical and horizontal. This is what removes the jump.
        float rawHorizontalness = ComputeRawHorizontalness();
        smoothedHorizontalness = Mathf.SmoothDamp(smoothedHorizontalness, rawHorizontalness, ref horizontalnessVelocity, paddingSmoothTime);
        float dynamicPadding = verticalPadding + settings.turnPaddingBonus * smoothedHorizontalness;

        // Camera follows whichever is higher: the player (when they're ahead) or the relentless
        // rising floor (when the player is lagging behind) - it never moves back down.
        float targetY = Mathf.Max(player.position.y, risingFloorY + dynamicPadding);
        Vector3 targetPos = new Vector3(centerX, targetY, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

        if (enableSubtleZoom)
        {
            // "Tension" is how far behind the target the camera currently is - zero when it's
            // comfortably keeping pace, larger when it's working to catch up. Easing the zoom off
            // this (rather than snapping) keeps the camera visibly, gently alive at all times without
            // being distracting, and it settles back to normal once the camera catches up again.
            float gap = Mathf.Max(0f, targetY - transform.position.y);
            float tension = zoomTensionRange > 0f ? Mathf.Clamp01(gap / zoomTensionRange) : 0f;
            float targetOrthoSize = baseOrthoSize * (1f + zoomTensionFraction * tension);
            float currentSize = cam.orthographicSize;
            cam.orthographicSize = Mathf.SmoothDamp(currentSize, targetOrthoSize, ref zoomVelocity, zoomSmoothTime);
        }

        float cameraBottomEdge = transform.position.y - cam.orthographicSize;
        bool inDeathGrace = playerController != null && playerController.IsInDeathGrace;
        if (!inDeathGrace && player.position.y < cameraBottomEdge - settings.offscreenDeathMargin)
        {
            gameOver = true;
            OnPlayerCaught?.Invoke();
        }
    }

    private float ComputeRawHorizontalness()
    {
        if (playerController == null) return 0f;
        Vector2 vel = playerController.CurrentVelocity;
        float speed = vel.magnitude;
        if (speed < 0.01f) return 0f;

        // 0 when moving purely vertically, 1 when moving purely horizontally.
        return Mathf.Abs(vel.x) / speed;
    }
}