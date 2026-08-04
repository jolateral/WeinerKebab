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
