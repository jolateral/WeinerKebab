using UnityEngine;

// Create via: Assets > Create > VentCrawler > Game Settings
// Drag the resulting asset into every script below that has a "settings" field.
[CreateAssetMenu(fileName = "GameSettings", menuName = "VentCrawler/Game Settings")]
public class GameSettings : ScriptableObject
{
    [Header("Player")]
    public float playerSpeed = 3.2f;          // world units/sec
    public float playerAccel = 12f;            // how snappily velocity reaches target
    [Tooltip("Multiplies playerSpeed while the player is moving straight up. Since most obstacles live on turns/horizontal stretches, this makes vertical climbing feel fast and automatic, and turning to dodge something feel like a deliberate slow-down by comparison.")]
    public float verticalSpeedMultiplier = 1.5f;

    [Header("Wire Hazard")]
    public float stunDurationSeconds = 0.5f;   // adjustable per your original request
    [Tooltip("Grace period after a stun ends during which the flood cannot kill the player. Prevents a wire hit from chaining straight into a death with no recovery window.")]
    public float postStunDeathGraceSeconds = 0.75f;
    [Tooltip("Duration in seconds of the camera's electrocution-style shake when the player hits a wire.")]
    public float wireShakeDuration = 0.35f;
    [Tooltip("Max screen-shake offset in world units when a wire is hit. Keep small (0.1-0.3) so it reads as a jolt, not seasickness.")]
    public float wireShakeMagnitude = 0.22f;

    [Header("Steam Hazard")]
    [Range(0.1f, 1f)] public float steamSpeedMultiplier = 0.45f; // % of normal speed while inside steam

    [Header("Fan Hazard")]
    [Tooltip("Acceleration applied while inside a fan zone. Fans now only ever blow along the vent's own axis (helping or hindering, picked randomly) rather than sideways into a wall - keep this BELOW playerSpeed so an opposing fan slows the player hard but can never fully cancel their forward speed and trap them. (playerSpeed - fanForce) is the player's worst-case net speed while fighting a fan head-on; keep that comfortably positive.")]
    public float fanForce = 2.2f;
    [Tooltip("How many consecutive cells long a fan's wind tunnel stretches along the corridor, picked randomly per fan within this (min, max) range and clamped to however much straight corridor is actually available on either side. (1,1) = the old single-cell-only behavior. Try (1,3) for the occasional longer 'blowing down the hallway' stretch.")]
    public Vector2Int fanTunnelLengthCellsRange = new Vector2Int(1, 3);

    [Header("Camera Rise & Death")]
    [Tooltip("Seconds at the very start of a run before the floor begins rising at all - gives the player a calm window to get oriented.")]
    public float riseStartDelaySeconds = 1.5f;
    public float cameraRiseSpeedBase = 0.45f;     // world units/sec the camera's floor rises, once it starts
    public float cameraRiseRampPerSec = 0.01f;    // extra units/sec added, per second survived (gentle ramp = long-term pressure, not early panic)
    [Tooltip("Hard ceiling on rise speed (before rubber-banding) so a skilled player can sustain pace indefinitely instead of the flood eventually outrunning everyone.")]
    public float cameraRiseSpeedMax = 1.5f;
    public float offscreenDeathMargin = 0.6f;     // how far below the camera's bottom edge (world units) the player must fall before it's game over

    [Header("Camera Zoom / Framing")]
    [Tooltip("Multiplies the camera's exact fit-to-maze-width size so the frame sits pulled back further than a tight fit, matching the wider/more zoomed-out comic panel framing. 1 = exact fit (old behavior). Try 1.6-2.0 to match the comic reference.")]
    public float cameraZoomOutMultiplier = 1.8f;
    [Tooltip("How quickly the camera's width-driven zoom eases toward its target as the maze ahead gets narrower/wider (e.g. approaching or leaving a wide reveal band). Bigger = lazier/slower zoom changes. This now actually does something: the camera continuously re-fits to whatever band width the player is near, so wide reveal bands really do pull the camera back.")]
    public float cameraWidthSmoothTime = 0.5f;

    [Header("Rubber-Banding (recovery from bad hazard luck)")]
    [Tooltip("Because the floor never moves back down, a wire stun permanently eats into the player's buffer with no way to earn it back - this is what made runs feel 'impossible' after just one or two hits. Rubber-banding fixes that by having the rise SPEED (not position) breathe based on how close the player is to danger, so a bad hit is recoverable instead of a death sentence.")]
    public bool enableRubberBanding = true;
    [Tooltip("How close to the camera's bottom edge counts as 'full danger' for rubber-banding purposes, in world units.")]
    public float rubberBandDangerRange = 5f;
    [Tooltip("Rise speed multiplier applied when the player is in full danger (right at the bottom edge). Lower = more forgiving recovery window.")]
    [Range(0.1f, 1f)] public float rubberBandMinMultiplier = 0.35f;
    [Tooltip("Rise speed multiplier applied when the player has a big comfortable buffer above the bottom edge. Above 1 = pushes skilled players harder so the game doesn't get boring.")]
    [Range(1f, 1.5f)] public float rubberBandMaxMultiplier = 1.15f;
    [Tooltip("How quickly the rubber-band multiplier itself eases toward its target - smooths out the speed changes so they're felt as gradual pressure, not a sudden gear shift.")]
    public float rubberBandSmoothTime = 0.35f;

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

