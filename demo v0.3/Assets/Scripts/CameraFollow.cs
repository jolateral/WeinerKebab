using UnityEngine;

// Attach to the Main Camera. Use an orthographic camera for a clean 2D top-down vent view.
//
// CROSSY-ROAD MODEL: the camera does NOT chase or react to the player at all. It rises at its
// own constant (gently ramping) pace, full stop. The player is completely free to move anywhere
// within the visible frame - toward the middle, toward the top, wherever. Pressure comes purely
// from watching the rising floor creep up the screen toward you, not from the camera snapping
// around to track your every move. This also makes camera motion mathematically continuous
// (no discontinuous jumps), because it's just a smoothly increasing number, never something that
// switches source (player vs floor) frame to frame.
//
// FRAMING: the camera's orthographic size is fixed - computed once from the base maze width,
// pulled back by cameraZoomOutMultiplier to match the wider, more zoomed-out comic panel
// reference (rather than a tight exact-fit). It deliberately does NOT change size as you climb,
// even if MazeGenerator's wide-reveal-band feature is re-enabled later - constant zoom swings
// were reported as disorienting, so framing now only ever changes via the small, slow "danger"
// tension zoom below (which can also be turned off via enableSubtleZoom).
//
// Death happens the moment the player falls fully outside the camera's view (off the bottom edge).
public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public GameSettings settings;
    public MazeGenerator mazeGenerator;
    [Tooltip("Optional but recommended: assign the player's PlayerController so the camera can grant post-stun death grace.")]
    public PlayerController playerController;

    [Tooltip("How far above the rising floor the camera's bottom edge sits. Bigger = more headroom/less pressure at any given moment.")]
    public float verticalPadding = 4.5f;
    [Tooltip("Smoothing for the camera's own rise motion. This is NOT reacting to the player - it just softens frame-to-frame float jitter. Keep small.")]
    public float smoothTime = 0.1f;
    [Tooltip("Extra breathing room added to the maze width when fitting the camera, in world units.")]
    public float horizontalPadding = 0.4f;

    [Header("Subtle Zoom (danger feedback)")]
    [Tooltip("Enable a gentle zoom-out when the player is close to the bottom edge (danger), easing back in when they're safely clear of it.")]
    public bool enableSubtleZoom = true;
    [Tooltip("Max extra orthographic size added when the player is right at the bottom edge, as a fraction of the base size. Keep small (0.05-0.15) so it reads as subtle.")]
    [Range(0f, 0.3f)] public float zoomTensionFraction = 0.08f;
    [Tooltip("World-unit distance from the bottom edge at which tension is considered 'full' (i.e. player is in real danger).")]
    public float zoomTensionRange = 2.5f;
    public float zoomSmoothTime = 0.6f;

    public System.Action OnPlayerCaught;

    private Camera cam;
    private Vector3 velocity = Vector3.zero;
    private float centerX;
    private float risingFloorY;   // the relentless minimum - only ever increases
    private float elapsed = 0f;
    private bool gameOver = false;

    private float aspect;
    private float baseOrthoSize;   // fixed width-driven size (before the danger-tension bump), computed once in Awake
    private float zoomVelocity = 0f;

    private float rubberBandMultiplier = 1f;
    private float rubberBandVelocity = 0f;

    public float HeightScore => player != null ? Mathf.Max(0f, player.position.y / settings.cellSize) : 0f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;

        centerX = mazeGenerator != null ? mazeGenerator.BandCenterX : settings.columns * settings.cellSize * 0.5f;

        aspect = (float)Screen.width / Screen.height;

        // Fit the base maze width into the current screen's aspect ratio, then pull back further
        // by cameraZoomOutMultiplier so the frame matches the wider comic-panel reference instead
        // of a tight exact fit.
        float baseWidth = settings.columns * settings.cellSize + horizontalPadding;
        baseOrthoSize = SizeForWidth(baseWidth) * settings.cameraZoomOutMultiplier;
        cam.orthographicSize = baseOrthoSize;

        // Start the floor well below the player so they begin comfortably inside the frame
        // (roughly centered, not pinned to the bottom edge) instead of feeling pressured immediately.
        float startY = player != null ? player.position.y : 0f;
        risingFloorY = startY - verticalPadding;
        transform.position = new Vector3(centerX, startY + cam.orthographicSize * 0.5f, transform.position.z);

        if (playerController == null && player != null)
        {
            playerController = player.GetComponent<PlayerController>();
        }
    }

    private float SizeForWidth(float worldWidth)
    {
        return worldWidth / (2f * aspect);
    }

    void LateUpdate()
    {
        if (gameOver || player == null) return;

        elapsed += Time.deltaTime;

        // Rise speed only starts building up after the grace period, and ramps gently from there -
        // this is what gives the player a calm opening window instead of instant pressure.
        float rampedElapsed = Mathf.Max(0f, elapsed - settings.riseStartDelaySeconds);
        float baseRiseSpeed = rampedElapsed > 0f
            ? Mathf.Min(settings.cameraRiseSpeedBase + settings.cameraRiseRampPerSec * rampedElapsed, settings.cameraRiseSpeedMax)
            : 0f;

        // Rubber-band the rise SPEED (never the camera's position) based on the player's current
        // buffer above the bottom edge. This is what stops one unlucky wire hit early in a run from
        // being an unrecoverable death sentence: lose your buffer and the flood eases off briefly so
        // you can rebuild it; get a big comfortable lead and it leans back in so it stays a challenge.
        if (settings.enableRubberBanding)
        {
            float cameraBottomEdgeNow = transform.position.y - cam.orthographicSize;
            float bufferAboveEdge = Mathf.Max(0f, player.position.y - cameraBottomEdgeNow);
            float safety = settings.rubberBandDangerRange > 0f ? Mathf.Clamp01(bufferAboveEdge / settings.rubberBandDangerRange) : 1f;
            float targetMultiplier = Mathf.Lerp(settings.rubberBandMinMultiplier, settings.rubberBandMaxMultiplier, safety);
            rubberBandMultiplier = Mathf.SmoothDamp(rubberBandMultiplier, targetMultiplier, ref rubberBandVelocity, settings.rubberBandSmoothTime);
        }
        else
        {
            rubberBandMultiplier = 1f;
        }

        float riseSpeed = baseRiseSpeed * rubberBandMultiplier;
        risingFloorY += riseSpeed * Time.deltaTime;

        if (mazeGenerator != null) mazeGenerator.EnsureGeneratedAhead(risingFloorY);

        // The camera target depends ONLY on the floor, never on the player. This is the key change:
        // no more source-switching, so no more discontinuous jumps.
        float targetY = risingFloorY + verticalPadding;
        Vector3 targetPos = new Vector3(centerX, targetY, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

        float cameraBottomEdge = transform.position.y - cam.orthographicSize;

        if (enableSubtleZoom)
        {
            // Tension is about the PLAYER's danger (how close to the bottom edge they are), not
            // about the camera catching up to anything. This is the ONLY thing that still moves
            // the zoom level at all, and it's small (zoomTensionFraction, default 8%) and slow
            // (zoomSmoothTime) by design - set enableSubtleZoom to false for a completely static
            // zoom level if even this is too much.
            float distanceFromEdge = Mathf.Max(0f, player.position.y - cameraBottomEdge);
            float tension = zoomTensionRange > 0f ? 1f - Mathf.Clamp01(distanceFromEdge / zoomTensionRange) : 0f;
            float targetOrthoSize = baseOrthoSize * (1f + zoomTensionFraction * tension);
            float currentSize = cam.orthographicSize;
            cam.orthographicSize = Mathf.SmoothDamp(currentSize, targetOrthoSize, ref zoomVelocity, zoomSmoothTime);
            // Recompute bottom edge since orthographicSize may have just changed.
            cameraBottomEdge = transform.position.y - cam.orthographicSize;
        }
        else
        {
            cam.orthographicSize = baseOrthoSize;
            cameraBottomEdge = transform.position.y - cam.orthographicSize;
        }

        bool inDeathGrace = playerController != null && playerController.IsInDeathGrace;
        if (!inDeathGrace && player.position.y < cameraBottomEdge - settings.offscreenDeathMargin)
        {
            gameOver = true;
            OnPlayerCaught?.Invoke();
        }
    }
}