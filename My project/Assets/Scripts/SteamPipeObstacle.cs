using UnityEngine;

public class SteamPipeObstacle : MonoBehaviour
{
    [Header("Steam Settings")]
    public float steamWidth = 3f;    // set by LevelGenerator to match platform segment width
    public float steamHeight = 1.8f; // tall enough to catch the player walking on the platform
                                     // but not so tall it reaches the floor above

    private GameObject steamCloudObject;

    private void Start()
    {
        // Pipe visual
        transform.localScale = new Vector3(0.4f, 0.8f, 1f);
        GetComponent<SpriteRenderer>().color = Color.white;

        BuildSteamZone();
    }

    // Called by LevelGenerator so the cloud width matches the platform segment
    public void SetWidth(float width)
    {
        steamWidth = width;
        // If Start has already run, rebuild with the new width
        if (steamCloudObject != null)
        {
            Destroy(steamCloudObject);
            BuildSteamZone();
        }
    }

    private void BuildSteamZone()
    {
        steamCloudObject = new GameObject("SteamCloud");
        steamCloudObject.transform.parent = transform;

        // Sit the cloud just above the pipe, centred horizontally on the platform
        // localPosition is relative to the pipe, which is already at platform surface level
        steamCloudObject.transform.localPosition = new Vector3(0f, steamHeight / 2f + 0.4f, 0f);
        steamCloudObject.transform.localRotation = Quaternion.identity;
        steamCloudObject.transform.localScale    = Vector3.one;

        // Visual — flat translucent rectangle across the platform
        SpriteRenderer cloudSr = steamCloudObject.AddComponent<SpriteRenderer>();
        cloudSr.sprite = GetComponent<SpriteRenderer>().sprite;
        cloudSr.color  = new Color(1f, 1f, 1f, 0.25f);

        // Scale the visual to match the trigger box
        // We set localScale here because the cloud is a child with localScale = 1
        steamCloudObject.transform.localScale = new Vector3(steamWidth, steamHeight, 1f);

        // Trigger — flat box, same dimensions, only catches player on this platform level
        BoxCollider2D bc = steamCloudObject.AddComponent<BoxCollider2D>();
        bc.size      = Vector2.one; // localScale handles the actual size
        bc.isTrigger = true;

        steamCloudObject.AddComponent<SteamZone>();
    }
}

// Helper component on the steam cloud child object
public class SteamZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
        {
            PlayerController pc = col.GetComponent<PlayerController>();
            if (pc != null) pc.inSteam = true;
        }
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
        {
            PlayerController pc = col.GetComponent<PlayerController>();
            if (pc != null) pc.inSteam = false;
        }
    }
}