using UnityEngine;

// Tiled background that scrolls slower than the maze, following only VERTICAL camera movement.
// Horizontally it stays locked to the camera (no sideways parallax).
//
// Setup:
//  1. Create an empty GameObject (e.g. "ParallaxBackground") at the scene root, NOT a child of the camera.
//  2. Add this script (it adds a SpriteRenderer automatically).
//  3. Assign your tile sprite to the SpriteRenderer's Sprite slot.
//  4. Sprite import settings: Sprite Mode = Single, Mesh Type = Full Rect, Compression = None.
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(1000)] // run after CameraFollow has moved the camera this frame, so there's no 1-frame lag/jitter
public class ParallaxBackground : MonoBehaviour
{
    [Tooltip("Leave empty to use Camera.main.")]
    public Camera targetCamera;

    [Tooltip("0 = pinned to the screen (looks infinitely far away). 1 = moves exactly with the maze. 0.3 - 0.5 usually feels good.")]
    [Range(0f, 1f)] public float verticalParallax = 0.4f;

    [Tooltip("Must be lower than the walls and floor so the background draws behind everything.")]
    public int sortingOrder = -100;

    private SpriteRenderer sr;
    private Vector2 lastSize;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.sortingOrder = sortingOrder;
        if (targetCamera == null) targetCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null || sr.sprite == null || !targetCamera.orthographic) return;

        // World size of ONE tile, taken from the sprite itself (pixels / PPU).
        Vector2 tile = sr.sprite.bounds.size;
        if (tile.x <= 0.0001f || tile.y <= 0.0001f) return;

        // Size the tiled area to cover everything the camera can currently see, in whole tiles.
        // This re-checks every frame because the camera zooms out over wide bands.
        float viewH = targetCamera.orthographicSize * 2f;
        float viewW = viewH * targetCamera.aspect;
        Vector2 needed = new Vector2(
            Mathf.Ceil((viewW + 2f * tile.x) / tile.x) * tile.x,
            Mathf.Ceil((viewH + 4f * tile.y) / tile.y) * tile.y);

        // Only touch .size when it actually changes (changing it rebuilds the mesh).
        if ((needed - lastSize).sqrMagnitude > 0.0001f)
        {
            sr.size = needed;
            lastSize = needed;
        }

        Vector3 camPos = targetCamera.transform.position;

        // Step the object in whole-tile jumps to stay under the camera (invisible, because the pattern repeats),
        // then add a smooth offset worth (camY * parallax) wrapped to one tile. That offset is the parallax.
        float snappedY = Mathf.Round(camPos.y / tile.y) * tile.y;
        float phase = Mathf.Repeat(camPos.y * verticalParallax, tile.y);

        transform.position = new Vector3(camPos.x, snappedY + phase, transform.position.z);
    }
}