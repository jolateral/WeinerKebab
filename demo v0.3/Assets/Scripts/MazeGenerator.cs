using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    [Header("Config")]
    public GameSettings settings;

    [Header("Prefabs (assign in Inspector)")]
    public GameObject wallSegmentPrefab;
    public GameObject steamPrefab;      
    public GameObject wirePrefab;       
    public GameObject fanPrefab;        
    public GameObject floorTilePrefab;  
    public GameObject ammoPickupPrefab; 
    public GameObject enemyCatPrefab;   

    public GameObject[] roomPrefabs;

    [Header("Partner Quest (wide bands)")]
    [Tooltip("Prefab with a SpriteRenderer, a trigger Collider2D and the PartnerCharacter script. One is hidden in every wide band.")]
    public GameObject partnerPrefab;
    [Tooltip("Optional. Blocks the wide band's exit until the partner is found. If empty, a tinted copy of the wall segment prefab is used.")]
    public GameObject gatePrefab;
    public Color fallbackGateTint = new Color(1f, 0.35f, 0.35f, 1f);

    private class PartnerQuest
    {
        public int startRow;
        public int endRow;
        public bool resolved;   // partner has been found, gate is open
        public bool completed;  // player has actually climbed out through the exit - protections end here
        public GameObject gate;
    }
    private List<PartnerQuest> partnerQuests = new List<PartnerQuest>();

    private List<MazeCell[]> rows = new List<MazeCell[]>();
    private int generatedUpToRow = -1;
    private float lastExitWorldX;
    private int bandsSinceWide = 999; 
    private int bandsSinceRoom = 999; 

    private struct BandLayout { public int columns; public float xOffset; }
    private Dictionary<int, BandLayout> rowLayout = new Dictionary<int, BandLayout>();
    private Dictionary<int, List<GameObject>> spawnedByRow = new Dictionary<int, List<GameObject>>();

    public float CellSize => settings.cellSize;
    public int Columns => settings.columns; 
    public float BandCenterX => settings.columns * settings.cellSize * 0.5f;

    void Awake()
    {
        lastExitWorldX = BandCenterX; 
        lastExitWorldX = GenerateBand(0, settings.rowsPerBand, lastExitWorldX, isFirstBand: true);
        for (int i = 0; i < settings.bandsAheadBuffer; i++)
        {
            lastExitWorldX = GenerateBand(generatedUpToRow + 1, settings.rowsPerBand, lastExitWorldX);
        }
    }

    public void EnsureGeneratedAhead(float floodWorldY)
    {
        int floodRow = Mathf.FloorToInt(floodWorldY / CellSize);
        int neededRow = floodRow + settings.rowsPerBand * settings.bandsAheadBuffer;

        // Generation and cleanup are tied to what the CAMERA can actually see, not just to the flood.
        // The camera can be zoomed way out (especially over wide bands), so a flood-only rule could
        // spawn things inside the visible frame, or delete things the player can still see.
        int cleanupBelowRow = floodRow - settings.rowsPerBand * 2;
        Camera viewCam = Camera.main;
        if (viewCam != null)
        {
            float viewBottomY = viewCam.transform.position.y - viewCam.orthographicSize;
            float viewTopY = viewCam.transform.position.y + viewCam.orthographicSize;
            int topRow = Mathf.FloorToInt(viewTopY / CellSize);
            int bottomRow = Mathf.FloorToInt(viewBottomY / CellSize);
            neededRow = Mathf.Max(neededRow, topRow + settings.rowsPerBand);              // always stay a full band above the top edge
            cleanupBelowRow = Mathf.Min(cleanupBelowRow, bottomRow - settings.rowsPerBand); // never clean up within a band of the bottom edge
        }

        while (generatedUpToRow < neededRow)
        {
            lastExitWorldX = GenerateBand(generatedUpToRow + 1, settings.rowsPerBand, lastExitWorldX);
        }

        List<int> toRemove = new List<int>();
        foreach (var kvp in spawnedByRow)
        {
            if (kvp.Key < cleanupBelowRow)
            {
                // Cats WALK, so one registered under a low row may have wandered up into view.
                // They despawn themselves (see EnemyCat) once they're safely below the camera.
                foreach (var go in kvp.Value)
                {
                    if (go != null && go.GetComponent<EnemyCat>() == null) Destroy(go);
                }
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var k in toRemove) { spawnedByRow.Remove(k); rowLayout.Remove(k); }

        partnerQuests.RemoveAll(q => q.endRow < cleanupBelowRow);
    }

    // True while the given world Y is inside a wide band the player hasn't finished yet. That means
    // BOTH before the partner is found AND after, right up until the player climbs out of the exit.
    public bool IsPartnerQuestActiveAtWorldY(float worldY)
    {
        int row = Mathf.FloorToInt(worldY / CellSize);
        foreach (var q in partnerQuests)
        {
            if (!q.completed && row >= q.startRow && row <= q.endRow) return true;
        }
        return false;
    }

    // Called every frame by CameraFollow. Once the partner has been found and the player has climbed
    // past the top of the band (through the opened gate), that quest is finished for good and the
    // camera goes back to normal behavior.
    public void UpdatePartnerQuestProgress(float playerWorldY)
    {
        int row = Mathf.FloorToInt(playerWorldY / CellSize);
        foreach (var q in partnerQuests)
        {
            if (q.resolved && !q.completed && row > q.endRow) q.completed = true;
        }
    }

    // Called by PartnerCharacter when the player touches them: marks the quest done and opens the gate.
    public void ResolvePartnerQuest(int questId)
    {
        foreach (var q in partnerQuests)
        {
            if (q.startRow != questId) continue;
            q.resolved = true;
            if (q.gate != null) Destroy(q.gate);
            q.gate = null;
        }
    }

    // Lets other scripts hand an object to the maze so it gets cleaned up with the row it sits on.
    public void RegisterSpawned(Vector3 worldPos, GameObject go)
    {
        int row = Mathf.Max(0, Mathf.FloorToInt(worldPos.y / CellSize));
        if (!spawnedByRow.ContainsKey(row)) spawnedByRow[row] = new List<GameObject>();
        spawnedByRow[row].Add(go);
    }

    public Vector3 CellWorldCenter(int row, int col)
    {
        float x = col * CellSize + CellSize * 0.5f;
        if (rowLayout.TryGetValue(row, out var layout))
        {
            x = layout.xOffset + col * CellSize + CellSize * 0.5f;
        }
        return new Vector3(x, row * CellSize + CellSize * 0.5f, 0f);
    }

    public MazeCell CellAt(int row, int col)
    {
        if (row < 0 || row >= rows.Count) return null;
        var rowArr = rows[row];
        if (rowArr == null) return null;
        if (col < 0 || col >= rowArr.Length) return null;
        return rowArr[col];
    }

    // Returns the actual layout (columns + x offset) for a given world row, falling back to the
    // default band width if that row hasn't been generated / recorded yet. Bands can vary in width
    // (wide "choice" bands), so this must be looked up per-row rather than assumed from settings.
    public bool TryGetRowLayout(int row, out float xOffset, out int columns)
    {
        if (rowLayout.TryGetValue(row, out var layout))
        {
            xOffset = layout.xOffset;
            columns = layout.columns;
            return true;
        }
        xOffset = BandCenterX - settings.columns * settings.cellSize * 0.5f;
        columns = settings.columns;
        return false;
    }

    // Converts a world position into (row, col) maze indices using the ACTUAL layout of that row,
    // instead of assuming a fixed band width. Use this anywhere you need to figure out which cell
    // something (an enemy, a pickup, etc.) currently occupies from its transform position.
    public void WorldToCell(Vector3 worldPos, out int row, out int col)
    {
        row = Mathf.FloorToInt(worldPos.y / CellSize);
        TryGetRowLayout(row, out float xOffset, out int columns);
        col = Mathf.Clamp(Mathf.FloorToInt((worldPos.x - xOffset) / CellSize), 0, columns - 1);
    }

    public float BandWidthAtWorldY(float worldY)
    {
        int row = Mathf.FloorToInt(worldY / CellSize);
        if (rowLayout.TryGetValue(row, out var layout)) return layout.columns * CellSize;
        return settings.columns * CellSize;
    }

    public bool IsWideBandAtWorldY(float worldY)
    {
        int row = Mathf.FloorToInt(worldY / CellSize);
        if (rowLayout.TryGetValue(row, out var layout)) return layout.columns > settings.columns;
        return false;
    }

    private float GenerateBand(int startRow, int numRows, float entryWorldX, bool isFirstBand = false)
    {
        System.Random rng = new System.Random();

        int bandColumns = settings.columns;
        bool isWideBand = false;
        bandsSinceWide++;
        if (settings.enableWideRevealBands && !isFirstBand && bandsSinceWide > settings.minBandsBetweenWideBands && rng.NextDouble() < settings.wideBandChance)
        {
            int rawWideColumns = Mathf.Max(settings.columns, Mathf.RoundToInt(settings.columns * settings.wideBandColumnMultiplier));
            int diff = rawWideColumns - settings.columns;
            if (diff % 2 != 0) rawWideColumns += 1;
            bandColumns = rawWideColumns;
            isWideBand = true;
            bandsSinceWide = 0;
        }

        float xOffset = BandCenterX - bandColumns * settings.cellSize * 0.5f;
        int entryCol = Mathf.Clamp(Mathf.RoundToInt((entryWorldX - xOffset) / settings.cellSize - 0.5f), 0, bandColumns - 1);

        EnsureRowCapacity(startRow + numRows - 1);

        MazeCell[][] band = new MazeCell[numRows][];
        for (int r = 0; r < numRows; r++)
        {
            band[r] = new MazeCell[bandColumns];
            for (int c = 0; c < bandColumns; c++) band[r][c] = new MazeCell();
            rows[startRow + r] = band[r];
            rowLayout[startRow + r] = new BandLayout { columns = bandColumns, xOffset = xOffset };
        }

        float bandHorizontalBias = (float)(settings.horizontalCarveBiasRange.x +
            rng.NextDouble() * (settings.horizontalCarveBiasRange.y - settings.horizontalCarveBiasRange.x));
        float bandStaircaseChance = (float)(settings.midRunStaircaseChanceRange.x +
            rng.NextDouble() * (settings.midRunStaircaseChanceRange.y - settings.midRunStaircaseChanceRange.x));

        Vector2Int horizontalRunRange = isWideBand ? settings.wideBandHorizontalRunCellsRange : settings.minHorizontalRunCellsRange;
        Vector2Int verticalRunRange = isWideBand ? settings.wideBandVerticalRunCellsRange : settings.verticalRunCellsRange;
        float resumeDirectionChance = isWideBand ? settings.wideBandResumeDirectionChance : 0.85f;
        if (isWideBand)
        {
            bandHorizontalBias = Mathf.Clamp01(bandHorizontalBias * 0.6f);
            bandStaircaseChance = Mathf.Clamp01(bandStaircaseChance + 0.3f);
        }

        bool[,] blocked = new bool[numRows, bandColumns];
        if (isWideBand)
        {
            blocked = BuildWideBandBlockedMask(numRows, bandColumns, entryCol, rng);
            int diff = bandColumns - settings.columns;
            int offset = diff / 2;
            for (int c = 0; c < bandColumns; c++)
            {
                if (c < offset || c >= offset + settings.columns) blocked[numRows - 1, c] = true;
            }
        }

        List<(int r, int c)> path = null;
        int maxAttempts = isWideBand ? 4 : 1;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            for (int r = 0; r < numRows; r++)
                for (int c = 0; c < bandColumns; c++)
                    band[r][c] = new MazeCell();

            var stack = new Stack<(int r, int c)>();
            stack.Push((0, entryCol));
            band[0][entryCol].visited = true;

            var incomingDir = new Dictionary<(int, int), string>();
            var runLength = new Dictionary<(int, int), int>();
            var runTarget = new Dictionary<(int, int), int>();
            var lastHorizontalDir = new Dictionary<(int, int), string>();
            var verticalRunTarget = new Dictionary<(int, int), int>();
            incomingDir[(0, entryCol)] = null;
            runLength[(0, entryCol)] = 0;

            while (stack.Count > 0)
            {
                var (r, c) = stack.Peek();
                var options = new List<(int nr, int nc, string wallHere, string wallThere)>();

                if (r + 1 < numRows && !band[r + 1][c].visited && !blocked[r + 1, c]) options.Add((r + 1, c, "N", "S"));
                if (r - 1 >= 0 && !band[r - 1][c].visited && !blocked[r - 1, c]) options.Add((r - 1, c, "S", "N"));
                if (c + 1 < bandColumns && !band[r][c + 1].visited && !blocked[r, c + 1]) options.Add((r, c + 1, "E", "W"));
                if (c - 1 >= 0 && !band[r][c - 1].visited && !blocked[r, c - 1]) options.Add((r, c - 1, "W", "E"));

                if (options.Count == 0) { stack.Pop(); continue; }

                int target = runTarget.TryGetValue((r, c), out var t) ? t : horizontalRunRange.x;
                string lastHoriz = lastHorizontalDir.TryGetValue((r, c), out var lh) ? lh : null;
                int vTarget = verticalRunTarget.TryGetValue((r, c), out var vt) ? vt : verticalRunRange.x;

                var pick = MomentumPick(options, rng, incomingDir[(r, c)], runLength[(r, c)], target, vTarget, lastHoriz, bandHorizontalBias, bandStaircaseChance, resumeDirectionChance);
                SetWall(band[r][c], pick.wallHere, false);
                SetWall(band[pick.nr][pick.nc], pick.wallThere, false);
                band[pick.nr][pick.nc].visited = true;

                incomingDir[(pick.nr, pick.nc)] = pick.wallHere;
                runLength[(pick.nr, pick.nc)] = (pick.wallHere == incomingDir[(r, c)]) ? runLength[(r, c)] + 1 : 1;

                if (pick.wallHere == "E" || pick.wallHere == "W")
                {
                    lastHorizontalDir[(pick.nr, pick.nc)] = pick.wallHere;
                    runTarget[(pick.nr, pick.nc)] = (pick.wallHere == incomingDir[(r, c)] && runTarget.ContainsKey((r, c)))
                        ? runTarget[(r, c)]
                        : rng.Next(horizontalRunRange.x, horizontalRunRange.y + 1);
                }
                else
                {
                    if (lastHorizontalDir.ContainsKey((r, c))) lastHorizontalDir[(pick.nr, pick.nc)] = lastHorizontalDir[(r, c)];
                    if (runTarget.ContainsKey((r, c))) runTarget[(pick.nr, pick.nc)] = runTarget[(r, c)];

                    verticalRunTarget[(pick.nr, pick.nc)] = (pick.wallHere == incomingDir[(r, c)] && verticalRunTarget.ContainsKey((r, c)))
                        ? verticalRunTarget[(r, c)]
                        : rng.Next(verticalRunRange.x, verticalRunRange.y + 1);
                }

                stack.Push((pick.nr, pick.nc));
            }

            path = ExtractSinglePath(band, numRows, bandColumns, entryCol);
            if (path[path.Count - 1].r == numRows - 1) break; 

            blocked = new bool[numRows, bandColumns];
            if (isWideBand)
            {
                int diff = bandColumns - settings.columns;
                int offset = diff / 2;
                for (int c = 0; c < bandColumns; c++)
                {
                    if (c < offset || c >= offset + settings.columns) blocked[numRows - 1, c] = true;
                }
            }
        }

        generatedUpToRow = startRow + numRows - 1;

        HashSet<(int, int)> spawnCells;
        List<(int, int)> offPathDetourCells = new List<(int, int)>();
        (int r, int c)? partnerCell = null;

        if (isWideBand)
        {
            spawnCells = new HashSet<(int, int)>();
            for (int r = 0; r < numRows; r++)
                for (int c = 0; c < bandColumns; c++)
                    if (band[r][c].visited) spawnCells.Add((r, c));

            var (entryR, entryC) = path[0];
            band[entryR][entryC].wallS = false;
            var (exitR, exitC) = path[path.Count - 1];
            band[exitR][exitC].wallN = false;
            foreach (var cell in path) band[cell.r][cell.c].onPath = true;

            PlaceObstaclesWideBand(startRow, band, rng, spawnCells, path);

            if (settings.requirePartnerInWideBands && partnerPrefab != null)
            {
                partnerCell = ChoosePartnerCell(band, numRows, bandColumns, spawnCells, path, rng);
            }
        }
        else
        {
            CollapseToSinglePath(band, numRows, bandColumns, path);
            spawnCells = new HashSet<(int, int)>(path);
            PlaceObstacles(startRow, numRows, band, bandColumns, rng, path, spawnCells, offPathDetourCells);
        }

        SpawnBandGeometry(startRow, spawnCells, band, xOffset);
        SpawnAmmoAndCats(startRow, numRows, band, spawnCells, path, offPathDetourCells, xOffset, rng, partnerCell);
        if (partnerCell.HasValue) SpawnPartnerAndGate(startRow, numRows, xOffset, path, partnerCell.Value);
        TrySpawnBackgroundRoom(startRow, numRows, xOffset, spawnCells, rng);

        int exitCol = path[path.Count - 1].c;
        float exitWorldX = xOffset + exitCol * settings.cellSize + settings.cellSize * 0.5f;
        return exitWorldX;
    }

    private void SpawnAmmoAndCats(int startRow, int numRows, MazeCell[][] band, HashSet<(int, int)> spawnCells, 
        List<(int r, int c)> path, List<(int r, int c)> detourCells, float xOffset, System.Random rng,
        (int r, int c)? reservedCell = null)
    {
        for (int r = 0; r < numRows; r++)
        {
            int worldRow = startRow + r;
            float worldHeightMeters = worldRow * CellSize;

            for (int c = 0; c < band[r].Length; c++)
            {
                if (!spawnCells.Contains((r, c))) continue;
                if (reservedCell.HasValue && reservedCell.Value.r == r && reservedCell.Value.c == c) continue; // keep the partner's cell free of ammo/cats
                Vector3 cellCenter = new Vector3(xOffset + c * CellSize + CellSize * 0.5f, worldRow * CellSize + CellSize * 0.5f, 0f);

                // Plant ammo pickups (prefer detour cells so players must go out of their way)
                if (ammoPickupPrefab != null && band[r][c].obstacle == ObstacleType.None)
                {
                    bool isDetour = detourCells.Contains((r, c));
                    double spawnChance = isDetour ? settings.ammoSpawnChance * 2f : settings.ammoSpawnChance * 0.3f;
                    if (rng.NextDouble() < spawnChance)
                    {
                        var ammo = Instantiate(ammoPickupPrefab, cellCenter, Quaternion.identity, transform);
                        if (!spawnedByRow.ContainsKey(worldRow)) spawnedByRow[worldRow] = new List<GameObject>();
                        spawnedByRow[worldRow].Add(ammo);
                    }
                }

                // Enemy Cat Spawning (Starts spawning at 50 meters)
                if (enemyCatPrefab != null && worldHeightMeters >= settings.catSpawnMinHeightMeters)
                {
                    if (rng.NextDouble() < settings.catSpawnChance)
                    {
                        var cat = Instantiate(enemyCatPrefab, cellCenter, Quaternion.identity, transform);
                        if (!spawnedByRow.ContainsKey(worldRow)) spawnedByRow[worldRow] = new List<GameObject>();
                        spawnedByRow[worldRow].Add(cat);
                    }
                }
            }
        }
    }

    // Picks where the partner waits: a cell OFF the shortest entry->exit route, as far from that
    // route (and from the entrance) as possible, so the player really has to explore a side branch.
    private (int r, int c)? ChoosePartnerCell(MazeCell[][] band, int numRows, int bandColumns,
        HashSet<(int, int)> spawnCells, List<(int r, int c)> path, System.Random rng)
    {
        var pathSet = new HashSet<(int, int)>();
        foreach (var p in path) pathSet.Add((p.r, p.c));

        var distFromPath = BfsDistances(band, numRows, bandColumns, path);
        var distFromEntry = BfsDistances(band, numRows, bandColumns, new List<(int r, int c)> { path[0] });

        float bestScore = -1f;
        var best = new List<(int, int)>();
        foreach (var cell in spawnCells)
        {
            if (pathSet.Contains(cell)) continue;
            if (!distFromPath.TryGetValue(cell, out int dp)) continue;
            if (!distFromEntry.TryGetValue(cell, out int de)) continue;

            float score = dp + 0.5f * de;
            if (score > bestScore + 0.001f) { bestScore = score; best.Clear(); best.Add(cell); }
            else if (Mathf.Abs(score - bestScore) <= 0.001f) best.Add(cell);
        }

        (int r, int c) chosen;
        if (best.Count > 0)
        {
            var pick = best[rng.Next(best.Count)];
            chosen = (pick.Item1, pick.Item2);
        }
        else
        {
            // Degenerate band with no side branches: fall back to the middle of the route.
            chosen = path[path.Count / 2];
        }

        band[chosen.r][chosen.c].obstacle = ObstacleType.None; // never bury the partner in steam/wire
        return chosen;
    }

    private Dictionary<(int, int), int> BfsDistances(MazeCell[][] band, int numRows, int bandColumns, List<(int r, int c)> sources)
    {
        var dist = new Dictionary<(int, int), int>();
        var queue = new Queue<(int, int)>();
        foreach (var s in sources)
        {
            if (dist.ContainsKey((s.r, s.c))) continue;
            dist[(s.r, s.c)] = 0;
            queue.Enqueue((s.r, s.c));
        }

        void Step(bool open, int fromDist, int nr, int nc)
        {
            if (!open) return;
            if (nr < 0 || nr >= numRows || nc < 0 || nc >= bandColumns) return;
            if (dist.ContainsKey((nr, nc))) return;
            dist[(nr, nc)] = fromDist + 1;
            queue.Enqueue((nr, nc));
        }

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            int d = dist[(r, c)];
            MazeCell cell = band[r][c];
            Step(!cell.wallN, d, r + 1, c);
            Step(!cell.wallS, d, r - 1, c);
            Step(!cell.wallE, d, r, c + 1);
            Step(!cell.wallW, d, r, c - 1);
        }
        return dist;
    }

    private void SpawnPartnerAndGate(int startRow, int numRows, float xOffset, List<(int r, int c)> path, (int r, int c) partnerCell)
    {
        var quest = new PartnerQuest { startRow = startRow, endRow = startRow + numRows - 1 };

        // Gate: sits on the north edge of the exit cell, exactly where the opening into the next band is.
        var exit = path[path.Count - 1];
        int exitWorldRow = startRow + exit.r;
        Vector3 exitCenter = new Vector3(xOffset + exit.c * CellSize + CellSize * 0.5f, exitWorldRow * CellSize + CellSize * 0.5f, 0f);
        Vector3 gatePos = exitCenter + new Vector3(0f, CellSize * 0.5f, 0f);

        GameObject gateSource = gatePrefab != null ? gatePrefab : wallSegmentPrefab;
        if (gateSource != null)
        {
            var gate = Instantiate(gateSource, gatePos, Quaternion.identity, transform);
            gate.transform.localScale = new Vector3(CellSize, gate.transform.localScale.y, gate.transform.localScale.z);
            if (gatePrefab == null)
            {
                foreach (var sr in gate.GetComponentsInChildren<SpriteRenderer>()) sr.color = fallbackGateTint;
            }
            quest.gate = gate;
            RegisterSpawned(gatePos, gate);
        }

        // Partner
        int partnerWorldRow = startRow + partnerCell.r;
        Vector3 partnerPos = new Vector3(xOffset + partnerCell.c * CellSize + CellSize * 0.5f, partnerWorldRow * CellSize + CellSize * 0.5f, 0f);
        var partnerGO = Instantiate(partnerPrefab, partnerPos, Quaternion.identity, transform);
        var partner = partnerGO.GetComponent<PartnerCharacter>();
        if (partner != null) partner.Init(this, quest.startRow);
        RegisterSpawned(partnerPos, partnerGO);

        partnerQuests.Add(quest);
    }

    private bool[,] BuildWideBandBlockedMask(int numRows, int bandColumns, int entryCol, System.Random rng)
    {
        bool[,] blocked = new bool[numRows, bandColumns];
        if (numRows <= 2) return blocked;

        int totalCarveableRows = numRows - 2;
        int area = totalCarveableRows * bandColumns;
        int targetBlocked = Mathf.RoundToInt(area * Mathf.Clamp01(settings.wideBandBlockedAreaFraction));

        int lo = Mathf.Max(1, settings.wideBandBlockClusterSizeRange.x);
        int hi = Mathf.Max(lo, settings.wideBandBlockClusterSizeRange.y);

        int blockedCount = 0;
        int safetyAttempts = 0;
        while (blockedCount < targetBlocked && safetyAttempts < 200)
        {
            safetyAttempts++;
            int clusterW = rng.Next(lo, hi + 1);
            int clusterH = rng.Next(lo, hi + 1);

            int startRow = rng.Next(1, Mathf.Max(2, numRows - 1 - clusterH + 1));
            int startCol = rng.Next(0, Mathf.Max(1, bandColumns - clusterW + 1));

            for (int r = startRow; r < Mathf.Min(startRow + clusterH, numRows - 1); r++)
            {
                for (int c = startCol; c < Mathf.Min(startCol + clusterW, bandColumns); c++)
                {
                    if (r == 0 && c == entryCol) continue;
                    if (!blocked[r, c])
                    {
                        blocked[r, c] = true;
                        blockedCount++;
                    }
                }
            }
        }
        return blocked;
    }

    private List<(int r, int c)> ExtractSinglePath(MazeCell[][] band, int numRows, int bandColumns, int entryCol)
    {
        var visited = new bool[numRows, bandColumns];
        var parent = new Dictionary<(int, int), (int, int)>();
        var queue = new Queue<(int, int)>();

        queue.Enqueue((0, entryCol));
        visited[0, entryCol] = true;
        (int, int) goal = (0, entryCol);

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            if (r == numRows - 1) { goal = (r, c); break; }

            MazeCell cell = band[r][c];
            TryVisit(visited, parent, queue, r, c, !cell.wallN, r + 1, c, numRows, bandColumns);
            TryVisit(visited, parent, queue, r, c, !cell.wallS, r - 1, c, numRows, bandColumns);
            TryVisit(visited, parent, queue, r, c, !cell.wallE, r, c + 1, numRows, bandColumns);
            TryVisit(visited, parent, queue, r, c, !cell.wallW, r, c - 1, numRows, bandColumns);
        }

        var path = new List<(int, int)>();
        var cur = goal;
        while (cur != (0, entryCol))
        {
            path.Add(cur);
            cur = parent[cur];
        }
        path.Add((0, entryCol));
        path.Reverse();
        return path;
    }

    private void TryVisit(bool[,] visited, Dictionary<(int, int), (int, int)> parent,
        Queue<(int, int)> queue, int r, int c, bool open, int nr, int nc, int numRows, int bandColumns)
    {
        if (!open) return;
        if (nr < 0 || nr >= numRows || nc < 0 || nc >= bandColumns) return;
        if (visited[nr, nc]) return;
        visited[nr, nc] = true;
        parent[(nr, nc)] = (r, c);
        queue.Enqueue((nr, nc));
    }

    private void CollapseToSinglePath(MazeCell[][] band, int numRows, int bandColumns, List<(int r, int c)> path)
    {
        for (int r = 0; r < numRows; r++)
        {
            for (int c = 0; c < bandColumns; c++)
            {
                band[r][c].wallN = true;
                band[r][c].wallS = true;
                band[r][c].wallE = true;
                band[r][c].wallW = true;
                band[r][c].onPath = false;
            }
        }

        for (int i = 0; i < path.Count; i++)
        {
            var (r, c) = path[i];
            band[r][c].onPath = true;
            if (i > 0)
            {
                var (pr, pc) = path[i - 1];
                OpenBetween(band, pr, pc, r, c);
            }
        }

        var (entryR, entryC) = path[0];
        band[entryR][entryC].wallS = false;

        var (exitR, exitC) = path[path.Count - 1];
        band[exitR][exitC].wallN = false;
    }

    private void OpenBetween(MazeCell[][] band, int r1, int c1, int r2, int c2)
    {
        if (r2 == r1 + 1) { band[r1][c1].wallN = false; band[r2][c2].wallS = false; }
        else if (r2 == r1 - 1) { band[r1][c1].wallS = false; band[r2][c2].wallN = false; }
        else if (c2 == c1 + 1) { band[r1][c1].wallE = false; band[r2][c2].wallW = false; }
        else if (c2 == c1 - 1) { band[r1][c1].wallW = false; band[r2][c2].wallE = false; }
    }

    private void SetWall(MazeCell cell, string side, bool value)
    {
        switch (side)
        {
            case "N": cell.wallN = value; break;
            case "S": cell.wallS = value; break;
            case "E": cell.wallE = value; break;
            case "W": cell.wallW = value; break;
        }
    }

    private void EnsureRowCapacity(int row)
    {
        while (rows.Count <= row) rows.Add(null);
    }

    private void PlaceObstacles(int startRow, int numRows, MazeCell[][] band, int bandColumns, System.Random rng,
        List<(int r, int c)> path, HashSet<(int, int)> spawnCells, List<(int r, int c)> detourCells)
    {
        for (int i = 2; i < path.Count - 1; i++)
        {
            var (r, c) = path[i];
            if (startRow + r == 0) continue; 

            double roll = rng.NextDouble();
            ObstacleType obstacle = ObstacleType.None;
            FanDirection fanDir = FanDirection.North;
            int fanSpan = 1;

            if (roll < settings.steamChance) obstacle = ObstacleType.Steam;
            else if (roll < settings.steamChance + settings.wireChance) obstacle = ObstacleType.Wire;
            else if (roll < settings.steamChance + settings.wireChance + settings.fanChance)
            {
                obstacle = ObstacleType.Fan;
                fanDir = PickFanDirection(path, i, rng);
                fanSpan = PickFanSpan(path, i, fanDir, rng);
            }

            if (obstacle == ObstacleType.None) continue;

            band[r][c].obstacle = obstacle;
            if (obstacle == ObstacleType.Fan)
            {
                band[r][c].fanDir = fanDir;
                band[r][c].fanSpanCells = fanSpan;
            }

            if (rng.NextDouble() < settings.hazardBypassChance)
            {
                TryCarveBypass(band, numRows, bandColumns, path, i, spawnCells, detourCells, rng);
            }
        }
    }

    private void PlaceObstaclesWideBand(int startRow, MazeCell[][] band, System.Random rng,
        HashSet<(int, int)> spawnCells, List<(int r, int c)> mainPath)
    {
        var entry = mainPath[0];
        var exit = mainPath[mainPath.Count - 1];
        double steamChance = settings.steamChance * settings.wideBandHazardChanceMultiplier;
        double wireChance = settings.wireChance * settings.wideBandHazardChanceMultiplier;

        foreach (var (r, c) in spawnCells)
        {
            if (startRow + r == 0) continue;             
            if (r == entry.r && c == entry.c) continue;   
            if (r == exit.r && c == exit.c) continue;     

            double roll = rng.NextDouble();
            if (roll < steamChance) band[r][c].obstacle = ObstacleType.Steam;
            else if (roll < steamChance + wireChance) band[r][c].obstacle = ObstacleType.Wire;
        }
    }

    private (int maxBack, int maxFwd) ComputeStraightRun(List<(int r, int c)> path, int i, int dirR, int dirC)
    {
        int maxBack = 0;
        while (i - 2 - maxBack >= 0)
        {
            var p0 = path[i - 2 - maxBack];
            var p1 = path[i - 1 - maxBack];
            if (p1.r - p0.r == dirR && p1.c - p0.c == dirC) maxBack++; else break;
        }
        int maxFwd = 0;
        while (i + 2 + maxFwd < path.Count)
        {
            var p0 = path[i + 1 + maxFwd];
            var p1 = path[i + 2 + maxFwd];
            if (p1.r - p0.r == dirR && p1.c - p0.c == dirC) maxFwd++; else break;
        }
        return (maxBack, maxFwd);
    }

    private FanDirection PickFanDirection(List<(int r, int c)> path, int i, System.Random rng)
    {
        var B = path[i];
        var C = path[i + 1];
        int outR = C.r - B.r, outC = C.c - B.c;

        bool travelingHorizontally = outC != 0;
        FanDirection optionA = travelingHorizontally ? FanDirection.East : FanDirection.North;
        FanDirection optionB = travelingHorizontally ? FanDirection.West : FanDirection.South;
        return rng.NextDouble() < 0.5 ? optionA : optionB;
    }

    private int PickFanSpan(List<(int r, int c)> path, int i, FanDirection dir, System.Random rng)
    {
        bool axisIsHorizontal = dir == FanDirection.East || dir == FanDirection.West;
        int dirR = axisIsHorizontal ? 0 : 1;
        int dirC = axisIsHorizontal ? 1 : 0;
        var (maxBack, maxFwd) = ComputeStraightRun(path, i, dirR, dirC);
        int maxAvailable = 1 + maxBack + maxFwd; 

        int lo = Mathf.Max(1, settings.fanTunnelLengthCellsRange.x);
        int hi = Mathf.Max(lo, settings.fanTunnelLengthCellsRange.y);
        int desired = rng.Next(lo, hi + 1);
        return Mathf.Min(desired, maxAvailable);
    }

    private bool TryCarveBypass(MazeCell[][] band, int numRows, int bandColumns,
        List<(int r, int c)> path, int i, HashSet<(int, int)> spawnCells, List<(int r, int c)> detourCells, System.Random rng)
    {
        var A = path[i - 1];
        var B = path[i];
        var C = path[i + 1];
        int inR = B.r - A.r, inC = B.c - A.c;
        int outR = C.r - B.r, outC = C.c - B.c;

        bool IsFree((int r, int c) cell)
        {
            if (cell.r < 0 || cell.r >= numRows || cell.c < 0 || cell.c >= bandColumns) return false;
            if (spawnCells.Contains(cell)) return false;
            var mc = band[cell.r][cell.c];
            return mc.wallN && mc.wallS && mc.wallE && mc.wallW;
        }

        if (inR == outR && inC == outC)
        {
            var (maxBack, maxFwd) = ComputeStraightRun(path, i, inR, inC);

            int extLo = Mathf.Max(0, settings.bypassDetourExtensionRange.x);
            int extHi = Mathf.Max(extLo, settings.bypassDetourExtensionRange.y);
            int extraBack = Mathf.Min(maxBack, rng.Next(extLo, extHi + 1));
            int extraFwd = Mathf.Min(maxFwd, rng.Next(extLo, extHi + 1));

            int startIdx = i - 1 - extraBack;
            int endIdx = i + 1 + extraFwd;

            int perpR = inC != 0 ? 1 : 0; 
            int perpC = inR != 0 ? 1 : 0;

            foreach (int sign in new[] { 1, -1 })
            {
                var lane = new List<(int r, int c)>();
                bool ok = true;
                for (int k = startIdx; k <= endIdx; k++)
                {
                    var p = path[k];
                    var off = (p.r + perpR * sign, p.c + perpC * sign);
                    if (!IsFree(off)) { ok = false; break; }
                    lane.Add(off);
                }
                if (!ok) continue;

                var start = path[startIdx];
                var end = path[endIdx];
                OpenBetween(band, start.r, start.c, lane[0].r, lane[0].c);
                for (int k = 0; k < lane.Count - 1; k++)
                    OpenBetween(band, lane[k].r, lane[k].c, lane[k + 1].r, lane[k + 1].c);
                OpenBetween(band, lane[lane.Count - 1].r, lane[lane.Count - 1].c, end.r, end.c);
                foreach (var cell in lane) 
                {
                    spawnCells.Add(cell);
                    detourCells.Add(cell);
                }
                return true;
            }
            return false;
        }
        else
        {
            var d = (A.r + outR, A.c + outC);
            if (d != B && IsFree(d))
            {
                OpenBetween(band, A.r, A.c, d.Item1, d.Item2);
                OpenBetween(band, d.Item1, d.Item2, C.r, C.c);
                spawnCells.Add(d);
                detourCells.Add(d);
                return true;
            }
            return false;
        }
    }

    private void SpawnBandGeometry(int startRow, HashSet<(int, int)> spawnCells, MazeCell[][] band, float xOffset)
    {
        foreach (var (r, c) in spawnCells)
        {
            int worldRow = startRow + r;
            if (!spawnedByRow.ContainsKey(worldRow)) spawnedByRow[worldRow] = new List<GameObject>();

            MazeCell cell = band[r][c];
            Vector3 center = new Vector3(xOffset + c * CellSize + CellSize * 0.5f, worldRow * CellSize + CellSize * 0.5f, 0f);

            if (floorTilePrefab != null)
            {
                var floor = Instantiate(floorTilePrefab, center, Quaternion.identity, transform);
                spawnedByRow[worldRow].Add(floor);
            }

            SpawnWallIfNeeded(cell.wallN, center, new Vector3(0, CellSize * 0.5f, 0), true, worldRow);
            SpawnWallIfNeeded(cell.wallS, center, new Vector3(0, -CellSize * 0.5f, 0), true, worldRow);
            SpawnWallIfNeeded(cell.wallE, center, new Vector3(CellSize * 0.5f, 0, 0), false, worldRow);
            SpawnWallIfNeeded(cell.wallW, center, new Vector3(-CellSize * 0.5f, 0, 0), false, worldRow);

            SpawnObstacle(cell, center, worldRow);
        }
    }

    private void SpawnWallIfNeeded(bool present, Vector3 cellCenter, Vector3 localOffset, bool horizontal, int worldRow)
    {
        if (!present || wallSegmentPrefab == null) return;
        Vector3 pos = cellCenter + localOffset;
        Quaternion rot = horizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90);
        var go = Instantiate(wallSegmentPrefab, pos, rot, transform);
        go.transform.localScale = new Vector3(CellSize, go.transform.localScale.y, go.transform.localScale.z);
        spawnedByRow[worldRow].Add(go);
    }

    private void SpawnObstacle(MazeCell cell, Vector3 center, int worldRow)
    {
        GameObject prefab = cell.obstacle switch
        {
            ObstacleType.Steam => steamPrefab,
            ObstacleType.Wire => wirePrefab,
            ObstacleType.Fan => fanPrefab,
            _ => null
        };
        if (prefab == null) return;

        var go = Instantiate(prefab, center, Quaternion.identity, transform);
        if (cell.obstacle == ObstacleType.Fan)
        {
            var fan = go.GetComponent<FanZone>();
            if (fan != null)
            {
                fan.SetDirection(cell.fanDir);
                fan.SetSpan(cell.fanSpanCells, CellSize);
            }
        }
        spawnedByRow[worldRow].Add(go);
    }

    private void TrySpawnBackgroundRoom(int startRow, int numRows, float xOffset, HashSet<(int, int)> spawnCells, System.Random rng)
    {
        bandsSinceRoom++;
        if (roomPrefabs == null || roomPrefabs.Length == 0) return;
        if (bandsSinceRoom <= settings.minBandsBetweenRooms) return;
        if (rng.NextDouble() >= settings.roomSpawnChance) return;

        int localRow = rng.Next(0, numRows);
        int minCol = int.MaxValue, maxCol = int.MinValue;
        foreach (var cell in spawnCells)
        {
            if (cell.Item1 != localRow) continue;
            if (cell.Item2 < minCol) minCol = cell.Item2;
            if (cell.Item2 > maxCol) maxCol = cell.Item2;
        }
        if (maxCol < minCol) return; 

        bandsSinceRoom = 0;

        int worldRow = startRow + localRow;
        float y = worldRow * CellSize + CellSize * 0.5f;

        bool onRight = rng.NextDouble() < 0.5;
        float mazeEdgeX = onRight ? xOffset + (maxCol + 1) * CellSize : xOffset + minCol * CellSize;

        var prefab = roomPrefabs[rng.Next(roomPrefabs.Length)];
        var go = Instantiate(prefab, new Vector3(mazeEdgeX, y, 0f), Quaternion.identity, transform);

        Vector3 scale = go.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (onRight ? 1f : -1f);
        go.transform.localScale = scale;

        float halfWidth = 0f;
        var renderer = go.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null) halfWidth = renderer.bounds.extents.x;

        float roomX = onRight
            ? mazeEdgeX + settings.roomSideOffset + halfWidth
            : mazeEdgeX - settings.roomSideOffset - halfWidth;
        go.transform.position = new Vector3(roomX, y, 0f);

        if (!spawnedByRow.ContainsKey(worldRow)) spawnedByRow[worldRow] = new List<GameObject>();
        spawnedByRow[worldRow].Add(go);
    }

    private (int nr, int nc, string wallHere, string wallThere) MomentumPick(
        List<(int nr, int nc, string wallHere, string wallThere)> options, System.Random rng,
        string enteredDir, int runLen, int horizontalTarget, int verticalTarget, string lastHorizontalDir,
        float horizontalBias, float staircaseChance, float resumeDirectionChance = 0.85f)
    {
        bool enteredHorizontal = enteredDir == "E" || enteredDir == "W";
        int minRun = enteredHorizontal ? horizontalTarget : verticalTarget;

        if (enteredDir != null && runLen < minRun)
        {
            var continueOption = options.Find(o => o.wallHere == enteredDir);
            if (continueOption.wallHere != null) return continueOption;
        }

        if (enteredHorizontal && lastHorizontalDir != null && rng.NextDouble() < staircaseChance)
        {
            var upOption = options.Find(o => o.wallHere == "N");
            if (upOption.wallHere != null) return upOption;
        }

        var horizontalOptions = options.FindAll(o => o.wallHere == "E" || o.wallHere == "W");

        if (lastHorizontalDir != null)
        {
            var resumeOption = horizontalOptions.Find(o => o.wallHere == lastHorizontalDir);
            if (resumeOption.wallHere != null && rng.NextDouble() < resumeDirectionChance) return resumeOption;
        }

        if (horizontalOptions.Count > 0 && rng.NextDouble() < horizontalBias)
        {
            return horizontalOptions[rng.Next(horizontalOptions.Count)];
        }

        return options[rng.Next(options.Count)];
    }
}