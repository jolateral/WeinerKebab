using UnityEngine;

public class CatController : MonoBehaviour
{
    [Header("Cat Settings")]
    public float descendSpeed = 3f;
    public float horizontalWanderSpeed = 1.5f;
    public float wanderAmplitude = 2f;   // how wide it sways left/right

    private float startX;
    private float timeAlive = 0f;
    private CameraScroller cam;

    private void Start()
    {
        startX = transform.position.x;
        cam = FindFirstObjectByType<CameraScroller>();

        transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        GetComponent<SpriteRenderer>().color = new Color(1f, 0.55f, 0f); // orange
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (GameManager.Instance.isGameOver) return;

        timeAlive += Time.deltaTime;

        // Move downward
        float newY = transform.position.y - descendSpeed * Time.deltaTime;

        // Gentle sinusoidal horizontal wander
        float newX = startX + Mathf.Sin(timeAlive * horizontalWanderSpeed) * wanderAmplitude;

        transform.position = new Vector3(newX, newY, 0);

        // Destroy once below camera view (camera has scrolled past it)
        if (cam != null && transform.position.y < cam.GetBottomEdge() - 2f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
        {
            GameManager.Instance.TriggerGameOver();
        }
    }
}