using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "VentCrawler/Game Settings")]
public class GameSettings : ScriptableObject
{
    [Header("Player")]
    public float playerSpeed = 3.2f;          // world units/sec
    public float playerAccel = 12f;            // how snappily velocity reaches target
    [Tooltip("Multiplies playerSpeed while the player is moving straight up.")]
    public float verticalSpeedMultiplier = 1.5f;

    [Header("Gun & Ammo")]
    public int maxAmmo = 5;
    public int startingAmmo = 0;
    public float catSpawnMinHeightMeters = 50f; // Cats start spawning at 50m
    [Range(0f, 1f)] public float ammoSpawnChance = 0.25f;
    [Range(0f, 1f)] public float catSpawnChance = 0.20f;

    [Header("Wire Hazard")]
    public float stunDurationSeconds = 0.5f;   
    public float postStunDeathGraceSeconds = 0.75f;
    public float wireShakeDuration = 0.35f;
    public float wireShakeMagnitude = 0.22f;

    [Header("Steam Hazard")]
    [Range(0.1f, 1f)] public float steamSpeedMultiplier = 0.45f; 

    [Header("Fan Hazard")]
    public float fanForce = 2.2f;
    public Vector2Int fanTunnelLengthCellsRange = new Vector2Int(1, 3);

    [Header("Camera Rise & Death")]
    public float riseStartDelaySeconds = 1.5f;
    public float cameraRiseSpeedBase = 0.45f;     
    public float cameraRiseRampPerSec = 0.01f;    
    public float cameraRiseSpeedMax = 1.5f;
    public float offscreenDeathMargin = 0.6f;     

    [Header("Camera Zoom / Framing")]
    public float cameraZoomOutMultiplier = 1.8f;
    public float cameraWidthSmoothTime = 0.5f;

    [Header("Rubber-Banding")]
    public bool enableRubberBanding = true;
    public float rubberBandDangerRange = 5f;
    [Range(0.1f, 1f)] public float rubberBandMinMultiplier = 0.35f;
    [Range(1f, 1.5f)] public float rubberBandMaxMultiplier = 1.15f;
    public float rubberBandSmoothTime = 0.35f;

    [Header("Maze")]
    public int columns = 9;
    public float cellSize = 1.6f;
    public int rowsPerBand = 6;
    public int bandsAheadBuffer = 3;           
    [Range(0f, 1f)] public float steamChance = 0.10f;
    [Range(0f, 1f)] public float wireChance = 0.07f;
    [Range(0f, 1f)] public float fanChance = 0.07f;
    public Vector2 horizontalCarveBiasRange = new Vector2(0.55f, 0.95f);
    public Vector2Int minHorizontalRunCellsRange = new Vector2Int(2, 8);
    public Vector2Int verticalRunCellsRange = new Vector2Int(1, 4);
    public Vector2 midRunStaircaseChanceRange = new Vector2(0.05f, 0.5f);

    [Header("Hazard Bypasses")]
    [Range(0f, 1f)] public float hazardBypassChance = 0.45f;
    public Vector2Int bypassDetourExtensionRange = new Vector2Int(0, 3);

    [Header("Wide Reveal / Choice Bands")]
    public bool enableWideRevealBands = true;
    [Range(0f, 1f)] public float wideBandChance = 0.12f;
    public float wideBandColumnMultiplier = 2.2f;
    public int minBandsBetweenWideBands = 3;
    [Range(0f, 1f)] public float wideBandHazardChanceMultiplier = 0.5f;
    public Vector2Int wideBandHorizontalRunCellsRange = new Vector2Int(1, 3);
    public Vector2Int wideBandVerticalRunCellsRange = new Vector2Int(1, 2);
    [Range(0.1f, 1f)] public float wideBandRiseSpeedMultiplier = 0.5f;
    [Range(0f, 0.85f)] public float wideBandResumeDirectionChance = 0.4f;
    [Range(0f, 0.7f)] public float wideBandBlockedAreaFraction = 0.4f;
    public Vector2Int wideBandBlockClusterSizeRange = new Vector2Int(2, 4);

    [Header("Partner Quest (wide bands)")]
    [Tooltip("If true, every wide band hides the OTHER character somewhere inside it, and the exit stays gated until the player touches them.")]
    public bool requirePartnerInWideBands = true;
    [Tooltip("Camera rise-speed multiplier while the player is inside a wide band, from entering it until they climb out of its exit (finding the partner doesn't end it). Very small = the flood only creeps, but still gives urgency.")]
    [Range(0f, 1f)] public float partnerQuestRiseSpeedMultiplier = 0.12f;
    [Tooltip("From entering a wide band until climbing out of its exit, the camera can NEVER kill the player. If the player gets closer than this (world units) to the bottom screen edge, the rise pauses completely so they can't be pushed off-screen.")]
    public float partnerQuestMinBufferAboveEdge = 1.5f;

    [Header("Background Rooms")]
    [Range(0f, 1f)] public float roomSpawnChance = 0.35f;
    public int minBandsBetweenRooms = 2;
    public float roomSideOffset = 0.5f;

    [Header("Milestones")]
    public float milestoneIntervalMeters = 100f;
}