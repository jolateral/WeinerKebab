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