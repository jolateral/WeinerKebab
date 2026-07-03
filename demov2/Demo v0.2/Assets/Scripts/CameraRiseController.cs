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
    public float killZoneBuffer = 0.5f;

    [Header("Portrait Framing")]
    public float lookAheadAbovePlayer = 10f;
    public float verticalFollowSpeed = 3f;

    private Camera cam;
    private float currentRiseSpeed;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        currentRiseSpeed = startRiseSpeed;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.isGameOver) return;

        currentRiseSpeed = Mathf.Min(maxRiseSpeed,
            currentRiseSpeed + riseAcceleration * Time.deltaTime);

        Vector3 pos = transform.position;

        if (player != null)
        {
            float targetY = player.position.y + lookAheadAbovePlayer;
            float risenY = pos.y + currentRiseSpeed * Time.deltaTime;
            float followedY = Mathf.Lerp(pos.y, targetY, verticalFollowSpeed * Time.deltaTime);
            pos.y = Mathf.Max(risenY, followedY);
        }
        else
        {
            pos.y += currentRiseSpeed * Time.deltaTime;
        }

        pos.x = 0f;
        transform.position = pos;

        if (player != null)
        {
            float bottomEdge = transform.position.y - cam.orthographicSize - killZoneBuffer;
            if (player.position.y < bottomEdge)
                GameManager.Instance.GameOver();
        }
    }

    public float CurrentRiseSpeed => currentRiseSpeed;
}