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
    [Tooltip("Horizontal distance between the centers of the two side-by-side mazes")]
    public float sideBySideSpacing = 6f;
    [Tooltip("0 = always one maze, 1 = always two, 0.5 = 50/50")]
    [Range(0f, 1f)]
    public float doubleMazeChance = 0.5f;

    [Header("Prefabs")]
    [Tooltip("The horizontal connector piece prefab used when two mazes spawn side by side")]
    public GameObject connectorPiecePrefab;

    [Header("Difficulty Scaling")]
    public AnimationCurve easyWeightOverProgress = AnimationCurve.Linear(0f, 1f, 1f, 0.1f);
    public AnimationCurve mediumWeightOverProgress = AnimationCurve.Linear(0f, 0.3f, 1f, 0.5f);
    public AnimationCurve hardWeightOverProgress = AnimationCurve.Linear(0f, 0.05f, 1f, 0.6f);
    public float progressNormalizationScore = 20f;

    [Header("Cleanup")]
    public float despawnDistanceBehind = 25f;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private float currentExitY = 0f;
    private bool isFirstSpawn = true;

    private void Awake() => Instance = this;

    private void Start()
    {
        if (firstMazePrefab != null)
            SpawnSingle(firstMazePrefab, 0f);
        else
            SpawnNextLevel(0f);
    }

    private void Update() => CleanupOldObjects();

public void OnPlayerReachedExit()
{
    Debug.Log("OnPlayerReachedExit called. Score: " + GameManager.Instance.score);
    if (GameManager.Instance.isGameOver) return;
    GameManager.Instance.AddScore(1);

    // Read currentExitY fresh before spawning anything new
    Debug.Log("Spawning next level from Y: " + currentExitY);
    SpawnNextLevel(currentExitY);
}

    private void SpawnNextLevel(float fromY)
    {
        bool doDouble = Random.value < doubleMazeChance;
        if (doDouble)
            SpawnDouble(fromY);
        else
            SpawnSingle(PickWeightedMaze(), fromY);
    }

    // ── Single maze ──────────────────────────────────────────────
private void SpawnSingle(GameObject prefab, float fromY)
{
    Debug.Log("SpawnSingle called at Y: " + fromY);
    
    if (prefab == null)
    {
        Debug.LogError("SpawnSingle: prefab is NULL!");
        return;
    }

    GameObject instance = Instantiate(prefab);
    Debug.Log("Instantiated: " + instance.name + " at " + instance.transform.position);

    MazeSegment seg = instance.GetComponent<MazeSegment>();
    if (seg == null)
    {
        Debug.LogError("SpawnSingle: MazeSegment component missing on " + instance.name);
        return;
    }
    if (seg.entryPoint == null)
    {
        Debug.LogError("SpawnSingle: entryPoint is not assigned on " + instance.name);
        return;
    }
    if (seg.exitPoint == null)
    {
        Debug.LogError("SpawnSingle: exitPoint is not assigned on " + instance.name);
        return;
    }

    float offsetX = -seg.entryPoint.position.x + instance.transform.position.x;
    float offsetY = fromY - seg.entryPoint.position.y;
    instance.transform.position = new Vector3(offsetX, offsetY, 0);
    Debug.Log("Maze repositioned to: " + instance.transform.position);

    spawnedObjects.Add(instance);
    currentExitY = seg.exitPoint.position.y;
    Debug.Log("currentExitY set to: " + currentExitY);

    if (isFirstSpawn && player != null)
    {
        player.position = seg.entryPoint.position;
        isFirstSpawn = false;
    }
}

    // ── Double maze ───────────────────────────────────────────────
