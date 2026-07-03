using UnityEngine;

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
            case Direction.Up:    return up;
            case Direction.Down:  return down;
            case Direction.Left:  return left;
            case Direction.Right: return right;
            default:              return false;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
        if (up)    Gizmos.DrawLine(transform.position, transform.position + Vector3.up    * 0.6f);
        if (down)  Gizmos.DrawLine(transform.position, transform.position + Vector3.down  * 0.6f);
        if (left)  Gizmos.DrawLine(transform.position, transform.position + Vector3.left  * 0.6f);
        if (right) Gizmos.DrawLine(transform.position, transform.position + Vector3.right * 0.6f);
    }
}