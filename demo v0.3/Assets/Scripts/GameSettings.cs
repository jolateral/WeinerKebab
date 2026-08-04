using UnityEngine;

// Create via: Assets > Create > VentCrawler > Game Settings
// Drag the resulting asset into every script below that has a "settings" field.
[CreateAssetMenu(fileName = "GameSettings", menuName = "VentCrawler/Game Settings")]
public class GameSettings : ScriptableObject
{
    [Header("Player")]
    public float playerSpeed = 3.2f;          // world units/sec
    public float playerAccel = 12f;            // how snappily velocity reaches target

    [Header("Wire Hazard")]
    public float stunDurationSeconds = 0.5f;   // adjustable per your original request

    [Header("Steam Hazard")]
    [Range(0.1f, 1f)] public float steamSpeedMultiplier = 0.45f; // % of normal speed while inside steam

    [Header("Fan Hazard")]
    public float fanForce = 6f;                // acceleration applied while inside a fan zone

    [Header("Camera Rise & Death")]
    public float cameraRiseSpeedBase = 1.4f;      // world units/sec the camera's floor rises
    public float cameraRiseRampPerSec = 0.03f;    // extra units/sec added, per second survived
    public float offscreenDeathMargin = 0.3f;     // how far below the camera's bottom edge (world units) the player must fall before it's game over

    [Header("Maze")]
    public int columns = 7;
    public float cellSize = 1.6f;
    public int rowsPerBand = 8;
    public int bandsAheadBuffer = 3;           // how many bands to keep generated above the flood
    [Range(0f, 1f)] public float steamChance = 0.10f;
    [Range(0f, 1f)] public float wireChance = 0.07f;
    [Range(0f, 1f)] public float fanChance = 0.07f;
}
