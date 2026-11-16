using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public GameSettings settings;
    public Vector2 startDirection = Vector2.up;
    public float swipeThreshold = 30f;

    [Header("Junction Assist (forgiving turns)")]
    [Tooltip("Auto-found at startup if left empty. Needed so turns can be matched to the real maze cells.")]
    public MazeGenerator mazeGenerator;
    [Tooltip("Master switch. Off = the old behavior (turn instantly, bump into the wall if it's closed).")]
    public bool enableJunctionAssist = true;
    [Tooltip("If a swipe can't be used yet (wall in that direction), it is remembered this long and used at the next opening. 0.35-0.5 feels good.")]
    public float turnBufferSeconds = 0.4f;
    [Range(0f, 0.8f)]
    [Tooltip("How early before a junction's center the turn is allowed to happen, as a fraction of one cell. Bigger = turns happen sooner after an early swipe.")]
    public float junctionEarlyCells = 0.3f;
    [Range(0f, 0.8f)]
    [Tooltip("How far PAST a junction's center a late swipe still counts, as a fraction of one cell. Bigger = more forgiving of late swipes.")]
    public float junctionLateCells = 0.4f;
    [Tooltip("After a turn, the player is smoothly pulled onto the new pipe's centerline for this many seconds.")]
    public float turnSnapSeconds = 0.15f;
    [Tooltip("How fast (world units/sec) that centering pull is.")]
    public float turnSnapSpeed = 14f;
    [Range(0.3f, 1f)]
    [Tooltip("Speed multiplier while crossing a true junction (path continues forward AND a side opening exists). 1 = no slowdown. 0.8 is subtle.")]
    public float junctionSlowMultiplier = 0.8f;
    [Tooltip("Register a swipe the moment the finger has dragged far enough, instead of waiting for the finger to lift. Feels much snappier.")]
    public bool swipeOnDrag = true;

    public SpriteRenderer spriteRenderer;

    [Header("Characters (cosmetic swap)")]
    [Tooltip("The Animator that drives the player's sprite. Auto-found on this object or its children if left empty.")]
    public Animator animator;
    [Tooltip("Element 0 = the character you START as (Glork). Element 1 = the partner you can swap to (Flerb).")]
    public CharacterDefinition[] characters;
    [Tooltip("Index into the Characters list of who the player is right now.")]
    public int currentCharacterIndex = 0;

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
    public System.Action<int> OnCharacterSwapped; // passes the NEW character index

    public bool HasCharacters => characters != null && characters.Length >= 2;
    public CharacterDefinition CurrentCharacter => HasCharacters ? characters[currentCharacterIndex % characters.Length] : null;
    // The character the player is NOT currently playing as - this is who waits in the wide-band mazes.
    public CharacterDefinition OtherCharacter => HasCharacters ? characters[(currentCharacterIndex + 1) % characters.Length] : null;

    private Vector2 touchStartPos;
    private bool touchActive = false;

    // Junction assist state
    private Vector2 bufferedDir = Vector2.zero;
    private float bufferTimeRemaining = 0f;
    private float snapRemaining = 0f;
    private Vector2 snapCenter;
    private float junctionSpeedFactor = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        currentDirection = startDirection.normalized;

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (mazeGenerator == null) mazeGenerator = FindFirstObjectByType<MazeGenerator>();
        ApplyFacingFromDirection(currentDirection);
        ApplyCharacterVisuals();
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
        // Try to use a remembered swipe, then age it.
        if (bufferTimeRemaining > 0f)
        {
            TryApplyBufferedTurn();
            bufferTimeRemaining = Mathf.Max(0f, bufferTimeRemaining - Time.fixedDeltaTime);
        }

        junctionSpeedFactor = ComputeJunctionSpeedFactor();

        float speed = IsStunned ? 0f : settings.playerSpeed * steamMultiplier * CurrentDirectionSpeedMultiplier() * junctionSpeedFactor;
        Vector2 targetVelocity = currentDirection * speed + fanForceThisFrame;

        currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-settings.playerAccel * Time.fixedDeltaTime));

        // Right after a turn, glide onto the new pipe's centerline so the player never clips a corner.
        if (snapRemaining > 0f)
        {
            snapRemaining -= Time.fixedDeltaTime;
            Vector2 p = rb.position;
            float step = turnSnapSpeed * Time.fixedDeltaTime;
            if (Mathf.Abs(currentDirection.x) < 0.5f)       // now moving vertically -> align X
                rb.position = new Vector2(Mathf.MoveTowards(p.x, snapCenter.x, step), p.y);
            else                                              // now moving horizontally -> align Y
                rb.position = new Vector2(p.x, Mathf.MoveTowards(p.y, snapCenter.y, step));
        }

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

        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) RequestTurn(Vector2.left);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) RequestTurn(Vector2.right);
        else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) RequestTurn(Vector2.up);
        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) RequestTurn(Vector2.down);
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
        else if (phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
        {
            // Fire as soon as the drag is long enough (once per touch) instead of waiting for release.
            if (swipeOnDrag && touchActive && TryFireSwipe(touch.position.ReadValue()))
            {
                touchActive = false;
            }
        }
        else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            if (touchActive) TryFireSwipe(touch.position.ReadValue());
            touchActive = false;
        }
    }

    private bool TryFireSwipe(Vector2 currentPos)
    {
        Vector2 delta = currentPos - touchStartPos;
        if (delta.magnitude < swipeThreshold) return false;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            RequestTurn(delta.x > 0 ? Vector2.right : Vector2.left);
        else
            RequestTurn(delta.y > 0 ? Vector2.up : Vector2.down);
        return true;
    }

    // ---------------------------------------------------------------------------------------
    // JUNCTION ASSIST
    // Every input goes through RequestTurn. U-turns and "already going that way" are handled
    // instantly. Sideways turns are validated against the real maze: if that side is open in the
    // cell the player is in (and they're close enough to its center), the turn happens now;
    // otherwise the swipe is remembered for turnBufferSeconds and used the moment it becomes
    // possible. Result: swiping early never bumps into a wall - you just flow into the pipe.
    // ---------------------------------------------------------------------------------------
    private void RequestTurn(Vector2 dir)
    {
        if (!enableJunctionAssist || mazeGenerator == null)
        {
            SetDirection(dir);
            return;
        }

        if (dir == currentDirection)
        {
            bufferTimeRemaining = 0f;
            return;
        }

        if (dir == -currentDirection)
        {
            bufferTimeRemaining = 0f;
            SetDirection(dir); // reversing is always possible - you came from there
            return;
        }

        bufferedDir = dir;
        bufferTimeRemaining = turnBufferSeconds;
        TryApplyBufferedTurn(); // maybe it's already valid right now
    }

    private void TryApplyBufferedTurn()
    {
        if (bufferTimeRemaining <= 0f || mazeGenerator == null) return;

        Vector2 pos = rb.position;
        mazeGenerator.WorldToCell(pos, out int row, out int col);
        MazeCell cell = mazeGenerator.CellAt(row, col);
        if (cell == null) return;
        if (!IsOpen(cell, bufferedDir)) return; // not an opening here - keep waiting

        Vector3 center3 = mazeGenerator.CellWorldCenter(row, col);
        Vector2 center = new Vector2(center3.x, center3.y);
        float cs = mazeGenerator.CellSize;

        // Positive = already past the junction's center, negative = still approaching it.
        float along = Vector2.Dot(pos - center, currentDirection);
        if (along < -junctionEarlyCells * cs) return;  // too early - wait until we're closer
        if (along > junctionLateCells * cs) return;    // too late for this junction

        SetDirection(bufferedDir);
        bufferTimeRemaining = 0f;
        snapRemaining = turnSnapSeconds;
        snapCenter = center;
    }

    private float ComputeJunctionSpeedFactor()
    {
        if (!enableJunctionAssist || mazeGenerator == null || junctionSlowMultiplier >= 0.999f) return 1f;

        mazeGenerator.WorldToCell(rb.position, out int row, out int col);
        MazeCell cell = mazeGenerator.CellAt(row, col);
        if (cell == null) return 1f;

        // Only a TRUE junction slows you: the path continues ahead AND there's a side opening.
        if (!IsOpen(cell, currentDirection)) return 1f;
        Vector2 sideA = new Vector2(-currentDirection.y, currentDirection.x);
        bool sideOpen = IsOpen(cell, sideA) || IsOpen(cell, -sideA);
        return sideOpen ? junctionSlowMultiplier : 1f;
    }

    private static bool IsOpen(MazeCell cell, Vector2 dir)
    {
        if (dir.y > 0.5f) return !cell.wallN;
        if (dir.y < -0.5f) return !cell.wallS;
        if (dir.x > 0.5f) return !cell.wallE;
        if (dir.x < -0.5f) return !cell.wallW;
        return false;
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

    // Purely cosmetic: swaps which character's running animation is playing. Controls, speed, ammo,
    // hitbox - everything gameplay-related - stays exactly the same.
    public void SwapCharacter()
    {
        if (!HasCharacters) return;
        currentCharacterIndex = (currentCharacterIndex + 1) % characters.Length;
        ApplyCharacterVisuals();
        OnCharacterSwapped?.Invoke(currentCharacterIndex);
    }

    private void ApplyCharacterVisuals()
    {
        var def = CurrentCharacter;
        if (def == null) return;
        if (animator != null && def.runningController != null)
        {
            animator.runtimeAnimatorController = def.runningController;
        }
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