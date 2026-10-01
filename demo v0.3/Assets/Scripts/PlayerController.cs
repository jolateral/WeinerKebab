using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public GameSettings settings;
    public Vector2 startDirection = Vector2.up;
    public float swipeThreshold = 30f;

    public SpriteRenderer spriteRenderer;

    [Header("Gun Setup")]
    public GameObject bulletPrefab;
    public Transform firePoint;

    [Header("Gun Feel / Fairness")]
    [Tooltip("Max distance a shot can register a hit at, in world units.")]
    public float shootRange = 6f;
    [Tooltip("Width of the hit-check lane swept forward, in world units. Wider than a single ray so a shot still registers even if the cat isn't perfectly centered in the corridor - this is the 'auto-aim' forgiveness. Keep it comfortably under one cell width so it doesn't reach through walls into the next lane.")]
    public float shootLaneWidth = 1f;

    private Rigidbody2D rb;
    private Vector2 currentVelocity;
    private Vector2 currentDirection;
    private float stunTimeRemaining = 0f;
    private float postStunGraceRemaining = 0f;
    private float steamMultiplier = 1f;
    private Vector2 fanForceThisFrame = Vector2.zero;

    private int currentAmmo = 0;

    public int CurrentAmmo => currentAmmo;
    public bool IsStunned => stunTimeRemaining > 0f;
    public bool IsInDeathGrace => IsStunned || postStunGraceRemaining > 0f;
    public Rigidbody2D Rigidbody => rb;
    public Vector2 CurrentVelocity => currentVelocity;

    public System.Action OnStunned;
    public System.Action<int> OnAmmoChanged;

    private Vector2 touchStartPos;
    private bool touchActive = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        currentDirection = startDirection.normalized;

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        ApplyFacingFromDirection(currentDirection);
    }

    void Start()
    {
        if (settings != null)
        {
            currentAmmo = settings.startingAmmo;
            OnAmmoChanged?.Invoke(currentAmmo);
        }
    }

    void Update()
    {
        if (stunTimeRemaining > 0f)
        {
            stunTimeRemaining -= Time.deltaTime;
            if (stunTimeRemaining <= 0f)
            {
                stunTimeRemaining = 0f;
                postStunGraceRemaining = settings.postStunDeathGraceSeconds;
            }
        }
        else if (postStunGraceRemaining > 0f)
        {
            postStunGraceRemaining -= Time.deltaTime;
        }

        ReadKeyboardTurn();
        ReadSwipeTurn();
        ReadShootInput();
    }

    void FixedUpdate()
    {
        float speed = IsStunned ? 0f : settings.playerSpeed * steamMultiplier * CurrentDirectionSpeedMultiplier();
        Vector2 targetVelocity = currentDirection * speed + fanForceThisFrame;

        currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-settings.playerAccel * Time.fixedDeltaTime));
        rb.linearVelocity = currentVelocity;

        fanForceThisFrame = Vector2.zero;
    }

    private float CurrentDirectionSpeedMultiplier()
    {
        return currentDirection == Vector2.up ? settings.verticalSpeedMultiplier : 1f;
    }

    private void ReadKeyboardTurn()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) SetDirection(Vector2.left);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) SetDirection(Vector2.right);
        else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) SetDirection(Vector2.up);
        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) SetDirection(Vector2.down);
    }

    private void ReadShootInput()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            Shoot();
        }
    }

    private void ReadSwipeTurn()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null) return;

        var touch = touchscreen.primaryTouch;
        var phase = touch.phase.ReadValue();

        if (phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            touchStartPos = touch.position.ReadValue();
            touchActive = true;
        }
        else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            if (touchActive)
            {
                Vector2 delta = touch.position.ReadValue() - touchStartPos;
                if (delta.magnitude >= swipeThreshold)
                {
                    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                        SetDirection(delta.x > 0 ? Vector2.right : Vector2.left);
                    else
                        SetDirection(delta.y > 0 ? Vector2.up : Vector2.down);
                }
            }
            touchActive = false;
        }
    }

    private void SetDirection(Vector2 dir)
    {
        currentDirection = dir;
        ApplyFacingFromDirection(dir);
    }

    private void ApplyFacingFromDirection(Vector2 dir)
    {
        if (spriteRenderer == null) return;
        if (dir.x > 0f) spriteRenderer.flipX = false;
        else if (dir.x < 0f) spriteRenderer.flipX = true;
    }

    public bool TryAddAmmo(int amount)
    {
        if (currentAmmo >= settings.maxAmmo) return false;
        currentAmmo = Mathf.Min(currentAmmo + amount, settings.maxAmmo);
        OnAmmoChanged?.Invoke(currentAmmo);
        return true;
    }

    public void Shoot()
    {
        if (currentAmmo <= 0 || bulletPrefab == null) return;

        currentAmmo--;
        OnAmmoChanged?.Invoke(currentAmmo);

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

        // Resolve the hit RIGHT NOW, at the exact moment the button is pressed, instead of waiting
        // for a projectile to travel there. This is what actually fixes "I shot first but the cat
        // still got me" - Update() runs before this frame's physics step, so a cat in the lane dies
        // before the physics engine even gets a chance to register the player touching it this
        // frame. The bullet instantiated below is now purely a visual tracer; it no longer decides
        // whether the shot landed.
        TryResolveHitScan(spawnPos, currentDirection);

        GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        Bullet bScript = bullet.GetComponent<Bullet>();
        if (bScript != null)
        {
            bScript.Init(currentDirection);
        }
    }

    private static readonly RaycastHit2D[] hitScanBuffer = new RaycastHit2D[16];

    // Sweeps a thin box forward from the muzzle. Anything tagged "Wall" blocks the shot from going
    // any further (so you can't shoot through walls into an adjacent corridor); the first thing
    // tagged "Cat" encountered before that gets killed instantly. Everything else (the player's own
    // collider, pickups, other bullets) is ignored and the scan just keeps going.
    //
    // Requires the enemy cat prefab to be tagged "Cat" (Project Settings > Tags and Layers, or the
    // Tag dropdown on the prefab) - add this tag if it doesn't exist yet.
    private void TryResolveHitScan(Vector3 origin, Vector2 direction)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.NoFilter();

        int count = Physics2D.BoxCast(origin, new Vector2(shootLaneWidth, shootLaneWidth), 0f,
            direction, filter, hitScanBuffer, shootRange);

        // Small buffer, simple manual sort by distance - avoids pulling in extra usings for a
        // one-off sort of at most 16 elements.
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (hitScanBuffer[j].distance < hitScanBuffer[i].distance)
                {
                    (hitScanBuffer[i], hitScanBuffer[j]) = (hitScanBuffer[j], hitScanBuffer[i]);
                }
            }
        }

        for (int i = 0; i < count; i++)
        {
            var col = hitScanBuffer[i].collider;
            if (col == null) continue;

            if (col.CompareTag("Wall")) return; // shot is blocked here - stop scanning further

            if (col.CompareTag("Cat"))
            {
                var cat = col.GetComponent<EnemyCat>();
                if (cat != null) cat.Kill();
                return;
            }
            // anything else (self collider, pickups, other bullets) - ignore and keep scanning
        }
    }

    public void SetInSteam(bool inSteam)
    {
        steamMultiplier = inSteam ? settings.steamSpeedMultiplier : 1f;
    }

    public void ApplyStun()
    {
        stunTimeRemaining = settings.stunDurationSeconds;
        OnStunned?.Invoke();
    }

    public void ApplyFanForce(Vector2 direction)
    {
        fanForceThisFrame += direction.normalized * settings.fanForce;
    }
}