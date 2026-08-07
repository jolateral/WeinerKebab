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

    // rows[r][c] = cell data. Row 0 is the starting row at the bottom.
    // NOTE: each row's array length can differ band-to-band (wide reveal bands are wider), so
    // always go through CellWorldCenter / rowLayout rather than assuming a fixed column count.
    private List<MazeCell[]> rows = new List<MazeCell[]>();
    private int generatedUpToRow = -1;
    private float lastExitWorldX;
    private int bandsSinceWide = 999; // large so the very first eligible band CAN roll wide

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
            bandColumns = Mathf.Max(settings.columns, Mathf.RoundToInt(settings.columns * settings.wideBandColumnMultiplier));
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

        var stack = new Stack<(int r, int c)>();
        stack.Push((0, entryCol));
        band[0][entryCol].visited = true;

        // Roll this band's "personality" once, up front, rather than reading a fixed value from
        // settings. This is what breaks the uniform feel - one band might commit hard to one long
        // horizontal sweep (high bias, long min run), the next might be scrappier and twistier
        // (lower bias, short min run), so the player can't predict what's coming a band ahead.
        float bandHorizontalBias = (float)(settings.horizontalCarveBiasRange.x +
            rng.NextDouble() * (settings.horizontalCarveBiasRange.y - settings.horizontalCarveBiasRange.x));
        if (isWideBand) bandHorizontalBias = Mathf.Clamp01(bandHorizontalBias + 0.1f);
        float bandStaircaseChance = (float)(settings.midRunStaircaseChanceRange.x +
            rng.NextDouble() * (settings.midRunStaircaseChanceRange.y - settings.midRunStaircaseChanceRange.x));

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

            if (r + 1 < numRows && !band[r + 1][c].visited) options.Add((r + 1, c, "N", "S"));
            if (r - 1 >= 0 && !band[r - 1][c].visited) options.Add((r - 1, c, "S", "N"));
            if (c + 1 < bandColumns && !band[r][c + 1].visited) options.Add((r, c + 1, "E", "W"));
            if (c - 1 >= 0 && !band[r][c - 1].visited) options.Add((r, c - 1, "W", "E"));

            if (options.Count == 0) { stack.Pop(); continue; }

            int target = runTarget.TryGetValue((r, c), out var t) ? t : settings.minHorizontalRunCellsRange.x;
            string lastHoriz = lastHorizontalDir.TryGetValue((r, c), out var lh) ? lh : null;
            int vTarget = verticalRunTarget.TryGetValue((r, c), out var vt) ? vt : settings.verticalRunCellsRange.x;

            var pick = MomentumPick(options, rng, incomingDir[(r, c)], runLength[(r, c)], target, vTarget, lastHoriz, bandHorizontalBias, bandStaircaseChance);
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
                    : rng.Next(settings.minHorizontalRunCellsRange.x, settings.minHorizontalRunCellsRange.y + 1);
            }
            else
            {
                if (lastHorizontalDir.ContainsKey((r, c))) lastHorizontalDir[(pick.nr, pick.nc)] = lastHorizontalDir[(r, c)];
                if (runTarget.ContainsKey((r, c))) runTarget[(pick.nr, pick.nc)] = runTarget[(r, c)];

                verticalRunTarget[(pick.nr, pick.nc)] = (pick.wallHere == incomingDir[(r, c)] && verticalRunTarget.ContainsKey((r, c)))
                    ? verticalRunTarget[(r, c)]
                    : rng.Next(settings.verticalRunCellsRange.x, settings.verticalRunCellsRange.y + 1);
            }

            stack.Push((pick.nr, pick.nc));
        }

        int exitCol = rng.Next(bandColumns);
        generatedUpToRow = startRow + numRows - 1;

        List<(int r, int c)> path = ExtractSinglePath(band, numRows, bandColumns, entryCol);
        CollapseToSinglePath(band, numRows, bandColumns, path);
        exitCol = path[path.Count - 1].c;

        // spawnCells starts as just the single corridor - this is what keeps the maze reading
        // exactly like it used to. PlaceObstacles below adds a handful of small "step around"
        // detour cells to this set, one per bypassed hazard, so the vast majority of the layout
        // is still one clean path with only the occasional local loop around an obstacle.
        var spawnCells = new HashSet<(int, int)>(path);

        PlaceObstacles(startRow, numRows, band, bandColumns, rng, path, spawnCells);
        SpawnBandGeometry(startRow, spawnCells, band, xOffset);

        float exitWorldX = xOffset + exitCol * settings.cellSize + settings.cellSize * 0.5f;
        return exitWorldX;
    }

    // Same BFS-over-the-spanning-tree extraction as the original generator: since the carve above
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

    private (int nr, int nc, string wallHere, string wallThere) MomentumPick(
        List<(int nr, int nc, string wallHere, string wallThere)> options, System.Random rng,
        string enteredDir, int runLen, int horizontalTarget, int verticalTarget, string lastHorizontalDir,
        float horizontalBias, float staircaseChance)
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
            if (resumeOption.wallHere != null && rng.NextDouble() < 0.85) return resumeOption;
        }

        if (horizontalOptions.Count > 0 && rng.NextDouble() < horizontalBias)
        {
            return horizontalOptions[rng.Next(horizontalOptions.Count)];
        }

        return options[rng.Next(options.Count)];
    }
}