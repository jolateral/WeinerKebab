using System.Collections.Generic;
using UnityEngine;

// Attach to an empty GameObject called "MazeGenerator" at world origin.
public class MazeGenerator : MonoBehaviour
{
    [Header("Config")]
    public GameSettings settings;

    [Header("Prefabs (assign in Inspector)")]
    public GameObject wallSegmentPrefab; // thin box, e.g. a 1 x 0.15 x 1 cube or a sprite
    public GameObject steamPrefab;       // trigger volume with SteamZone.cs
    public GameObject wirePrefab;        // trigger volume with WireHazard.cs
    public GameObject fanPrefab;         // trigger volume with FanZone.cs
    public GameObject floorTilePrefab;   // optional, purely visual

    [Tooltip("Decorative room prefabs placed off to the side of the vent for visual interest (see GameSettings > Background Rooms for frequency/spacing). Purely visual - no collider needed, since these always spawn outside the maze's own columns where the player can never go. Assign 4-6 variants for variety; one is picked at random each time.")]
    public GameObject[] roomPrefabs;

    // rows[r][c] = cell data. Row 0 is the starting row at the bottom.
    // NOTE: each row's array length can differ band-to-band (wide reveal bands are wider), so
    // always go through CellWorldCenter / rowLayout rather than assuming a fixed column count.
    private List<MazeCell[]> rows = new List<MazeCell[]>();
    private int generatedUpToRow = -1;
    private float lastExitWorldX;
    private int bandsSinceWide = 999; // large so the very first eligible band CAN roll wide
    private int bandsSinceRoom = 999; // large so the very first eligible band CAN roll a room

    // Per-row layout so world positions and hit-testing work even though band width varies.
    private struct BandLayout { public int columns; public float xOffset; }
    private Dictionary<int, BandLayout> rowLayout = new Dictionary<int, BandLayout>();

    // Track spawned GameObjects per row so we can clean up rows far below the flood.
    private Dictionary<int, List<GameObject>> spawnedByRow = new Dictionary<int, List<GameObject>>();

    public float CellSize => settings.cellSize;
    public int Columns => settings.columns; // base/default column count (wide bands are wider - see BandWidthAtWorldY)

    // The camera should always frame around this fixed X - every band (normal or wide) is
    // centered on it, wide bands just extend further left/right from the same center.
    public float BandCenterX => settings.columns * settings.cellSize * 0.5f;

    void Awake()
    {
        lastExitWorldX = BandCenterX; // first band always starts centered
        // Generate a few bands immediately so the player has somewhere to stand at start.
        lastExitWorldX = GenerateBand(0, settings.rowsPerBand, lastExitWorldX, isFirstBand: true);
        for (int i = 0; i < settings.bandsAheadBuffer; i++)
        {
            lastExitWorldX = GenerateBand(generatedUpToRow + 1, settings.rowsPerBand, lastExitWorldX);
        }
    }

