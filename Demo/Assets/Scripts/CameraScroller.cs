using UnityEngine;

public class CameraScroller : MonoBehaviour
{
    [Header("Scroll Settings")]
    public float baseScrollSpeed = 1.5f;
    public float speedIncreasePerFloor = 0.02f;
    public float maxScrollSpeed = 6f;

    [Header("References")]
    public Transform player;

    private float scrollSpeed;
    private Camera cam;

    private void Start()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        if (GameManager.Instance.isGameOver) return;

        // Speed increases with floors
        scrollSpeed = Mathf.Min(
            baseScrollSpeed + GameManager.Instance.currentFloor * speedIncreasePerFloor,
            maxScrollSpeed
        );

        transform.position += Vector3.up * scrollSpeed * Time.deltaTime;

        // Check if player has fallen below camera view
        if (player != null)
        {
            float bottomEdge = transform.position.y - cam.orthographicSize;
            if (player.position.y < bottomEdge - 0.5f) // 0.5 grace buffer
            {
                GameManager.Instance.TriggerGameOver();
            }
        }
    }

    public float GetBottomEdge()
    {
        return transform.position.y - cam.orthographicSize;
    }

    public float GetTopEdge()
    {
        return transform.position.y + cam.orthographicSize;
    }

    public float GetScrollSpeed()
    {
        return scrollSpeed;
    }
}