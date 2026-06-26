using UnityEngine;

// Drop one of these as a child object at every point in a maze where
// the player could turn. Check the boxes for whichever directions are
// actually open pipes from this point. The collider should be a trigger
// roughly the size of the intersection.
[RequireComponent(typeof(Collider2D))]
public class JunctionTrigger : MonoBehaviour
{
    [Header("Open exits from this junction")]
    public bool up;
    public bool down;
    public bool left;
    public bool right;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    public bool CanGo(Direction d)
    {
        switch (d)
        {
            case Direction.Up: return up;
            case Direction.Down: return down;
            case Direction.Left: return left;
            case Direction.Right: return right;
            default: return false;
        }
    }

    // Visual aid in the Scene view so you can see at a glance which
    // directions a junction allows while you're building mazes.
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.15f);

        if (up) Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.6f);
        if (down) Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 0.6f);
        if (left) Gizmos.DrawLine(transform.position, transform.position + Vector3.left * 0.6f);
        if (right) Gizmos.DrawLine(transform.position, transform.position + Vector3.right * 0.6f);
    }
}
