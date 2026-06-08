using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;

    [Header("Fall Gravity")]
    public float fallGravityMultiplier = 3.5f;
    public float maxFallSpeed = 20f;

    [Header("State Modifiers")]
    public float fanResistance = 0.5f;
    public float fanAcceleration = 1.8f;
    public float steamSlowdown = 0.4f;
    public float zapDuration = 1.2f;

    private Rigidbody2D rb;
    private int groundContactCount = 0;
    private bool isZapped = false;
    private float zapTimer = 0f;

    // Tracks which horizontal directions are currently blocked by a wall
    // -1 = left wall touching, 1 = right wall touching, 0 = neither
    private int wallContactDirection = 0;

    [HideInInspector] public float fanMultiplier = 1f;
    [HideInInspector] public bool inSteam = false;
    [HideInInspector] public bool isGliding = false;

    private float glideTimer = 0f;
    private float glideDuration = 0.8f;

    private SpriteRenderer sr;
    private Color normalColor = new Color(0f,   1f,  0.53f);
    private Color zappedColor = new Color(1f,   1f,  0f);
    private Color steamColor  = new Color(0.8f, 0.8f,1f);
    private Color glideColor  = new Color(1f,   0.5f,0f);

    private bool IsGrounded => groundContactCount > 0;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        sr.color = normalColor;
    }

    private void Update()
    {
        if (GameManager.Instance.isGameOver) return;

        if (isZapped)
        {
            HandleZap();
            return;
        }

        HandleMovement();
        HandleJump();
        HandleGlide();
        ApplyFallGravity();
        UpdateColour();
    }

    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float speedMod = inSteam ? steamSlowdown : 1f;
        speedMod *= fanMultiplier;

        float desiredVX = h * moveSpeed * speedMod;

        // If the player is pressing into a wall, zero out movement in that direction.
        // This prevents sticking regardless of whether they are grounded or airborne.
        // Example: wallContactDirection = 1 means a right wall is touching.
        // If h is also positive (pressing right into the wall), block it.
        if (wallContactDirection != 0 && Mathf.Sign(h) == Mathf.Sign(wallContactDirection))
        {
            desiredVX = 0f;
        }

        rb.velocity = new Vector2(desiredVX, rb.velocity.y);
    }

    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && IsGrounded)
        {
            float h = Input.GetAxis("Horizontal");
            float speedMod = inSteam ? steamSlowdown : 1f;
            speedMod *= fanMultiplier;

            rb.velocity = new Vector2(h * moveSpeed * speedMod, jumpForce);

            if (fanMultiplier >= fanAcceleration)
            {
                isGliding = true;
                glideTimer = glideDuration;
            }
        }
    }

    private void HandleGlide()
    {
        if (!isGliding) return;
        glideTimer -= Time.deltaTime;
        if (glideTimer <= 0f) { isGliding = false; return; }
        rb.velocity = new Vector2(rb.velocity.x, Mathf.Max(rb.velocity.y, -1f));
    }

    private void ApplyFallGravity()
    {
        if (isGliding) return;

        if (rb.velocity.y < 0f)
        {
            rb.velocity += Vector2.up * Physics2D.gravity.y
                         * (fallGravityMultiplier - 1f) * Time.deltaTime;

            rb.velocity = new Vector2(rb.velocity.x,
                          Mathf.Max(rb.velocity.y, -maxFallSpeed));
        }
    }

    private void HandleZap()
    {
        zapTimer -= Time.deltaTime;
        sr.color = Time.time % 0.2f < 0.1f ? zappedColor : normalColor;
        if (zapTimer <= 0f)
        {
            isZapped = false;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    public void ApplyZap()
    {
        if (isZapped) return;
        isZapped = true;
        zapTimer = zapDuration;
        rb.velocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private void UpdateColour()
    {
        if (isGliding)    sr.color = glideColor;
        else if (inSteam) sr.color = steamColor;
        else              sr.color = normalColor;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        foreach (ContactPoint2D contact in col.contacts)
        {
            // Floor contact — normal points strongly upward
            if (contact.normal.y > 0.7f)
            {
                groundContactCount++;
                isGliding = false;
            }

            // Wall contact — normal is mostly horizontal, not a floor
            // normal.x > 0 means a wall to the LEFT is pushing us right
            // normal.x < 0 means a wall to the RIGHT is pushing us left
            // We store the direction the wall is ON (opposite of normal)
            if (Mathf.Abs(contact.normal.x) > 0.7f && Mathf.Abs(contact.normal.y) < 0.3f)
            {
                // If normal points right (+x), wall is to our left (-1)
                // If normal points left (-x), wall is to our right (+1)
                wallContactDirection = contact.normal.x > 0f ? -1 : 1;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        // Recount floor contacts in case player is still on another platform
        groundContactCount = 0;
        wallContactDirection = 0;

        foreach (ContactPoint2D contact in col.contacts)
        {
            if (contact.normal.y > 0.7f)
                groundContactCount++;

            if (Mathf.Abs(contact.normal.x) > 0.7f && Mathf.Abs(contact.normal.y) < 0.3f)
                wallContactDirection = contact.normal.x > 0f ? -1 : 1;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Cat"))
            GameManager.Instance.TriggerGameOver();
    }
}