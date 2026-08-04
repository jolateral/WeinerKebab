using UnityEngine;

// Attach to the Main Camera. Use an orthographic camera for a clean 2D top-down vent view.
public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public RisingFlood flood;
    public GameSettings settings;
    [Tooltip("How far above the flood line the camera keeps its bottom edge.")]
    public float verticalPadding = 3f;
    public float smoothTime = 0.15f;
    [Tooltip("Extra breathing room added to the maze width when fitting the camera, in world units.")]
    public float horizontalPadding = 0.4f;

    private Vector3 velocity = Vector3.zero;
    private float centerX;

    void Awake()
    {
        Camera cam = GetComponent<Camera>();
        cam.orthographic = true;

        float mazeWidth = settings.columns * settings.cellSize;
        centerX = mazeWidth * 0.5f;

        // Fit the maze width exactly into the current screen's aspect ratio (e.g. 390x844 portrait).
        float aspect = (float)Screen.width / Screen.height;
        cam.orthographicSize = (mazeWidth + horizontalPadding) / (2f * aspect);

        transform.position = new Vector3(centerX, transform.position.y, transform.position.z);
    }

    void LateUpdate()
    {
        if (player == null || flood == null) return;

        // Keep the camera between the player (top bias) and the flood (bottom bound),
        // so the flood is always visible creeping up from the bottom of the screen.
        float targetY = Mathf.Max(player.position.y, flood.floodWorldY + verticalPadding);
        Vector3 targetPos = new Vector3(centerX, targetY, transform.position.z);

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);
    }
}

