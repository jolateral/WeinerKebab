using UnityEngine;
using UnityEngine.InputSystem;

// Attach to the Player GameObject alongside: Rigidbody2D (gravity scale 0, freeze Z rotation),
// CircleCollider2D (not a trigger).
// Requires the Input System package (Window > Package Manager > Input System) -
// this script uses it directly via Keyboard.current / Touchscreen.current, no Input Actions asset needed.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public GameSettings settings;

    private Rigidbody2D rb;
    private Vector2 currentVelocity;
    private float stunTimeRemaining = 0f;
    private float steamMultiplier = 1f;
    private Vector2 fanForceThisFrame = Vector2.zero;

    public bool IsStunned => stunTimeRemaining > 0f;

    // Simple swipe tracking for touch input.
    private Vector2 touchStartPos;
    private bool touchActive = false;
    private Vector2 swipeDirection = Vector2.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    void Update()
    {
        if (stunTimeRemaining > 0f) stunTimeRemaining -= Time.deltaTime;
        HandleTouchInput();
    }

    void FixedUpdate()
    {
        Vector2 inputDir = GetInputDirection();

        float speed = IsStunned ? 0f : settings.playerSpeed * steamMultiplier;
        Vector2 targetVelocity = inputDir * speed + fanForceThisFrame;

        currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-settings.playerAccel * Time.fixedDeltaTime));
        rb.linearVelocity = currentVelocity;

        // Fan force is re-applied every frame the player stays in a fan trigger (OnTriggerStay2D).
        // Reset here so that leaving the trigger naturally removes the force next frame.
        fanForceThisFrame = Vector2.zero;
    }

    private Vector2 GetInputDirection()
    {
        Vector2 dir = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) dir.x -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dir.x += 1f;
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) dir.y += 1f;
            if (kb.downArrowKey.isPressed || kb.sKey.isPressed) dir.y -= 1f;
        }

        if (dir.sqrMagnitude < 0.01f && swipeDirection.sqrMagnitude > 0.01f)
        {
            dir = swipeDirection;
        }

        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }

    private void HandleTouchInput()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null || touchscreen.primaryTouch == null)
        {
            swipeDirection = Vector2.zero;
            touchActive = false;
            return;
        }

        var touch = touchscreen.primaryTouch;
        var phase = touch.phase.ReadValue();

        if (phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            touchStartPos = touch.position.ReadValue();
            touchActive = true;
        }
        else if (touchActive && (phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary))
        {
            Vector2 delta = touch.position.ReadValue() - touchStartPos;
            float threshold = 20f; // pixels
            float x = Mathf.Abs(delta.x) > threshold ? Mathf.Sign(delta.x) : 0f;
            float y = Mathf.Abs(delta.y) > threshold ? Mathf.Sign(delta.y) : 0f;
            // Prioritize whichever axis has the larger swipe so movement feels grid-like.
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) y = 0f; else x = 0f;
            swipeDirection = new Vector2(x, y);
        }
        else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            touchActive = false;
            swipeDirection = Vector2.zero;
        }
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

