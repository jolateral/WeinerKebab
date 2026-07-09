using UnityEngine;

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
        if (sr != null)
        {
            sr.color = new Color(0.85f, 0.6f, 1f, 0.45f);
            sr.sortingOrder = 5; // must draw above pipes (order 0)
        }
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