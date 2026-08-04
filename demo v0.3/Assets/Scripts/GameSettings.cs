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
    [Tooltip("Grace period after a stun ends during which the flood cannot kill the player. Prevents a wire hit from chaining straight into a death with no recovery window.")]
    public float postStunDeathGraceSeconds = 0.75f;

    [Header("Steam Hazard")]
    [Range(0.1f, 1f)] public float steamSpeedMultiplier = 0.45f; // % of normal speed while inside steam

    [Header("Fan Hazard")]
    public float fanForce = 6f;                // acceleration applied while inside a fan zone

    [Header("Camera Rise & Death")]
    public float cameraRiseSpeedBase = 1.4f;      // world units/sec the camera's floor rises
    public float cameraRiseRampPerSec = 0.03f;    // extra units/sec added, per second survived
    [Tooltip("Hard ceiling on rise speed so a skilled player can sustain pace indefinitely instead of the flood eventually outrunning everyone.")]
    public float cameraRiseSpeedMax = 2.6f;
    public float offscreenDeathMargin = 0.6f;     // how far below the camera's bottom edge (world units) the player must fall before it's game over
    [Tooltip("Extra vertical padding granted while the player is moving mostly sideways (i.e. actively navigating a turn) rather than idling.")]
    public float turnPaddingBonus = 1.0f;

    [Header("Maze")]
    public int columns = 7;
    public float cellSize = 1.6f;
    public int rowsPerBand = 6;
    public int bandsAheadBuffer = 3;           // how many bands to keep generated above the flood
    [Range(0f, 1f)] public float steamChance = 0.10f;
    [Range(0f, 1f)] public float wireChance = 0.07f;
    [Range(0f, 1f)] public float fanChance = 0.07f;
    [Tooltip("Probability (0-1) of picking the upward-opening neighbor when it's available during maze carving, biasing corridors to trend vertical instead of wandering sideways.")]
    [Range(0f, 1f)] public float upwardCarveBias = 0.55f;
}