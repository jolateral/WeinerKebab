using UnityEngine;

public class WireObstacle : MonoBehaviour
{
    [Header("Wire Settings")]
    public float sparkInterval = 0.15f;

    private float sparkTimer = 0f;
    private SpriteRenderer sr;
    private Color activeColour = new Color(1f, 1f, 0f);      // yellow
    private Color sparkColour = new Color(1f, 0.5f, 0f);     // orange flash

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.color = activeColour;
        transform.localScale = new Vector3(1.2f, 0.25f, 1f);
    }

    private void Update()
    {
        if (GameManager.Instance.isGameOver) return;

        // Flicker spark effect
        sparkTimer -= Time.deltaTime;
        if (sparkTimer <= 0f)
        {
            sr.color = (sr.color == activeColour) ? sparkColour : activeColour;
            sparkTimer = sparkInterval;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
        {
            PlayerController pc = col.GetComponent<PlayerController>();
            if (pc != null) pc.ApplyZap();
        }
    }
}