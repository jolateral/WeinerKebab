using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("How much faster the player moves compared to the camera rise speed")]
    public float speedMultiplierOverCamera = 3f;
    public Direction startingDirection = Direction.Up;
    [HideInInspector] public Direction currentDirection;

    [Header("Steam Modifier")]
    [HideInInspector] public bool inSteam = false;
    public float steamSpeedMultiplier = 0.35f;

    [Header("Fan Modifier")]
    [HideInInspector] public float fanMultiplier = 1f;

    [Header("Stun (Wire)")]
    public float stunDuration = 0.5f;
    private bool isStunned = false;

    [Header("Wall Safety Check")]
    public bool useWallBlocking = true;
    public LayerMask wallLayerMask;
    public float wallCheckBuffer = 0.05f;

    private Rigidbody2D rb;
    private JunctionTrigger currentJunction;

    // Player speed is always relative to camera so player never races away
    private float BaseSpeed =>
        CameraRiseController.Instance != null
            ? CameraRiseController.Instance.CurrentRiseSpeed * speedMultiplierOverCamera
            : 3f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentDirection = startingDirection;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.isGameOver) return;
        HandleInput();
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance == null || GameManager.Instance.isGameOver) return;
        Move();
    }

    private void HandleInput()
    {
        if (isStunned || currentJunction == null) return;

        Direction? requested = null;
        if (Input.GetKeyDown(KeyCode.W)) requested = Direction.Up;
        else if (Input.GetKeyDown(KeyCode.S)) requested = Direction.Down;
        else if (Input.GetKeyDown(KeyCode.A)) requested = Direction.Left;
        else if (Input.GetKeyDown(KeyCode.D)) requested = Direction.Right;

        if (requested.HasValue && currentJunction.CanGo(requested.Value))
            TurnTo(requested.Value);
    }

    private void TurnTo(Direction newDirection)
    {
        currentDirection = newDirection;
        Vector3 pos = transform.position;
        Vector3 junctionPos = currentJunction.transform.position;
        if (newDirection == Direction.Up || newDirection == Direction.Down)
            pos.x = junctionPos.x;
        else
            pos.y = junctionPos.y;
        transform.position = pos;
    }

    private void Move()
    {
        float speed = BaseSpeed * fanMultiplier;
        if (inSteam) speed *= steamSpeedMultiplier;
        if (isStunned) speed = 0f;
        if (speed <= 0f) return;

        Vector2 dirVector = DirectionToVector(currentDirection);
        float step = speed * Time.fixedDeltaTime;

        if (useWallBlocking)
        {
            RaycastHit2D hit = Physics2D.Raycast(rb.position, dirVector,
                step + wallCheckBuffer, wallLayerMask);
            if (hit.collider != null) return;
        }

        rb.MovePosition(rb.position + dirVector * step);
    }

    public static Vector2 DirectionToVector(Direction d)
    {
        switch (d)
        {
            case Direction.Up:    return Vector2.up;
            case Direction.Down:  return Vector2.down;
            case Direction.Left:  return Vector2.left;
            default:              return Vector2.right;
        }
    }

    public void ApplyZap()
    {
        if (!isStunned) StartCoroutine(StunRoutine());
    }

    private IEnumerator StunRoutine()
    {
        isStunned = true;
        yield return new WaitForSeconds(stunDuration);
        isStunned = false;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.TryGetComponent(out JunctionTrigger jt))
            currentJunction = jt;
        else if (col.CompareTag("Cat"))
            GameManager.Instance.GameOver();
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.TryGetComponent(out JunctionTrigger jt) && currentJunction == jt)
            currentJunction = null;
    }
}