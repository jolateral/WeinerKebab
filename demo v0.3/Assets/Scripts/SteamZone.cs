using UnityEngine;

// Attach to the Steam prefab. Requires a Collider2D with "Is Trigger" checked.
public class SteamZone : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null) player.SetInSteam(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null) player.SetInSteam(false);
    }
}