    // Call this every frame (or periodically) from GameManager, passing the current flood height.
    public void EnsureGeneratedAhead(float floodWorldY)
    {
        int floodRow = Mathf.FloorToInt(floodWorldY / CellSize);
        int neededRow = floodRow + settings.rowsPerBand * settings.bandsAheadBuffer;
        while (generatedUpToRow < neededRow)
        {
            lastExitWorldX = GenerateBand(generatedUpToRow + 1, settings.rowsPerBand, lastExitWorldX);
        }

        // Cleanup: destroy spawned objects for rows well below the flood to keep the scene light.
        int cleanupBelowRow = floodRow - settings.rowsPerBand * 2;
        List<int> toRemove = new List<int>();
        foreach (var kvp in spawnedByRow)
        {
            if (kvp.Key < cleanupBelowRow)
            {
                foreach (var go in kvp.Value) if (go != null) Destroy(go);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var k in toRemove) { spawnedByRow.Remove(k); rowLayout.Remove(k); }
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

    // World-unit width of the maze band that occupies the given world Y - lets the camera know
    // how far to zoom out to fit whatever's currently (or about to be) on screen, since wide
    // reveal bands are wider than normal ones.
    public float BandWidthAtWorldY(float worldY)
    {
        int row = Mathf.FloorToInt(worldY / CellSize);
        if (rowLayout.TryGetValue(row, out var layout)) return layout.columns * CellSize;
        return settings.columns * CellSize;
    }

    // True if the band occupying this world Y is a wide "choice" band (wider than the normal
    // column count). CameraFollow uses this to ease off the flood's rise speed while the player is
    // navigating a wide band, so one wrong turn in a real branching layout is recoverable instead
    // of an instant death sentence.
    public bool IsWideBandAtWorldY(float worldY)
    {
        int row = Mathf.FloorToInt(worldY / CellSize);
        if (rowLayout.TryGetValue(row, out var layout)) return layout.columns > settings.columns;
        return false;
    }

    // Recursive backtracker over a band of rows, entering from a world-space X position on the
    // band's bottom edge (world X rather than a column index, since a wide reveal band's column
    // coordinate space differs from a normal band's).
    private float GenerateBand(int startRow, int numRows, float entryWorldX, bool isFirstBand = false)
    {
        System.Random rng = new System.Random();

        // Decide this band's width. Wide "reveal" bands are much wider and let the camera pull
        // back to show almost the whole layout at once, per the comic reference. Never on the
        // very first band, and gated by a cooldown so they stay a rare/special beat.
        int bandColumns = settings.columns;
        bool isWideBand = false;
        bandsSinceWide++;
        // enableWideRevealBands is the hard gate here - even if wideBandChance still has a stale
        // value saved on an old GameSettings asset, this guarantees bandColumns can never differ
        // between bands, which is what keeps every band fitting the screen and connecting cleanly
        // to the one below it.
        if (settings.enableWideRevealBands && !isFirstBand && bandsSinceWide > settings.minBandsBetweenWideBands && rng.NextDouble() < settings.wideBandChance)
        {
            int rawWideColumns = Mathf.Max(settings.columns, Mathf.RoundToInt(settings.columns * settings.wideBandColumnMultiplier));
            // Bands are always centered on the same fixed BandCenterX, so a wide band's xOffset is
            // settings.columns*cellSize*0.5 - rawWideColumns*cellSize*0.5 relative to a normal
            // band's xOffset of 0. If (rawWideColumns - settings.columns) is ODD, that difference
            // is a HALF-cell, not a whole one - meaning the wide band's entire cell grid sits
            // staggered half a cell sideways from every normal band's grid. No exit opening can
            // ever land squarely on the band above when that's the case (this was the seam
            // misalignment bug). Bumping to the next even difference keeps both grids on the same
            // lattice, so entryCol/exitCol always line up exactly, with no rounding slop.
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

        // Roll this band's "personality" once, up front, rather than reading a fixed value from
        // settings. This is what breaks the uniform feel - one band might commit hard to one long
        // horizontal sweep (high bias, long min run), the next might be scrappier and twistier
        // (lower bias, short min run), so the player can't predict what's coming a band ahead.
        float bandHorizontalBias = (float)(settings.horizontalCarveBiasRange.x +
            rng.NextDouble() * (settings.horizontalCarveBiasRange.y - settings.horizontalCarveBiasRange.x));
        float bandStaircaseChance = (float)(settings.midRunStaircaseChanceRange.x +
            rng.NextDouble() * (settings.midRunStaircaseChanceRange.y - settings.midRunStaircaseChanceRange.x));

        // Choice bands get their OWN carve profile instead of reusing the normal band's. The
        // normal profile is tuned to look right at the normal (narrow) column count - it favors
        // long horizontal "sweeps" that, at 2x+ the width, turn into big uninterrupted open bars
        // instead of a grid of individual shafts. Lower bias + a strong cap on how long any run
        // can get + a much lower "keep going the same way" chance keeps the visual grain (wall
        // spacing in world units) consistent with normal bands, just repeated across more shafts.
        Vector2Int horizontalRunRange = isWideBand ? settings.wideBandHorizontalRunCellsRange : settings.minHorizontalRunCellsRange;
        Vector2Int verticalRunRange = isWideBand ? settings.wideBandVerticalRunCellsRange : settings.verticalRunCellsRange;
        float resumeDirectionChance = isWideBand ? settings.wideBandResumeDirectionChance : 0.85f;
        if (isWideBand)
        {
            bandHorizontalBias = Mathf.Clamp01(bandHorizontalBias * 0.6f);
            bandStaircaseChance = Mathf.Clamp01(bandStaircaseChance + 0.3f);
        }

        // Choice bands carve around pre-blocked "rock/machinery" clusters instead of filling the
        // ENTIRE grid. This matters because a recursive-backtracker maze always visits 100% of the
        // cells it's allowed to touch before it stops - that's exactly why keeping the full carve
        // (instead of collapsing to one path, as normal bands do) was filling the whole band
        // edge-to-edge with corridor, reading as one solid block instead of a snaking maze. Blocking
        // out a chunk of the area up front gives the carve real gaps to wind around, so it reads at
        // the same density as your normal bands - just wider, with real branch choices.
        bool[,] blocked = new bool[numRows, bandColumns];
        if (isWideBand)
        {
            blocked = BuildWideBandBlockedMask(numRows, bandColumns, entryCol, rng);
            
            // FIX: Force the exit to align with the next normal band by blocking the top row's outer columns.
            int diff = bandColumns - settings.columns;
            int offset = diff / 2;
            for (int c = 0; c < bandColumns; c++)
            {
                if (c < offset || c >= offset + settings.columns)
                {
                    blocked[numRows - 1, c] = true;
                }
            }
        }

        List<(int r, int c)> path = null;
        int maxAttempts = isWideBand ? 4 : 1;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Reset to a blank slate for this attempt (needed on a retry after a failed blocked
            // carve below).
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
            if (path[path.Count - 1].r == numRows - 1) break; // reached the top - good, we're done

            // The blocked layout walled off the top row from the entry entirely (bad luck with
            // cluster placement). Drop the blocking and recarve one more time so we always end up
            // with a working band - worst case a choice band just looks like a normal full-grid
            // one instead of leaving the player stuck.
            blocked = new bool[numRows, bandColumns];
            
            // FIX: Re-apply the top row constraint even on a retry so we don't break alignment.
            if (isWideBand)
            {
                int diff = bandColumns - settings.columns;
                int offset = diff / 2;
                for (int c = 0; c < bandColumns; c++)
                {
                    if (c < offset || c >= offset + settings.columns)
                    {
                        blocked[numRows - 1, c] = true;
                    }
                }
            }
        }

        generatedUpToRow = startRow + numRows - 1;

        // ExtractSinglePath gives us THE route from entry to the top of the band (the carve above
        // produces a perfect maze - exactly one route between any two cells). Normal bands then
        // collapse everything else away so only that route remains, same as before. Wide "choice"
        // bands instead keep the ENTIRE carved maze intact - every branch the backtracker visited
        // is a real, walkable corridor - so the player gets a genuine "which way do I go" moment
        // instead of one forced path. Only the single top-row cell this path ends on gets its
        // ceiling opened, so the next band still connects cleanly; other branches are free to
        // dead-end at the top of the reveal, which is fine - the choice is about the routes
        // THROUGH the band, not about where it lets you out.
        HashSet<(int, int)> spawnCells;

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
        }
        else
        {
            CollapseToSinglePath(band, numRows, bandColumns, path);

            // spawnCells starts as just the single corridor - this is what keeps the maze reading
            // exactly like it used to. PlaceObstacles below adds a handful of small "step around"
            // detour cells to this set, one per bypassed hazard, so the vast majority of the layout
            // is still one clean path with only the occasional local loop around an obstacle.
            spawnCells = new HashSet<(int, int)>(path);
            PlaceObstacles(startRow, numRows, band, bandColumns, rng, path, spawnCells);
        }

        int exitCol = path[path.Count - 1].c;

        SpawnBandGeometry(startRow, spawnCells, band, xOffset);
        TrySpawnBackgroundRoom(startRow, numRows, xOffset, spawnCells, rng);

        float exitWorldX = xOffset + exitCol * settings.cellSize + settings.cellSize * 0.5f;
        return exitWorldX;
    }

    // Scatters several chunky rectangular "blocked" clusters (rock/machinery the vent routes
    // around) across a choice band before it's carved, so the maze only fills part of the grid
    // instead of the full rectangle. Chunky rectangles (rather than independently-random single
    // cells) read as deliberate obstacles instead of Swiss-cheese noise, and leave the remaining
    // open area connected enough for a normal-looking winding maze to form around them. Row 0
    // (the seam into this band) and the top row (the seam into the next band) are always left
    // fully clear so band-to-band connections stay reliable.
    private bool[,] BuildWideBandBlockedMask(int numRows, int bandColumns, int entryCol, System.Random rng)
    {
        bool[,] blocked = new bool[numRows, bandColumns];
        if (numRows <= 2) return blocked; // too shallow a band for this to be worth doing

        int totalCarveableRows = numRows - 2; // excludes row 0 and the top row
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

            int startRow = rng.Next(1, Mathf.Max(2, numRows - 1 - clusterH + 1)); // stays within rows [1, numRows-2]
            int startCol = rng.Next(0, Mathf.Max(1, bandColumns - clusterW + 1));

            for (int r = startRow; r < Mathf.Min(startRow + clusterH, numRows - 1); r++)
            {
                for (int c = startCol; c < Mathf.Min(startCol + clusterW, bandColumns); c++)
                {
                    if (r == 0 && c == entryCol) continue; // never block the entry cell itself
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


    // produces a perfect maze (exactly one route between any two cells), this finds THE route from
    // the entry to the top of the band.
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
        List<(int r, int c)> path, HashSet<(int, int)> spawnCells)
    {
        // Skip the first couple of path cells so the seam into this band stays clear, and the
        // very last cell (the seam into the next band).
        for (int i = 2; i < path.Count - 1; i++)
        {
            var (r, c) = path[i];
            if (startRow + r == 0) continue; // keep spawn row clear

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

            // Every once in a while, carve a small local "step around" loop that lets the player
            // dodge THIS specific hazard, instead of turning the whole maze into a branching one.
            // This keeps the corridor-with-turns look intact everywhere else.
            if (rng.NextDouble() < settings.hazardBypassChance)
            {
                TryCarveBypass(band, numRows, bandColumns, path, i, spawnCells, rng);
            }
        }
    }

    // Hazard placement for wide CHOICE bands. Unlike PlaceObstacles (which walks the single
    // collapsed corridor), this scatters hazards across every branch of the kept-intact spanning
    // tree, at reduced density (wideBandHazardChanceMultiplier) since a dense branching layout
    // full of hazards gets unfair/unreadable fast. No bypass-detour carving here - the branching
    // structure itself already gives the player alternate routes around anything they'd rather
    // avoid, which is the whole point of a choice band. Fans are intentionally skipped: their
    // direction/span logic (PickFanDirection/PickFanSpan) assumes a single linear path with a
    // clear "next step", which off-path branch cells don't have.
    private void PlaceObstaclesWideBand(int startRow, MazeCell[][] band, System.Random rng,
        HashSet<(int, int)> spawnCells, List<(int r, int c)> mainPath)
    {
        var entry = mainPath[0];
        var exit = mainPath[mainPath.Count - 1];
        double steamChance = settings.steamChance * settings.wideBandHazardChanceMultiplier;
        double wireChance = settings.wireChance * settings.wideBandHazardChanceMultiplier;

        foreach (var (r, c) in spawnCells)
        {
            if (startRow + r == 0) continue;             // keep spawn row clear
            if (r == entry.r && c == entry.c) continue;   // keep the entry cell clear
            if (r == exit.r && c == exit.c) continue;     // keep the exit cell clear

            double roll = rng.NextDouble();
            if (roll < steamChance) band[r][c].obstacle = ObstacleType.Steam;
            else if (roll < steamChance + wireChance) band[r][c].obstacle = ObstacleType.Wire;
        }
    }

    // A fan blowing along the vent's own axis makes physical sense (mounted at the end of a
    // straight shaft, blowing up or down it) - unlike blowing "into" a solid side wall. So fans
    // pick a direction along whichever axis the player is actually traveling through this cell,
    // randomly either WITH the player (a helpful boost) or AGAINST them (resistance to fight
    // through). It's safe for a fan to oppose the player head-on because fanForce is kept below
    // playerSpeed in GameSettings - the player's net forward speed in the worst case is
    // (playerSpeed - fanForce), which is always positive, so an opposing fan slows you down hard
    // but can never fully pin you in place.
    // Walks outward from path index i along a fixed direction (dirR, dirC) on each side, counting
    // how many consecutive cells the corridor keeps going dead straight before it turns or runs
    // out of path. Shared by the bypass-detour carving and the fan wind-tunnel sizing, since both
    // need to know "how much straight corridor is safely available here" before extending anything.
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

    // Picks how many cells long this fan's wind tunnel stretches, along the SAME axis it blows on
    // (so the tunnel and the push direction always agree), clamped to however much straight
    // corridor is actually available around it. A span of 1 = the original single-cell fan.
    private int PickFanSpan(List<(int r, int c)> path, int i, FanDirection dir, System.Random rng)
    {
        bool axisIsHorizontal = dir == FanDirection.East || dir == FanDirection.West;
        int dirR = axisIsHorizontal ? 0 : 1;
        int dirC = axisIsHorizontal ? 1 : 0;
        var (maxBack, maxFwd) = ComputeStraightRun(path, i, dirR, dirC);
        int maxAvailable = 1 + maxBack + maxFwd; // total straight cells the tunnel could span

        int lo = Mathf.Max(1, settings.fanTunnelLengthCellsRange.x);
        int hi = Mathf.Max(lo, settings.fanTunnelLengthCellsRange.y);
        int desired = rng.Next(lo, hi + 1);
        return Mathf.Min(desired, maxAvailable);
    }

    // Carves a minimal alternate route around B (path[i]) that avoids it, using only cells that
    // are still completely untouched (all four walls intact - guaranteed not to be part of the
    // main corridor or a previous bypass). Returns true if a bypass was actually carved.
    //
    // - If the path runs straight through B (same direction in and out), the detour is a lane one
    //   row/column to the side of B, covering B plus - randomly, per bypass - a few extra cells of
    //   genuinely straight corridor on either side (settings.bypassDetourExtensionRange). This is
    //   what lets some dodges read as a short 3-cell bubble and others as a longer sweeping branch,
    //   without ever distorting a turn, since it only extends along confirmed-straight stretches.
    // - If the path turns at B (a corner), the detour stays a single-cell "corner cut" through the
    //   cell diagonal to B - a tiny fork right at the turn.
    private bool TryCarveBypass(MazeCell[][] band, int numRows, int bandColumns,
        List<(int r, int c)> path, int i, HashSet<(int, int)> spawnCells, System.Random rng)
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
            // Straight through B. Before offsetting, see how much further the corridor keeps
            // going in this exact same direction on each side - that's the most this detour is
            // ever allowed to extend, so it can never eat into a turn.
            var (maxBack, maxFwd) = ComputeStraightRun(path, i, inR, inC);

            int extLo = Mathf.Max(0, settings.bypassDetourExtensionRange.x);
            int extHi = Mathf.Max(extLo, settings.bypassDetourExtensionRange.y);
            int extraBack = Mathf.Min(maxBack, rng.Next(extLo, extHi + 1));
            int extraFwd = Mathf.Min(maxFwd, rng.Next(extLo, extHi + 1));

            int startIdx = i - 1 - extraBack;
            int endIdx = i + 1 + extraFwd;

            int perpR = inC != 0 ? 1 : 0; // if moving horizontally, offset vertically, and vice versa
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
                foreach (var cell in lane) spawnCells.Add(cell);
                return true;
            }
            return false;
        }
        else
        {
            // Turn at B - corner cut through the cell diagonal from B.
            var d = (A.r + outR, A.c + outC);
            if (d != B && IsFree(d))
            {
                OpenBetween(band, A.r, A.c, d.Item1, d.Item2);
                OpenBetween(band, d.Item1, d.Item2, C.r, C.c);
                spawnCells.Add(d);
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

    // Places a decorative room prefab in the black dead-space just outside the maze's own
    // columns, to the left or right at random. This space is already visible because the camera
    // is pulled back wider than the maze (cameraZoomOutMultiplier), so the room simply fills in
    // what would otherwise be empty black background - it never overlaps the corridor or any
    // wall/obstacle geometry, and has no collider, so the player can never touch it and the vent
    // effectively "goes around" it automatically.
    private void TrySpawnBackgroundRoom(int startRow, int numRows, float xOffset, HashSet<(int, int)> spawnCells, System.Random rng)
    {
        bandsSinceRoom++;
        if (roomPrefabs == null || roomPrefabs.Length == 0) return;
        if (bandsSinceRoom <= settings.minBandsBetweenRooms) return;
        if (rng.NextDouble() >= settings.roomSpawnChance) return;

        // Pick a random row within this band, then find the actual corridor cell(s) on that row -
        // NOT the outer edge of the whole band, which is often mostly dead space since the
        // corridor is a single winding path and rarely spans the full band width on any given
        // row. Anchoring to the band edge is what caused rooms to float far from the vent.
        int localRow = rng.Next(0, numRows);
        int minCol = int.MaxValue, maxCol = int.MinValue;
        foreach (var cell in spawnCells)
        {
            if (cell.Item1 != localRow) continue;
            if (cell.Item2 < minCol) minCol = cell.Item2;
            if (cell.Item2 > maxCol) maxCol = cell.Item2;
        }
        if (maxCol < minCol) return; // no corridor cell on this row - skip this band's room roll

        bandsSinceRoom = 0;

        int worldRow = startRow + localRow;
        float y = worldRow * CellSize + CellSize * 0.5f;

        bool onRight = rng.NextDouble() < 0.5;
        // Use the real wall edge of the actual rightmost/leftmost corridor cell on this row.
        float mazeEdgeX = onRight ? xOffset + (maxCol + 1) * CellSize : xOffset + minCol * CellSize;

        // Spawn first at a placeholder X so we can measure the prefab's actual rendered width
        // (different room images will be different sizes/aspect ratios) and butt its NEAR edge -
        // not its center pivot - up against the wall plus the configured gap. Positioning by
        // center pivot alone was the bug: a room wider than roomSideOffset would dip back behind
        // the wall column instead of sitting cleanly beside it.
        var prefab = roomPrefabs[rng.Next(roomPrefabs.Length)];
        var go = Instantiate(prefab, new Vector3(mazeEdgeX, y, 0f), Quaternion.identity, transform);

        // Mirror the sprite when placed on the left so asymmetric room art (e.g. a fireplace
        // rendered opening toward the right) still faces back in toward the vent on both sides.
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

        // Piggyback on the same per-row cleanup as walls/obstacles, so old rooms get destroyed
        // once the flood has risen well past them.
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