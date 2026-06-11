using UnityEngine;

public class FanObstacle : MonoBehaviour
{
    [Header("Fan Settings")]
    public float onDuration            = 3f;
    public float offDuration           = 2f;
    public float beamLength            = 3.5f;
    public float blowDirection         = 1f;   // set by LevelGenerator, not randomised

    [Header("Speed Modifiers")]
    public float resistanceMultiplier   = 0.45f;
    public float accelerationMultiplier = 1.9f;

    private bool isOn      = false;
    private float timer    = 0f;
    private bool directionSet = false;  // true once LevelGenerator has called SetDirection
    private SpriteRenderer sr;
    private PlayerController player;
    private bool playerInBeam = false;
    private float beamHeight;

    private Color onColour  = new Color(0f,  0.8f, 1f);
    private Color offColour = new Color(0.2f,0.4f, 0.5f);

    // Called by LevelGenerator immediately after Instantiate
    public void SetDirection(float direction)
    {
        blowDirection = direction;
        directionSet  = true;
        ApplyDirection();
    }

    private void ApplyDirection()
    {
        // Flip the sprite to face the blow direction
        transform.localScale = new Vector3(0.6f * blowDirection, 0.6f, 1f);
    }

    private void Start()
    {
        sr     = GetComponent<SpriteRenderer>();
        player = FindFirstObjectByType<PlayerController>();

        // Only randomise direction if LevelGenerator didn't set it
        // (e.g. during editor testing with a manually placed fan)
        if (!directionSet)
        {
            blowDirection = Random.value > 0.5f ? 1f : -1f;
            ApplyDirection();
        }

        Collider2D col = GetComponent<Collider2D>();
        beamHeight = col != null
            ? col.bounds.size.y
            : Mathf.Abs(transform.localScale.y);

        isOn  = Random.value > 0.5f;
        timer = isOn
            ? Random.Range(0f, onDuration)
            : Random.Range(0f, offDuration);

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

        Vector2 origin    = (Vector2)transform.position;
        Vector2 direction = Vector2.right * blowDirection;
        Vector2 castSize  = new Vector2(0.05f, beamHeight);

        RaycastHit2D hit = Physics2D.BoxCast(
            origin, castSize, 0f, direction, beamLength);

        if (hit.collider == null || !hit.collider.CompareTag("Player"))
        {
            ClearEffect();
            return;
        }

        playerInBeam = true;

        float playerVelX      = player.GetComponent<Rigidbody2D>().velocity.x;
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
        Gizmos.color = Color.cyan;
        float height = Application.isPlaying
            ? beamHeight
            : Mathf.Abs(transform.localScale.y);
        Vector3 beamCenter = transform.position
                           + Vector3.right * blowDirection * (beamLength / 2f);
        Gizmos.DrawWireCube(beamCenter, new Vector3(beamLength, height, 0));
    }
}