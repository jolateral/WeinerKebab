using UnityEngine;

// Mostly your original script -- it was already solid. Removed the
// forced transform.localScale override so you can size each wire
// instance directly in the Inspector, matching the placeholder-square
// workflow you're using for the rest of the obstacles.
[RequireComponent(typeof(Collider2D))]
public class WireObstacle : MonoBehaviour
{
    [Header("Spark Flicker")]
    public float sparkInterval = 0.15f;

    private float sparkTimer;
    private SpriteRenderer sr;
    private readonly Color activeColour = new Color(1f, 1f, 0f);   // yellow
    private readonly Color sparkColour = new Color(1f, 0.5f, 0f);  // orange flash

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = activeColour;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.isGameOver) return;

        sparkTimer -= Time.deltaTime;
        if (sparkTimer <= 0f)
        {
            if (sr != null) sr.color = (sr.color == activeColour) ? sparkColour : activeColour;
            sparkTimer = sparkInterval;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!col.CompareTag("Player")) return;
        PlayerController pc = col.GetComponent<PlayerController>();
        if (pc != null) pc.ApplyZap();
    }
}
