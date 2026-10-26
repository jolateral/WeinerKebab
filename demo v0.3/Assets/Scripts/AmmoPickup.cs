using UnityEngine;

// Attach to the Ammo Pickup prefab. Requires a Collider2D (Is Trigger).
public class AmmoPickup : MonoBehaviour
{
    public int ammoAmount = 1;

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            if (player.TryAddAmmo(ammoAmount))
            {
                Destroy(gameObject);
            }
        }
    }
}