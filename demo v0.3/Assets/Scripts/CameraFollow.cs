using UnityEngine;

// Attach to the Main Camera. Use an orthographic camera for a clean 2D top-down vent view.
//
// CROSSY-ROAD MODEL: the camera does NOT chase or react to the player's position at all. It rises
// at its own constant (gently ramping) pace, full stop. The player is completely free to move
// anywhere within the visible frame - toward the middle, toward the top, wherever. Pressure comes
// purely from watching the rising floor creep up the screen toward you, not from the camera
// snapping around to track your every move.
//
// FRAMING: base width-driven zoom now tracks the actual band width around the player (via
// mazeGenerator.BandWidthAtWorldY), smoothed by settings.cameraWidthSmoothTime. On ordinary bands
// every band is the same width, so this sits perfectly still just like the old fixed-size
// behavior. The only time it actually moves is around a wide "choice" band, where it slowly pulls
// back to reveal the bigger branching layout, then eases back in afterward. On top of that sits
// the small, fast "danger" tension zoom (enableSubtleZoom) and the wire-hit electrocution shake.
//
// Death happens the moment the player falls fully outside the camera's view (off the bottom edge).
public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public GameSettings settings;
    public MazeGenerator mazeGenerator;
    [Tooltip("Optional but recommended: assign the player's PlayerController so the camera can grant post-stun death grace and react to wire hits with a shake.")]
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

    [Header("Wire-Hit Shake")]
    [Tooltip("Turn the electrocution shake off entirely if you ever want to A/B it.")]
    public bool enableWireShake = true;

    public System.Action OnPlayerCaught;

    private Camera cam;
    private Vector3 basePosition;  // the "clean" (unshaken) camera position - shake is added on top, purely visually
    private Vector3 velocity = Vector3.zero;
    private float centerX;
    private float risingFloorY;   // the relentless minimum - only ever increases
    private float elapsed = 0f;
    private bool gameOver = false;

    private float aspect;
    private float baseOrthoSize;   // width-driven size (before the danger-tension bump); now re-fitted continuously
    private float widthVelocity = 0f;
    private float zoomVelocity = 0f;

    private float rubberBandMultiplier = 1f;
    private float rubberBandVelocity = 0f;

    // Wire-hit shake state
    private float shakeTimeRemaining = 0f;
    private float shakeTotalDuration = 0f;
    private float shakeMagnitude = 0f;
    private float shakeSeedX;
    private float shakeSeedY;

    public float HeightScore => player != null ? Mathf.Max(0f, player.position.y / settings.cellSize) : 0f;

    void Awake()
    {
        ResetForNewRun();
    }

    // Everything needed to put the camera back into its exact "fresh run" state: correct fit,
    // correct starting position, correct internal counters, zero leftover shake/zoom/rubber-band
    // state. Awake() calls this normally, but GameManager ALSO calls this explicitly on every
    // restart. That belt-and-suspenders approach means the camera resets correctly even if this
    // GameObject happens to survive a scene reload (e.g. via DontDestroyOnLoad somewhere else in
    // the project, or a persistent bootstrap scene) - cases where Awake() would otherwise never
    // run again and the camera would just freeze at whatever tight/loose zoom it happened to be at
    // the moment the player died.
    public void ResetForNewRun()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;

        centerX = mazeGenerator != null ? mazeGenerator.BandCenterX : settings.columns * settings.cellSize * 0.5f;

        aspect = (float)Screen.width / Screen.height;

        // Fit the base maze width into the current screen's aspect ratio, then pull back further
        // by cameraZoomOutMultiplier so the frame matches the wider comic-panel reference instead
        // of a tight exact fit. This is just the STARTING value - LateUpdate keeps re-fitting it
        // to whatever band width is actually around the player from here on.
        float baseWidth = settings.columns * settings.cellSize + horizontalPadding;
        baseOrthoSize = SizeForWidth(baseWidth) * settings.cameraZoomOutMultiplier;
        cam.orthographicSize = baseOrthoSize;

        // Start the floor well below the player so they begin comfortably inside the frame
        // (roughly centered, not pinned to the bottom edge) instead of feeling pressured immediately.
        float startY = player != null ? player.position.y : 0f;
        risingFloorY = startY - verticalPadding;
        basePosition = new Vector3(centerX, startY + cam.orthographicSize * 0.5f, transform.position.z);
        transform.position = basePosition;

        if (playerController == null && player != null)
        {
            playerController = player.GetComponent<PlayerController>();
        }

        // Guard against double-subscribing if this object survived a reload and ResetForNewRun()
        // is called more than once (Awake, then GameManager.Start on the same run).
        if (playerController != null)
        {
            playerController.OnStunned -= HandleWireHit;
            playerController.OnStunned += HandleWireHit;
        }

        // Reset every piece of run-local state so nothing bleeds over from a previous life.
        elapsed = 0f;
        gameOver = false;
        widthVelocity = 0f;
        zoomVelocity = 0f;
        rubberBandMultiplier = 1f;
        rubberBandVelocity = 0f;
        velocity = Vector3.zero;
        shakeTimeRemaining = 0f;
        shakeTotalDuration = 0f;
        shakeMagnitude = 0f;

        shakeSeedX = Random.Range(0f, 1000f);
        shakeSeedY = Random.Range(1000f, 2000f);
    }

    void OnDestroy()
    {
        if (playerController != null) playerController.OnStunned -= HandleWireHit;
    }

    private float SizeForWidth(float worldWidth)
    {
        return worldWidth / (2f * aspect);
    }

    private void HandleWireHit()
    {
        if (!enableWireShake) return;
        shakeTimeRemaining = settings.wireShakeDuration;
        shakeTotalDuration = settings.wireShakeDuration;
        shakeMagnitude = settings.wireShakeMagnitude;
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
            float cameraBottomEdgeNow = basePosition.y - cam.orthographicSize;
            float bufferAboveEdge = Mathf.Max(0f, player.position.y - cameraBottomEdgeNow);
            float safety = settings.rubberBandDangerRange > 0f ? Mathf.Clamp01(bufferAboveEdge / settings.rubberBandDangerRange) : 1f;
            float targetMultiplier = Mathf.Lerp(settings.rubberBandMinMultiplier, settings.rubberBandMaxMultiplier, safety);
            rubberBandMultiplier = Mathf.SmoothDamp(rubberBandMultiplier, targetMultiplier, ref rubberBandVelocity, settings.rubberBandSmoothTime);
        }
        else
        {
            rubberBandMultiplier = 1f;
        }

        // Ease off the rise speed further while the player is inside a wide "choice" band - those
        // have real branching routes, so a wrong turn needs a genuine recovery window instead of
        // the flood punishing exploration the same way it punishes a mistake in a normal
        // single-corridor band.
        float wideBandMultiplier = 1f;
        if (mazeGenerator != null && mazeGenerator.IsWideBandAtWorldY(player.position.y))
        {
            wideBandMultiplier = settings.wideBandRiseSpeedMultiplier;
        }

        float riseSpeed = baseRiseSpeed * rubberBandMultiplier * wideBandMultiplier;
        risingFloorY += riseSpeed * Time.deltaTime;

        if (mazeGenerator != null) mazeGenerator.EnsureGeneratedAhead(risingFloorY);

        // The camera target depends ONLY on the floor, never on the player. This is the key change:
        // no more source-switching, so no more discontinuous jumps.
        float targetY = risingFloorY + verticalPadding;
        Vector3 targetPos = new Vector3(centerX, targetY, basePosition.z);
        basePosition = Vector3.SmoothDamp(basePosition, targetPos, ref velocity, smoothTime);

        // Continuously re-fit the base zoom to whatever band width is actually around the player.
        // On normal (uniform-width) bands this target never changes, so nothing visibly moves - it
        // only kicks in around a wide choice band, pulling the camera back to reveal it, then easing
        // back in afterward. Looks ahead to whichever is further along (player vs. floor) so the
        // reveal starts opening up slightly before the player is right on top of it.
        if (mazeGenerator != null)
        {
            float aheadY = Mathf.Max(player.position.y, risingFloorY);
            float targetBandWidth = mazeGenerator.BandWidthAtWorldY(aheadY) + horizontalPadding;
            float targetBaseSize = SizeForWidth(targetBandWidth) * settings.cameraZoomOutMultiplier;
            baseOrthoSize = Mathf.SmoothDamp(baseOrthoSize, targetBaseSize, ref widthVelocity, settings.cameraWidthSmoothTime);
        }

        float cameraBottomEdge = basePosition.y - cam.orthographicSize;

        if (enableSubtleZoom)
        {
            // Tension is about the PLAYER's danger (how close to the bottom edge they are), not
            // about the camera catching up to anything. This stacks on top of the width-driven
            // baseOrthoSize above, and stays small (zoomTensionFraction, default 8%) and slow
            // (zoomSmoothTime) by design - set enableSubtleZoom to false for a completely static
            // zoom level if even this is too much.
            float distanceFromEdge = Mathf.Max(0f, player.position.y - cameraBottomEdge);
            float tension = zoomTensionRange > 0f ? 1f - Mathf.Clamp01(distanceFromEdge / zoomTensionRange) : 0f;
            float targetOrthoSize = baseOrthoSize * (1f + zoomTensionFraction * tension);
            float currentSize = cam.orthographicSize;
            cam.orthographicSize = Mathf.SmoothDamp(currentSize, targetOrthoSize, ref zoomVelocity, zoomSmoothTime);
            // Recompute bottom edge since orthographicSize may have just changed.
            cameraBottomEdge = basePosition.y - cam.orthographicSize;
        }
        else
        {
            cam.orthographicSize = baseOrthoSize;
            cameraBottomEdge = basePosition.y - cam.orthographicSize;
        }

        bool inDeathGrace = playerController != null && playerController.IsInDeathGrace;
        if (!inDeathGrace && player.position.y < cameraBottomEdge - settings.offscreenDeathMargin)
        {
            gameOver = true;
            OnPlayerCaught?.Invoke();
        }

        // Apply the electrocution shake as a purely visual offset on top of basePosition, so it
        // never fights the SmoothDamp above or the death/edge math, which all use basePosition.
        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimeRemaining > 0f)
        {
            shakeTimeRemaining -= Time.deltaTime;
            float t = shakeTotalDuration > 0f ? Mathf.Clamp01(shakeTimeRemaining / shakeTotalDuration) : 0f;
            float falloff = t * t; // eases out toward the end instead of stopping abruptly
            float noiseX = (Mathf.PerlinNoise(shakeSeedX, Time.time * 40f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(shakeSeedY, Time.time * 40f) - 0.5f) * 2f;
            shakeOffset = new Vector3(noiseX, noiseY, 0f) * shakeMagnitude * falloff;
        }

        transform.position = basePosition + shakeOffset;
    }
}