using UnityEngine;
using UnityEngine.InputSystem;

// Attach to the Player GameObject alongside: Rigidbody2D (gravity scale 0, freeze Z rotation),
// CircleCollider2D (not a trigger).
// Requires the Input System package (Window > Package Manager > Input System) -
// this script uses it directly via Keyboard.current / Touchscreen.current, no Input Actions asset needed.
//
// Movement model: the player moves continuously in `currentDirection` at all times (Pac-Man style).
// An arrow-key press or a swipe sets a new `currentDirection` - it doesn't have to be held down.
// Since corridors are single-width, an attempted turn into a wall is simply blocked by physics
// collision until the player is at a junction where that direction is actually open.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public GameSettings settings;
    [Tooltip("Direction the player starts moving in before any input.")]
    public Vector2 startDirection = Vector2.up;
    [Tooltip("Minimum swipe distance in pixels before it counts as a direction change.")]
    public float swipeThreshold = 30f;

    [Tooltip("Sprite to mirror left/right as the player turns. Leave empty to auto-find on this GameObject or its children.")]
    public SpriteRenderer spriteRenderer;

    private Rigidbody2D rb;
    private Vector2 currentVelocity;
    private Vector2 currentDirection;
    private float stunTimeRemaining = 0f;
    private float postStunGraceRemaining = 0f;
    private float steamMultiplier = 1f;
    private Vector2 fanForceThisFrame = Vector2.zero;

    public bool IsStunned => stunTimeRemaining > 0f;

    // True while stunned, or for a short grace window right after a stun ends - CameraFollow
    // checks this so a wire hit can't chain directly into a flood death with no recovery window.
    public bool IsInDeathGrace => IsStunned || postStunGraceRemaining > 0f;

    public Rigidbody2D Rigidbody => rb;
    public Vector2 CurrentVelocity => currentVelocity;

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
    }

    void FixedUpdate()
    {
        float speed = IsStunned ? 0f : settings.playerSpeed * steamMultiplier;
        Vector2 targetVelocity = currentDirection * speed + fanForceThisFrame;

        currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-settings.playerAccel * Time.fixedDeltaTime));
        rb.linearVelocity = currentVelocity;

        // Fan force is re-applied every frame the player stays in a fan trigger (OnTriggerStay2D).
        fanForceThisFrame = Vector2.zero;
    }

    private void ReadKeyboardTurn()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Use "this frame" presses, not held state, so a tap is enough - the player keeps moving after.
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) SetDirection(Vector2.left);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) SetDirection(Vector2.right);
        else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) SetDirection(Vector2.up);
        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) SetDirection(Vector2.down);
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
                    // Snap to the dominant axis so diagonal swipes still read as a clean turn.
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

    // Mirrors the sprite left/right on horizontal turns. Left/right in this Pac-Man-style movement
    // is the only case that needs a flip - up/down keeps whatever horizontal facing was last set,
    // since there's nothing to mirror on a purely vertical turn.
    private void ApplyFacingFromDirection(Vector2 dir)
    {
        if (spriteRenderer == null) return;
        if (dir.x > 0f) spriteRenderer.flipX = false;
        else if (dir.x < 0f) spriteRenderer.flipX = true;
    }

    // --- Called by hazard trigger scripts ---
    public void SetInSteam(bool inSteam)
    {
        steamMultiplier = inSteam ? settings.steamSpeedMultiplier : 1f;
    }

    public void ApplyStun()
    {
        stunTimeRemaining = settings.stunDurationSeconds;
    }

    public void ApplyFanForce(Vector2 direction)
    {
        fanForceThisFrame += direction.normalized * settings.fanForce;
    }
}