private void SpawnDouble(float fromY)
{
    Debug.Log("SpawnDouble called at Y: " + fromY);

    if (connectorPiecePrefab == null)
    {
        Debug.LogError("ConnectorPiece prefab not assigned! Falling back to single.");
        SpawnSingle(PickWeightedMaze(), fromY);
        return;
    }

    // 1. Spawn connector centered at fromY
    GameObject connector = Instantiate(connectorPiecePrefab);
    ConnectorPiece cp = connector.GetComponent<ConnectorPiece>();

    if (cp == null)
    {
        Debug.LogError("ConnectorPiece component missing from prefab!");
        Destroy(connector);
        SpawnSingle(PickWeightedMaze(), fromY);
        return;
    }
    if (cp.centerEntry == null || cp.leftExit == null || cp.rightExit == null)
    {
        Debug.LogError("ConnectorPiece is missing centerEntry, leftExit, or rightExit references!");
        Destroy(connector);
        SpawnSingle(PickWeightedMaze(), fromY);
        return;
    }

    // Position connector so centerEntry aligns with fromY
    // We need to do this BEFORE reading leftExit/rightExit world positions
    float connOffsetY = fromY - cp.centerEntry.localPosition.y;
    connector.transform.position = new Vector3(0f, connOffsetY, 0f);
    Debug.Log("Connector placed at: " + connector.transform.position
        + " centerEntry world Y: " + cp.centerEntry.position.y
        + " leftExit world Y: " + cp.leftExit.position.y);

    spawnedObjects.Add(connector);

    // 2. Spawn left maze — read exit positions AFTER connector is repositioned
    GameObject prefabA = PickWeightedMaze();
    GameObject mazeA = Instantiate(prefabA);
    MazeSegment segA = mazeA.GetComponent<MazeSegment>();

    if (segA == null || segA.entryPoint == null)
    {
        Debug.LogError("Left maze missing MazeSegment or entryPoint!");
        Destroy(mazeA);
    }
    else
    {
        Vector3 leftExitWorld = cp.leftExit.position;
        mazeA.transform.position = new Vector3(
            leftExitWorld.x - segA.entryPoint.localPosition.x,
            leftExitWorld.y - segA.entryPoint.localPosition.y,
            0f);
        Debug.Log("MazeA placed at: " + mazeA.transform.position);
        spawnedObjects.Add(mazeA);
    }

    // 3. Spawn right maze
    GameObject prefabB = PickWeightedMaze();
    GameObject mazeB = Instantiate(prefabB);
    MazeSegment segB = mazeB.GetComponent<MazeSegment>();

    if (segB == null || segB.entryPoint == null)
    {
        Debug.LogError("Right maze missing MazeSegment or entryPoint!");
        Destroy(mazeB);
    }
    else
    {
        Vector3 rightExitWorld = cp.rightExit.position;
        mazeB.transform.position = new Vector3(
            rightExitWorld.x - segB.entryPoint.localPosition.x,
            rightExitWorld.y - segB.entryPoint.localPosition.y,
            0f);
        Debug.Log("MazeB placed at: " + mazeB.transform.position);
        spawnedObjects.Add(mazeB);
    }

    // Exit Y is the top of whichever maze is taller
    float exitA = segA?.exitPoint?.position.y ?? fromY;
    float exitB = segB?.exitPoint?.position.y ?? fromY;
    currentExitY = Mathf.Max(exitA, exitB);
    Debug.Log("Double spawn complete. currentExitY: " + currentExitY);

    if (isFirstSpawn && player != null)
    {
        player.position = cp.centerEntry.position;
        isFirstSpawn = false;
    }
}

    // ── Helpers ───────────────────────────────────────────────────
    private GameObject PickWeightedMaze()
    {
        float t = Mathf.Clamp01(GameManager.Instance.score / progressNormalizationScore);
        float wEasy = easyWeightOverProgress.Evaluate(t);
        float wMedium = mediumWeightOverProgress.Evaluate(t);
        float wHard = hardWeightOverProgress.Evaluate(t);

        Difficulty chosen = WeightedPickDifficulty(wEasy, wMedium, wHard);
        List<GameObject> candidates = mazeLibrary.mazes
            .Where(m => m.difficulty == chosen)
            .Select(m => m.mazePrefab).ToList();

        if (candidates.Count == 0)
            candidates = mazeLibrary.mazes.Select(m => m.mazePrefab).ToList();

        return candidates[Random.Range(0, candidates.Count)];
    }

    private Difficulty WeightedPickDifficulty(float wEasy, float wMedium, float wHard)
    {
        float total = wEasy + wMedium + wHard;
        if (total <= 0f) return Difficulty.Easy;
        float roll = Random.value * total;
        if (roll < wEasy) return Difficulty.Easy;
        roll -= wEasy;
        if (roll < wMedium) return Difficulty.Medium;
        return Difficulty.Hard;
    }

private void CleanupOldObjects()
{
    if (player == null) return;
    for (int i = spawnedObjects.Count - 1; i >= 0; i--)
    {
        GameObject obj = spawnedObjects[i];
        if (obj == null) { spawnedObjects.RemoveAt(i); continue; }
        if (obj.transform.position.y < player.position.y - despawnDistanceBehind)
        {
            Debug.LogWarning("Despawning: " + obj.name + " at Y:" + obj.transform.position.y + 
                " player Y:" + player.position.y + 
                " threshold:" + (player.position.y - despawnDistanceBehind));
            Destroy(obj);
            spawnedObjects.RemoveAt(i);
        }
    }
}
}