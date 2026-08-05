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
    [Tooltip("Seconds at the very start of a run before the floor begins rising at all - gives the player a calm window to get oriented.")]
    public float riseStartDelaySeconds = 1.5f;
    public float cameraRiseSpeedBase = 0.45f;     // world units/sec the camera's floor rises, once it starts
    public float cameraRiseRampPerSec = 0.01f;    // extra units/sec added, per second survived (gentle ramp = long-term pressure, not early panic)
    [Tooltip("Hard ceiling on rise speed (before rubber-banding) so a skilled player can sustain pace indefinitely instead of the flood eventually outrunning everyone.")]
    public float cameraRiseSpeedMax = 1.5f;
    public float offscreenDeathMargin = 0.6f;     // how far below the camera's bottom edge (world units) the player must fall before it's game over

    [Header("Rubber-Banding (recovery from bad hazard luck)")]
    [Tooltip("Because the floor never moves back down, a wire stun permanently eats into the player's buffer with no way to earn it back - this is what made runs feel 'impossible' after just one or two hits. Rubber-banding fixes that by having the rise SPEED (not position) breathe based on how close the player is to danger, so a bad hit is recoverable instead of a death sentence.")]
    public bool enableRubberBanding = true;
    [Tooltip("How close to the camera's bottom edge counts as 'full danger' for rubber-banding purposes, in world units.")]
    public float rubberBandDangerRange = 3f;
    [Tooltip("Rise speed multiplier applied when the player is in full danger (right at the bottom edge). Lower = more forgiving recovery window.")]
    [Range(0.1f, 1f)] public float rubberBandMinMultiplier = 0.35f;
    [Tooltip("Rise speed multiplier applied when the player has a big comfortable buffer above the bottom edge. Above 1 = pushes skilled players harder so the game doesn't get boring.")]
    [Range(1f, 1.5f)] public float rubberBandMaxMultiplier = 1.15f;
    [Tooltip("How quickly the rubber-band multiplier itself eases toward its target - smooths out the speed changes so they're felt as gradual pressure, not a sudden gear shift.")]
    public float rubberBandSmoothTime = 0.8f;

    [Header("Maze")]
    [Tooltip("Widened from 7 so horizontal runs have real distance to cover - narrow shafts can't support the side-to-side feel no matter how the carve bias is tuned.")]
    public int columns = 9;
    public float cellSize = 1.6f;
    public int rowsPerBand = 6;
    public int bandsAheadBuffer = 3;           // how many bands to keep generated above the flood
    [Range(0f, 1f)] public float steamChance = 0.10f;
    [Range(0f, 1f)] public float wireChance = 0.07f;
    [Range(0f, 1f)] public float fanChance = 0.07f;
    [Tooltip("Range each band's horizontal carve bias is randomly picked from. Wide range = more variety between bands (some very sideways-heavy, some tighter/twistier); narrow range = more consistent feel.")]
    public Vector2 horizontalCarveBiasRange = new Vector2(0.55f, 0.95f);
    [Tooltip("Range a horizontal run's target length is randomly picked from EACH TIME a new run starts (not once per band) - this is what makes the 'turn up' moment unpredictable instead of happening at the same spot every sweep.")]
    public Vector2Int minHorizontalRunCellsRange = new Vector2Int(2, 8);
    [Tooltip("Range a vertical connector's length is randomly picked from each time one starts (replaces a fixed length) - some risers between horizontal sweeps will be a quick 1-cell jog, others a longer multi-cell climb, instead of every connector looking the same.")]
    public Vector2Int verticalRunCellsRange = new Vector2Int(1, 4);
    [Tooltip("Range each BAND's mid-run staircase chance is randomly picked from - this is what varies the overall texture between bands. A band that rolls low reads as clean/uniform sweeps; a band that rolls high is staircase-heavy and chaotic. Widen for more contrast between bands.")]
    public Vector2 midRunStaircaseChanceRange = new Vector2(0.05f, 0.5f);
}