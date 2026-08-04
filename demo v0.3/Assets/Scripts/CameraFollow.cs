using UnityEngine;

// Attach to the Main Camera. Use an orthographic camera for a clean 2D top-down vent view.
// This now owns the whole "relentless rise" mechanic directly: there's no separate flood object -
// the camera itself rises at an ever-increasing minimum rate, and death happens the moment the
// player falls fully outside the camera's view (off the bottom edge), not from touching anything.
public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public GameSettings settings;
    public MazeGenerator mazeGenerator;

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
    }

    void LateUpdate()
    {
        if (gameOver || player == null) return;

        elapsed += Time.deltaTime;
        float riseSpeed = settings.cameraRiseSpeedBase + settings.cameraRiseRampPerSec * elapsed;
        risingFloorY += riseSpeed * Time.deltaTime;

        if (mazeGenerator != null) mazeGenerator.EnsureGeneratedAhead(risingFloorY);

        // Camera follows whichever is higher: the player (when they're ahead) or the relentless
        // rising floor (when the player is lagging behind) - it never moves back down.
        float targetY = Mathf.Max(player.position.y, risingFloorY + verticalPadding);
        Vector3 targetPos = new Vector3(centerX, targetY, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

        float cameraBottomEdge = transform.position.y - cam.orthographicSize;
        if (player.position.y < cameraBottomEdge - settings.offscreenDeathMargin)
        {
            gameOver = true;
            OnPlayerCaught?.Invoke();
        }
    }
}
