using UnityEngine;

public class ConnectorPiece : MonoBehaviour
{
    [Tooltip("Bottom center — where the player arrives from below")]
    public Transform centerEntry;
    [Tooltip("Top left — where the left maze's entryPoint connects")]
    public Transform leftExit;
    [Tooltip("Top right — where the right maze's entryPoint connects")]
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