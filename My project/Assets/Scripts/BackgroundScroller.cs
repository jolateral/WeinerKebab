using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{
    public float tileHeight = 20f; // height of one tile of background
    private Transform cam;

    private void Start()
    {
        cam = Camera.main.transform;
    }

    private void Update()
    {
        // Snap background to stay behind camera
        float targetY = Mathf.Round(cam.position.y / tileHeight) * tileHeight;
        transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
    }
}