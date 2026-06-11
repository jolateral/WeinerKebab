using UnityEngine;

public class ChuteObstacle : MonoBehaviour
{
    [Header("Chute Settings")]
    public float dropDistance = 3.5f;

    private bool isDropping = false;
    private BoxCollider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>() as BoxCollider2D;
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // Thin horizontal strip — only at the very top of the gap
        // so the player must step INTO it from above, not bump it from below
        col.size   = new Vector2(1f, 0.15f);
        col.offset = new Vector2(0f,  0.1f); // sit near the top edge of the gap

        GetComponent<SpriteRenderer>().color = new Color(0.67f, 0f, 1f, 0.45f);
        transform.localScale = Vector3.one; // scale via SetWidth instead
    }

    // Called by LevelGenerator so the trigger width matches the platform gap exactly
    public void SetWidth(float width)
    {
        if (col != null) col.size = new Vector2(width, 0.15f);
        // Visual scale to match
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(width, s.y, s.z);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || isDropping) return;

        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        PlayerController pc = other.GetComponent<PlayerController>();
        if (rb == null || pc == null) return;

        // Only fire when player is moving downward (stepping into the chute)
        // Positive Y velocity = jumping upward through the gap = ignore
        if (rb.velocity.y > 0.1f) return;

        StartCoroutine(DropPlayer(pc, rb));
    }

    private System.Collections.IEnumerator DropPlayer(PlayerController pc, Rigidbody2D rb)
    {
        isDropping = true;

        rb.velocity    = Vector2.zero;
        rb.isKinematic = true;

        Vector3 originalScale = pc.transform.localScale;
        SpriteRenderer sr     = pc.GetComponent<SpriteRenderer>();
        Color originalColor   = sr.color;

        // Parachute visual
        pc.transform.localScale = new Vector3(originalScale.x * 3f, originalScale.y * 0.4f, 1f);
        sr.color = new Color(1f, 0.5f, 0f);

        float elapsed      = 0f;
        float floatDuration = 1.2f;
        Vector3 startPos   = pc.transform.position;
        Vector3 targetPos  = startPos + Vector3.down * dropDistance;

        while (elapsed < floatDuration)
        {
            elapsed += Time.deltaTime;
            pc.transform.position = Vector3.Lerp(startPos, targetPos, elapsed / floatDuration);
            yield return null;
        }

        pc.transform.localScale = originalScale;
        sr.color       = originalColor;
        rb.isKinematic = false;

        yield return new WaitForSeconds(0.5f);
        isDropping = false;
    }
}