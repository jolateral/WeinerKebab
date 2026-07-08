using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("References")]
    public MazeLibrary mazeLibrary;
    public Transform player;

    [Header("Optional guaranteed easy start")]
    public GameObject firstMazePrefab;

    [Header("Layout")]
    public float sideBySideSpacing = 6f;
    [Range(0f, 1f)]
    public float doubleMazeChance = 0.5f;

    [Header("Lookahead")]
    [Tooltip("How many maze layers to pre-spawn above the current one at start")]
    public int preSpawnAhead = 2;

    [Header("Difficulty Scaling")]
    public AnimationCurve easyWeightOverProgress   = AnimationCurve.Linear(0f, 1f,    1f, 0.1f);
    public AnimationCurve mediumWeightOverProgress = AnimationCurve.Linear(0f, 0.3f,  1f, 0.5f);
    public AnimationCurve hardWeightOverProgress   = AnimationCurve.Linear(0f, 0.05f, 1f, 0.6f);
    public float progressNormalizationScore = 20f;

    [Header("Cleanup")]
    public float despawnDistanceBehind = 80f;

    // ── State ─────────────────────────────────────────────────────
    private float   currentExitY          = 0f;
    private float[] currentExitXPositions = new float[] { 0f };

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private bool isFirstSpawn = true;

    private void Awake() => Instance = this;

    private void Start()
    {
        // 1. Spawn the first maze — this sets currentExitY and currentExitXPositions
        GameObject startPrefab = firstMazePrefab != null
            ? firstMazePrefab
            : PickWeightedMaze();
        SpawnMazeAt(startPrefab, 0f, 0f);

        // 2. Pre-spawn layers above so player never sees mazes pop in
        // State is correct at this point so these chain correctly
        for (int i = 0; i < preSpawnAhead; i++)
            SpawnNextLayer();
    }

    private void Update() => CleanupOldObjects();

    // Called by MazeExitMarker when player reaches any maze exit
    public void OnPlayerReachedExit()
    {
        if (GameManager.Instance.isGameOver) return;
        GameManager.Instance.AddScore(1);
        // Spawn exactly ONE new layer ahead to keep the lookahead buffer filled
        SpawnNextLayer();
    }

    // ── Layer spawning ────────────────────────────────────────────

    private void SpawnNextLayer()
    {
        bool doDouble = Random.value < doubleMazeChance;
        if (doDouble)
            SpawnDoubleLayer();
        else
            SpawnSingleLayer();
    }

    private void SpawnSingleLayer()
    {
        float[] toPositions = new float[] { 0f };

        GameObject connector = ProceduralConnector.Build(
            currentExitXPositions,
            toPositions,
            currentExitY,
            out float connectorTop);
        spawnedObjects.Add(connector);

        float mazeExitY = SpawnMazeAt(PickWeightedMaze(), 0f, connectorTop);

        currentExitY          = mazeExitY;
        currentExitXPositions = toPositions;
    }

    private void SpawnDoubleLayer()
    {
        float leftX  = -sideBySideSpacing * 0.5f;
        float rightX =  sideBySideSpacing * 0.5f;
        float[] toPositions = new float[] { leftX, rightX };

        GameObject connector = ProceduralConnector.Build(
            currentExitXPositions,
            toPositions,
            currentExitY,
            out float connectorTop);
        spawnedObjects.Add(connector);

        float exitAY = SpawnMazeAt(PickWeightedMaze(), leftX,  connectorTop);
        float exitBY = SpawnMazeAt(PickWeightedMaze(), rightX, connectorTop);

        currentExitY          = Mathf.Max(exitAY, exitBY);
        currentExitXPositions = toPositions;
    }

    // Spawns one maze with its entryPoint at (centerX, fromY).
    // Returns the world Y of this maze's exit.
    private float SpawnMazeAt(GameObject prefab, float centerX, float fromY)
    {
        if (prefab == null) return fromY;

        GameObject instance = Instantiate(prefab);
        MazeSegment seg = instance.GetComponent<MazeSegment>();

        if (seg == null || seg.entryPoint == null || seg.exitPoint == null)
        {
            Debug.LogError("Maze prefab missing MazeSegment or entry/exit: " + prefab.name);
            Destroy(instance);
            return fromY;
        }

        Vector3 entryLocal = seg.entryPoint.localPosition;
        instance.transform.position = new Vector3(
            centerX - entryLocal.x,
            fromY   - entryLocal.y,
            0f);

        // Ensure pipes always render behind the player
        foreach (SpriteRenderer sr in instance.GetComponentsInChildren<SpriteRenderer>())
            sr.sortingOrder = 0;

        spawnedObjects.Add(instance);

        // First maze only: snap player to entry and record initial state
        if (isFirstSpawn && player != null)
        {
            player.position       = seg.entryPoint.position;
            currentExitY          = seg.exitPoint.position.y;
            currentExitXPositions = new float[] { centerX };
            isFirstSpawn          = false;
        }

        return seg.exitPoint.position.y;
    }

    // ── Difficulty helpers ────────────────────────────────────────

    private GameObject PickWeightedMaze()
    {
        float t = Mathf.Clamp01(GameManager.Instance.score / progressNormalizationScore);
        float wE = easyWeightOverProgress.Evaluate(t);
        float wM = mediumWeightOverProgress.Evaluate(t);
        float wH = hardWeightOverProgress.Evaluate(t);

        Difficulty chosen = WeightedPickDifficulty(wE, wM, wH);
        List<GameObject> pool = mazeLibrary.mazes
            .Where(m => m.difficulty == chosen)
            .Select(m => m.mazePrefab)
            .ToList();

        if (pool.Count == 0)
            pool = mazeLibrary.mazes.Select(m => m.mazePrefab).ToList();

        return pool[Random.Range(0, pool.Count)];
    }

    private Difficulty WeightedPickDifficulty(float wE, float wM, float wH)
    {
        float total = wE + wM + wH;
        if (total <= 0f) return Difficulty.Easy;
        float roll = Random.value * total;
        if (roll < wE) return Difficulty.Easy;
        roll -= wE;
        if (roll < wM) return Difficulty.Medium;
        return Difficulty.Hard;
    }

    // ── Cleanup ───────────────────────────────────────────────────

    private void CleanupOldObjects()
    {
        if (player == null) return;
        float threshold = player.position.y - despawnDistanceBehind;
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = spawnedObjects[i];
            if (obj == null) { spawnedObjects.RemoveAt(i); continue; }
            if (obj.transform.position.y < threshold)
            {
                Destroy(obj);
                spawnedObjects.RemoveAt(i);
            }
        }
    }
}