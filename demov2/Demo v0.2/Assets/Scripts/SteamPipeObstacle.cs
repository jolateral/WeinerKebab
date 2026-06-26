using UnityEngine;

// Simplified compared to your old version -- no runtime-generated child
// object needed. Just resize this GameObject's BoxCollider2D (and its
// sprite, if you add one) directly in the Editor to cover the steam area.
// Slowdown amount itself lives on PlayerController.steamSpeedMultiplier
// so it's tunable in one place even if you place many steam zones.
[RequireComponent(typeof(Collider2D))]
public class SteamPipeObstacle : MonoBehaviour
{
    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(0.85f, 0.6f, 1f, 0.45f); // placeholder translucent purple
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!col.CompareTag("Player")) return;
        PlayerController pc = col.GetComponent<PlayerController>();
        if (pc != null) pc.inSteam = true;
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (!col.CompareTag("Player")) return;
        PlayerController pc = col.GetComponent<PlayerController>();
        if (pc != null) pc.inSteam = false;
    }
}
