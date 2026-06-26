using UnityEngine;

// Attach to the ConnectorPiece prefab root.
// centerEntry = where the player arrives from below (bottom center)
// leftExit    = where the left maze's entry point should connect
// rightExit   = where the right maze's entry point should connect
public class ConnectorPiece : MonoBehaviour
{
    public Transform centerEntry;
    public Transform leftExit;
    public Transform rightExit;

    private void OnDrawGizmos()
    {
        if (centerEntry != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(centerEntry.position, 0.2f);
        }
        if (leftExit != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(leftExit.position, 0.2f);
        }
        if (rightExit != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(rightExit.position, 0.2f);
        }
    }
}