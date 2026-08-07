using UnityEngine;

// Attach to the Fan prefab. Requires a Collider2D with "Is Trigger" checked.
// MazeGenerator calls SetDirection() right after instantiating this prefab.
public class FanZone : MonoBehaviour
{
    public FanDirection direction = FanDirection.North;

    [Tooltip("Optional: rotate a child arrow sprite to visually match the direction.")]
    public Transform arrowVisual;

    public void SetDirection(FanDirection dir)
    {
        direction = dir;
        if (arrowVisual != null)
        {
            float z = dir switch
            {
                FanDirection.North => 90f,
                FanDirection.South => -90f,
                FanDirection.East => 0f,
                FanDirection.West => 180f,
                _ => 0f
            };
            arrowVisual.localRotation = Quaternion.Euler(0, 0, z);
        }
    }

    // Stretches this fan's trigger collider along the corridor so it acts like wind blowing down
    // a hallway instead of a single-cell push. Requires a BoxCollider2D on this prefab. Called by
    // MazeGenerator right after instantiation, using however many straight cells it confirmed are
    // safe to span (see fanSpanCells on the source MazeCell / fanTunnelLengthCellsRange in
    // GameSettings). Grows the collider symmetrically from the fan's own center, since the fan
    // sits in the middle cell of the tunnel it's extending into on both sides.
    public void SetSpan(int spanCells, float cellSize)
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null || spanCells <= 1) return;

        float lengthWorldUnits = spanCells * cellSize;
        bool axisIsHorizontal = direction == FanDirection.East || direction == FanDirection.West;

        Vector2 size = col.size;
        if (axisIsHorizontal) size.x = lengthWorldUnits;
        else size.y = lengthWorldUnits;
        col.size = size;
    }

    private Vector2 DirectionVector()
    {
        return direction switch
        {
            FanDirection.North => Vector2.up,
            FanDirection.South => Vector2.down,
            FanDirection.East => Vector2.right,
            FanDirection.West => Vector2.left,
            _ => Vector2.zero
        };
    }

    void OnTriggerStay2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null) player.ApplyFanForce(DirectionVector());
    }
}