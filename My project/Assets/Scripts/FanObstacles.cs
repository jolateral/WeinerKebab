using UnityEngine;

public class FanObstacle : MonoBehaviour
{
    [Header("Fan Settings")]
    public float onDuration        = 3f;
    public float offDuration       = 2f;
    public float beamLength        = 3.5f;   // how far the wind reaches horizontally
    public float blowDirection     = 1f;     // 1 = blows right, -1 = blows left

    [Header("Speed Modifiers")]
    public float resistanceMultiplier   = 0.45f;
    public float accelerationMultiplier = 1.9f;

    private bool isOn   = false;
    private float timer = 0f;
    private SpriteRenderer sr;
    private PlayerController player;
    private bool playerInBeam = false;

    private Color onColour  = new Color(0f,  0.8f, 1f);
    private Color offColour = new Color(0.2f,0.4f, 0.5f);

    // The beam height is derived from the fan's actual rendered height,
    // set once in Start after the sprite and scale are finalised
    private float beamHeight;

    private void Start()
    {
        sr     = GetComponent<SpriteRenderer>();
        player = FindFirstObjectByType<PlayerController>();

        blowDirection = Random.value > 0.5f ? 1f : -1f;
        transform.localScale = new Vector3(0.6f * blowDirection, 0.6f, 1f);

        // Measure the fan's world-space height from its collider bounds
        // so the beam exactly matches whatever size the fan is
        Collider2D col = GetComponent<Collider2D>();
        beamHeight = col != null ? col.bounds.size.y : Mathf.Abs(transform.localScale.y);

        isOn  = Random.value > 0.5f;
        timer = isOn ? Random.Range(0f, onDuration) : Random.Range(0f, offDuration);
        UpdateColour();
    }

    private void Update()
    {
        if (GameManager.Instance.isGameOver) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            isOn  = !isOn;
            timer = isOn ? onDuration : offDuration;
            UpdateColour();
        }

        if (!isOn) { ClearEffect(); return; }

        CheckBeam();
    }

   private void CheckBeam()
{
    if (player == null) return;

    // Cast a box from the fan's edge outward in the blow direction.
    // The cast stops at the first collider it hits — if that's a platform,
    // the player behind it is unaffected. Only if the player is the first
    // thing hit does the fan effect apply.
    Vector2 origin    = (Vector2)transform.position;
    Vector2 direction = Vector2.right * blowDirection;
    Vector2 castSize  = new Vector2(0.05f, beamHeight); // thin leading edge

    RaycastHit2D hit = Physics2D.BoxCast(
        origin,
        castSize,
        0f,
        direction,
        beamLength
    );

    // Nothing hit, or hit something that isn't the player — clear and return
    if (hit.collider == null || !hit.collider.CompareTag("Player"))
    {
        ClearEffect();
        return;
    }

    // The player is the first thing the beam hits — apply effect
    playerInBeam = true;

    float playerVelX = player.GetComponent<Rigidbody2D>().velocity.x;
    bool walkingAgainstFan = playerVelX * blowDirection < 0f;

    player.fanMultiplier = walkingAgainstFan
        ? resistanceMultiplier
        : accelerationMultiplier;
}

    private void ClearEffect()
    {
        if (playerInBeam && player != null)
        {
            player.fanMultiplier = 1f;
            playerInBeam = false;
        }
    }

    private void UpdateColour()
    {
        if (sr != null) sr.color = isOn ? onColour : offColour;
    }

    private void OnDestroy()
    {
        if (player != null) player.fanMultiplier = 1f;
    }

    private void OnDrawGizmosSelected()
    {
        // Shows the exact beam box in the Scene view so you can visually verify it
        Gizmos.color = Color.cyan;
        float height = Application.isPlaying
            ? beamHeight
            : Mathf.Abs(transform.localScale.y);
        Vector3 beamCenter = transform.position
                           + Vector3.right * blowDirection * (beamLength / 2f);
        Gizmos.DrawWireCube(beamCenter, new Vector3(beamLength, height, 0));
    }
}