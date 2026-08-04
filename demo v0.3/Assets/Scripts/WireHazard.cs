using UnityEngine;

// Attach to the Wire prefab. Requires a Collider2D with "Is Trigger" checked.
public class WireHazard : MonoBehaviour
{
    private bool hasTriggeredThisContact = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null && !hasTriggeredThisContact)
        {
            player.ApplyStun();
            hasTriggeredThisContact = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            hasTriggeredThisContact = false; // allow re-trigger on a fresh contact later
        }
    }
}
