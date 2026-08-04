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
    private List<MazeCell[]> rows = new List<MazeCell[]>();
    private int generatedUpToRow = -1;
    private int lastExitCol;

    // Track spawned GameObjects per row so we can clean up rows far below the flood.
    private Dictionary<int, List<GameObject>> spawnedByRow = new Dictionary<int, List<GameObject>>();

    public float CellSize => settings.cellSize;
    public int Columns => settings.columns;

    void Awake()
    {
        lastExitCol = Columns / 2;
        // Generate a few bands immediately so the player has somewhere to stand at start.
        GenerateBand(0, settings.rowsPerBand, lastExitCol, isFirstBand: true);
        for (int i = 0; i < settings.bandsAheadBuffer; i++)
        {
            lastExitCol = GenerateBand(generatedUpToRow + 1, settings.rowsPerBand, lastExitCol);
        }
    }

    // Call this every frame (or periodically) from GameManager, passing the current flood height.
    public void EnsureGeneratedAhead(float floodWorldY)
    {
        int floodRow = Mathf.FloorToInt(floodWorldY / CellSize);
        int neededRow = floodRow + settings.rowsPerBand * settings.bandsAheadBuffer;
        while (generatedUpToRow < neededRow)
        {
            lastExitCol = GenerateBand(generatedUpToRow + 1, settings.rowsPerBand, lastExitCol);
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
        foreach (var k in toRemove) spawnedByRow.Remove(k);
    }

    public Vector3 CellWorldCenter(int row, int col)
    {
        return new Vector3(col * CellSize + CellSize * 0.5f, row * CellSize + CellSize * 0.5f, 0f);
    }

    public MazeCell CellAt(int row, int col)
    {
        if (row < 0 || row >= rows.Count) return null;
        if (col < 0 || col >= Columns) return null;
        return rows[row][col];
    }

    // Recursive backtracker over a band of rows, entering from entryCol on the band's bottom edge.
    private int GenerateBand(int startRow, int numRows, int entryCol, bool isFirstBand = false)
    {
        EnsureRowCapacity(startRow + numRows - 1);

        MazeCell[][] band = new MazeCell[numRows][];
        for (int r = 0; r < numRows; r++)
        {
            band[r] = new MazeCell[Columns];
            for (int c = 0; c < Columns; c++) band[r][c] = new MazeCell();
            rows[startRow + r] = band[r];
        }

        var stack = new Stack<(int r, int c)>();
        stack.Push((0, entryCol));
        band[0][entryCol].visited = true;

        // Open connection down to the previous band (or leave closed if this is the very first band).
        band[0][entryCol].wallS = false;
        if (!isFirstBand && startRow > 0 && rows[startRow - 1] != null)
        {
            rows[startRow - 1][entryCol].wallN = false;
        }

        System.Random rng = new System.Random();

        while (stack.Count > 0)
        {
            var (r, c) = stack.Peek();
            var options = new List<(int nr, int nc, string wallHere, string wallThere)>();

            if (r + 1 < numRows && !band[r + 1][c].visited) options.Add((r + 1, c, "N", "S"));
            if (r - 1 >= 0 && !band[r - 1][c].visited) options.Add((r - 1, c, "S", "N"));
            if (c + 1 < Columns && !band[r][c + 1].visited) options.Add((r, c + 1, "E", "W"));
            if (c - 1 >= 0 && !band[r][c - 1].visited) options.Add((r, c - 1, "W", "E"));

            if (options.Count == 0) { stack.Pop(); continue; }

            var pick = options[rng.Next(options.Count)];
            SetWall(band[r][c], pick.wallHere, false);
            SetWall(band[pick.nr][pick.nc], pick.wallThere, false);
            band[pick.nr][pick.nc].visited = true;
            stack.Push((pick.nr, pick.nc));
        }

        int exitCol = rng.Next(Columns);
        generatedUpToRow = startRow + numRows - 1;

        PlaceObstacles(startRow, numRows, band, rng);
        SpawnBandGeometry(startRow, numRows, band);

        return exitCol;
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

    private void PlaceObstacles(int startRow, int numRows, MazeCell[][] band, System.Random rng)
    {
        for (int r = 0; r < numRows; r++)
        {
            if (startRow + r == 0) continue; // keep spawn row clear
            if (r < 1) continue;             // keep the row right at the band seam clear-ish

            for (int c = 0; c < Columns; c++)
            {
                double roll = rng.NextDouble();
                if (roll < settings.steamChance)
                {
                    band[r][c].obstacle = ObstacleType.Steam;
                }
                else if (roll < settings.steamChance + settings.wireChance)
                {
                    band[r][c].obstacle = ObstacleType.Wire;
                }
                else if (roll < settings.steamChance + settings.wireChance + settings.fanChance)
                {
                    band[r][c].obstacle = ObstacleType.Fan;
                    band[r][c].fanDir = (FanDirection)rng.Next(4);
                }
            }
        }
    }

    private void SpawnBandGeometry(int startRow, int numRows, MazeCell[][] band)
    {
        for (int r = 0; r < numRows; r++)
        {
            int worldRow = startRow + r;
            if (!spawnedByRow.ContainsKey(worldRow)) spawnedByRow[worldRow] = new List<GameObject>();

            for (int c = 0; c < Columns; c++)
            {
                MazeCell cell = band[r][c];
                Vector3 center = CellWorldCenter(worldRow, c);

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
    }

    private void SpawnWallIfNeeded(bool present, Vector3 cellCenter, Vector3 localOffset, bool horizontal, int worldRow)
    {
        if (!present || wallSegmentPrefab == null) return;
        Vector3 pos = cellCenter + localOffset;
        Quaternion rot = horizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90);
        var go = Instantiate(wallSegmentPrefab, pos, rot, transform);
        // Scale the wall's length to the cell size (assumes prefab is authored at 1 unit length on X).
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
            if (fan != null) fan.SetDirection(cell.fanDir);
        }
        spawnedByRow[worldRow].Add(go);
    }
}