    [Header("Hazard Bypasses (lets the player route around SOME obstacles)")]
    [Tooltip("The maze stays a single corridor everywhere (outside of choice bands), same as before. This is the chance, PER HAZARD PLACED, that a small local detour loop gets carved just around that one hazard - a little 'step around' bubble the player can duck into instead of walking through it. Lower = mostly forced hits like before with occasional dodges; higher = most hazards are avoidable. 0 = old behavior (always forced through).")]
    [Range(0f, 1f)] public float hazardBypassChance = 0.45f;
    [Tooltip("Extra cells the bypass loop can stretch on EACH side of the hazard, beyond the base 3-cell bubble, when the corridor is straight enough nearby to support it. A value is picked per-bypass in this (min, max) range, clamped to however much straight corridor is actually available near that hazard. (0,0) = always the original fixed-length bubble. Try (0,3) for a mix of short dodges and longer sweeping branches.")]
    public Vector2Int bypassDetourExtensionRange = new Vector2Int(0, 3);

    [Header("Wide Reveal / Choice Bands (comic-panel wide shots with real route choice)")]
    [Tooltip("Master switch for the wide-band feature. When ON, occasional bands are much wider AND keep their full branching maze uncollapsed, so the player gets a real 'which way do I go' moment instead of a single forced corridor. The camera pulls back to reveal the whole thing (see cameraWidthSmoothTime). Leave OFF for the old guaranteed-uniform-width behavior.")]
    public bool enableWideRevealBands = false;
    [Tooltip("Chance, each time a new band is generated, that it becomes a wide choice band. Has NO effect while enableWideRevealBands (above) is off.")]
    [Range(0f, 1f)] public float wideBandChance = 0.12f;
    [Tooltip("How much wider a wide band's column count is versus the normal maze width (multiplier on 'columns').")]
    public float wideBandColumnMultiplier = 2.2f;
    [Tooltip("Minimum number of normal bands required between two wide bands, so wide reveals stay special/rare instead of clustering together.")]
    public int minBandsBetweenWideBands = 3;
    [Tooltip("Hazard chances (steamChance/wireChance) are multiplied by this inside choice bands, since a dense branching maze full of hazards gets unfair/unreadable fast. Fans are never placed inside choice bands (their push direction logic assumes a single linear corridor).")]
    [Range(0f, 1f)] public float wideBandHazardChanceMultiplier = 0.5f;
    [Tooltip("Choice bands use THEIR OWN run-length ranges instead of minHorizontalRunCellsRange, kept short on purpose. Reusing the normal (narrow-band-tuned) ranges at 2x+ width produces long uninterrupted horizontal bars instead of a grid of individual shafts - shrinking this keeps corridor segments the same visual size as normal bands, just repeated across more of them.")]
    public Vector2Int wideBandHorizontalRunCellsRange = new Vector2Int(1, 3);
    [Tooltip("Choice-band equivalent of verticalRunCellsRange - kept short for the same reason as wideBandHorizontalRunCellsRange.")]
    public Vector2Int wideBandVerticalRunCellsRange = new Vector2Int(1, 2);
    [Tooltip("Extra flood-rise slowdown while the player is anywhere inside a wide choice band's row range (multiplies on top of everything else, including rubber-banding). Wide bands have real branching routes to explore, so one wrong turn shouldn't be an instant death sentence the way it can be in a normal single-corridor band - this buys back time to backtrack and find the right path. 1 = no extra slowdown (old behavior). Try 0.4-0.6 for a generous exploration window.")]
    [Range(0.1f, 1f)] public float wideBandRiseSpeedMultiplier = 0.5f;
    [Tooltip("Chance the carve keeps going in the same horizontal direction it was already going, inside choice bands only (normal bands use a fixed 0.85). Lower = many more turns = finer-grained, more 'individual shaft' look. Raise toward 0.85 if choice bands still feel too open.")]
    [Range(0f, 0.85f)] public float wideBandResumeDirectionChance = 0.4f;
    [Tooltip("Fraction of a choice band's interior area (excluding the entry/exit rows) that gets blocked out into solid rock/machinery clusters BEFORE carving, so the maze winds around real gaps instead of filling the entire rectangle edge-to-edge (which is what a recursive-backtracker maze always does if you let it fill the whole grid). Raise this if choice bands still look like one solid block; lower it if they start looking too empty/sparse.")]
    [Range(0f, 0.7f)] public float wideBandBlockedAreaFraction = 0.4f;
    [Tooltip("Width/height (in cells) of each blocked rock/machinery cluster in a choice band, picked randomly per cluster within this range. Bigger = fewer, chunkier obstacles; smaller = more, finer-grained ones.")]
    public Vector2Int wideBandBlockClusterSizeRange = new Vector2Int(2, 4);

    [Header("Background Rooms (visual flavor in the dead space beside the vent)")]
    [Tooltip("Chance, each time a new band is generated, that a decorative room image gets placed off to one side of the maze. Purely visual - it sits outside the maze's own columns, in the space the pulled-back camera already reveals, so the player can never reach or touch it.")]
    [Range(0f, 1f)] public float roomSpawnChance = 0.35f;
    [Tooltip("Minimum number of bands required between two spawned rooms, so they read as an occasional flourish instead of clustering together.")]
    public int minBandsBetweenRooms = 2;
    [Tooltip("Extra world-unit gap between the maze's outer wall and the room image, so it doesn't visually touch the vent wall.")]
    public float roomSideOffset = 0.5f;

    [Header("Milestones")]
    [Tooltip("How often (in meters of height climbed) the player gets a positive milestone flash + sound cue.")]
    public float milestoneIntervalMeters = 100f;
}