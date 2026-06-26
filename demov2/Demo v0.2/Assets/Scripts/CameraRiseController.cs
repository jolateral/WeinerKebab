using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraRiseController : MonoBehaviour
{
    public static CameraRiseController Instance;

    [Header("References")]
    public Transform player;

    [Header("Rise Speed")]
    public float startRiseSpeed = 1f;
    public float riseAcceleration = 0.015f;
    public float maxRiseSpeed = 3.5f;

    [Header("Kill Zone")]
    [Tooltip("How far below the camera bottom edge before death triggers")]
    public float killZoneBuffer = 0.5f;

    [Header("Portrait Framing")]
    [Tooltip("How far above the player the camera looks — lets player see upcoming mazes")]
    public float lookAheadAbovePlayer = 6f;
    [Tooltip("Smoothing on the vertical camera follow")]
    public float verticalFollowSpeed = 1.5f;

    private Camera cam;
    private float currentRiseSpeed;
    private float targetY;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        currentRiseSpeed = startRiseSpeed;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.isGameOver) return;

        // Accelerate rise
        currentRiseSpeed = Mathf.Min(maxRiseSpeed,
            currentRiseSpeed + riseAcceleration * Time.deltaTime);

        // Target Y keeps the player in the lower portion of screen
        // with lots of look-ahead above so they can see upcoming mazes
        if (player != null)
            targetY = player.position.y + lookAheadAbovePlayer;

        Vector3 pos = transform.position;
        // Rise constantly but also smoothly follow the target
        pos.y += currentRiseSpeed * Time.deltaTime;
        pos.y = Mathf.Max(pos.y, Mathf.Lerp(pos.y, targetY,
            verticalFollowSpeed * Time.deltaTime));
        // X stays fixed — no horizontal follow for portrait mode
        pos.x = 0f;
        transform.position = pos;

        // Kill zone check
        if (player != null)
        {
            float bottomEdge = transform.position.y - cam.orthographicSize - killZoneBuffer;
            if (player.position.y < bottomEdge)
                GameManager.Instance.GameOver();
        }
    }

    public float CurrentRiseSpeed => currentRiseSpeed;
}