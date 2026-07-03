using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MazeExitMarker : MonoBehaviour
{
    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
            LevelManager.Instance.OnPlayerReachedExit();
    }
}