using UnityEngine;

// Resize the BoxCollider2D (trigger) on this object to represent the
// visible wind stream -- same idea as your concept art's light-blue
// rectangle. blowDirection is set per-instance in the Inspector since
// mazes are hand-built rather than procedurally generated.
[RequireComponent(typeof(Collider2D))]
public class FanObstacle : MonoBehaviour
{
    [Header("Fan Settings")]
    public Direction blowDirection = Direction.Right;

    [Header("Speed Modifiers")]
    [Tooltip("Multiplier applied when the player walks INTO the wind")]
    public float resistanceMultiplier = 0.45f;
    [Tooltip("Multiplier applied when the player walks WITH the wind")]
    public float accelerationMultiplier = 1.9f;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(0.4f, 0.85f, 1f, 0.6f); // placeholder light blue
            sr.sortingOrder = 5; // must draw above pipes (order 0)
        }
    }

    private void OnTriggerEnter2D(Collider2D col) => ApplyOrClear(col, true);
    private void OnTriggerStay2D(Collider2D col) => ApplyOrClear(col, true);
    private void OnTriggerExit2D(Collider2D col) => ApplyOrClear(col, false);

    private void ApplyOrClear(Collider2D col, bool inZone)
    {
        if (!col.CompareTag("Player")) return;
        PlayerController pc = col.GetComponent<PlayerController>();
        if (pc == null) return;

        if (!inZone)
        {
            pc.fanMultiplier = 1f;
            return;
        }

        if (pc.currentDirection == blowDirection)
            pc.fanMultiplier = accelerationMultiplier;       // moving with the wind
        else if (pc.currentDirection == Opposite(blowDirection))
            pc.fanMultiplier = resistanceMultiplier;          // moving into the wind
        else
            pc.fanMultiplier = 1f;                            // crossing the stream sideways
    }

    private static Direction Opposite(Direction d)
    {
        switch (d)
        {
            case Direction.Up: return Direction.Down;
            case Direction.Down: return Direction.Up;
            case Direction.Left: return Direction.Right;
            default: return Direction.Left;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.8f);
        Vector3 dir = (Vector3)(Vector2)PlayerController.DirectionToVector(blowDirection);
        Gizmos.DrawLine(transform.position, transform.position + dir * 2f);
    }
}