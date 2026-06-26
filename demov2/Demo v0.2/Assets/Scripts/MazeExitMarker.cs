using UnityEngine;

// Place on a GameObject with a trigger Collider2D at the very end of a
// maze's path (the same spot as MazeSegment.exitPoint). When the player
// reaches it, the next maze is spawned and the score goes up by one.
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
    Debug.Log("MazeExitMarker hit by: " + col.name + " tag: " + col.tag);
    if (col.CompareTag("Player"))
    {
        Debug.Log("Player reached exit! Calling LevelManager.");
        LevelManager.Instance.OnPlayerReachedExit();
    }
}
}
