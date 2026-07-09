using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WireObstacle : MonoBehaviour
{
    [Header("Spark Flicker")]
    public float sparkInterval = 0.15f;

    private float sparkTimer;
    private SpriteRenderer sr;
    private readonly Color activeColour = new Color(1f, 1f, 0f);
    private readonly Color sparkColour  = new Color(1f, 0.5f, 0f);

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = activeColour;
            sr.sortingOrder = 5; // must draw above pipes (order 0)
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.isGameOver) return;
        sparkTimer -= Time.deltaTime;
        if (sparkTimer <= 0f)
        {
            if (sr != null)
                sr.color = (sr.color == activeColour) ? sparkColour : activeColour;
            sparkTimer = sparkInterval;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!col.CompareTag("Player")) return;
        PlayerController pc = col.GetComponent<PlayerController>();
        // ApplyZap() already does a full stop for stunDuration seconds
        if (pc != null) pc.ApplyZap();
    }
}