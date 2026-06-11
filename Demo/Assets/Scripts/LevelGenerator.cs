using UnityEngine;
using System.Collections.Generic;

public class LevelGenerator : MonoBehaviour
{
    [Header("References")]
    public CameraScroller cameraScroller;
    public GameObject platformPrefab;
    public GameObject fanPrefab;
    public GameObject wirePrefab;
    public GameObject steamPipePrefab;
    public GameObject chutePrefab;

    [Header("Level Layout")]
    public float floorHeight = 3.5f;
    public float levelWidth = 9.5f;
    public float platformThickness = 0.4f;
    public int initialFloorsToGenerate = 10;

    [Header("Opening Settings")]
    [Tooltip("Smallest an opening gap can be")]
    public float minOpeningWidth = 1.8f;
    [Tooltip("Largest an opening gap can be")]
    public float maxOpeningWidth = 2.6f;
    [Tooltip("1 or 2 openings per floor")]
    public int openingsPerFloor = 2;
    [Tooltip("How far a new opening must be from any opening on the previous floor")]
    public float minOffsetFromLastFloor = 2.5f;

    [Header("Obstacle Chances (0-1)")]
    public float fanChance = 0.4f;
    public float wireChance = 0.3f;
    public float steamChance = 0.35f;
    public float chuteChance = 0.25f;

    private float nextFloorY = 0f;
    private int generatedFloorCount = 0;
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private int lastReportedFloor = 0;

    // Stores the center X of each opening on the most recently generated floor
    private List<float> previousFloorOpeningCenters = new List<float>();

    private void Start()
    {
        SpawnFullPlatform(0f);
        nextFloorY = floorHeight;

        for (int i = 0; i < initialFloorsToGenerate; i++)
            GenerateFloor();
    }

    private void Update()
    {
        if (GameManager.Instance.isGameOver) return;

        float topEdge = cameraScroller.GetTopEdge();
        while (nextFloorY < topEdge + floorHeight * 4)
            GenerateFloor();

        int currentFloor = Mathf.FloorToInt(cameraScroller.GetBottomEdge() / floorHeight);
        if (currentFloor > lastReportedFloor)
        {
            int diff = currentFloor - lastReportedFloor;
            for (int i = 0; i < diff; i++) GameManager.Instance.AddFloor();
            lastReportedFloor = currentFloor;
        }

        float cleanupY = cameraScroller.GetBottomEdge() - floorHeight * 2;
        spawnedObjects.RemoveAll(obj =>
        {
            if (obj != null && obj.transform.position.y < cleanupY)
            {
                Destroy(obj);
                return true;
            }
            return false;
        });
    }

    private void GenerateFloor()
{
    float y = nextFloorY;
    nextFloorY += floorHeight;
    generatedFloorCount++;

    bool spawnObstacles = generatedFloorCount > 3;
    float half = levelWidth / 2f;

    // STEP 1: Carve jump openings
    int numOpenings = Random.Range(1, openingsPerFloor + 1);
    List<(float start, float end)> openings = CarveOpenings(numOpenings, half);

    previousFloorOpeningCenters.Clear();
    foreach (var o in openings)
        previousFloorOpeningCenters.Add((o.start + o.end) / 2f);

    // STEP 2: Decide chute positions (separate from openings)
    // Chutes are also gaps in the platform, but they are NOT openings —
    // the player falls through them downward, not jumps up through them.
    List<(float start, float end)> chuteGaps = new List<(float start, float end)>();

    if (spawnObstacles && Random.value < chuteChance)
    {
        // Pick a chute position that doesn't overlap any opening
        float chuteWidth = 1.6f;
        float cx = TryGetNonOverlappingX(openings, chuteWidth, half);
        if (cx != float.MinValue)
        {
            float cStart = cx - chuteWidth / 2f;
            float cEnd   = cx + chuteWidth / 2f;
            chuteGaps.Add((cStart, cEnd));

            // Spawn the chute trigger object centred in the gap
            GameObject chute = Instantiate(chutePrefab,
                new Vector3(cx, y + platformThickness / 2f, 0), Quaternion.identity);
            // Tell the chute how wide the gap is so its collider matches
            chute.GetComponent<ChuteObstacle>().SetWidth(chuteWidth);
            spawnedObjects.Add(chute);
        }
    }

    // STEP 3: Build platforms — punch out BOTH openings AND chute gaps
    List<(float start, float end)> allGaps = new List<(float start, float end)>(openings);
    allGaps.AddRange(chuteGaps);

    List<(float start, float end)> platformSegments = BuildSegments(-half, half, allGaps);

    foreach (var seg in platformSegments)
    {
        float width = seg.end - seg.start;
        if (width < 0.5f) continue;
        float segCx = (seg.start + seg.end) / 2f;
        GameObject p = Instantiate(platformPrefab,
            new Vector3(segCx, y, 0), Quaternion.identity);
        p.transform.localScale = new Vector3(width, platformThickness, 1f);
        spawnedObjects.Add(p);
    }

    if (!spawnObstacles) return;

    // STEP 4: Obstacles on platform segments only
    float obstacleY = y + platformThickness / 2f + 0.3f;

if (Random.value < fanChance)
{
    float fx = GetSafeObstacleX(platformSegments);
    if (fx != float.MinValue)
    {
        GameObject fanObj = Instantiate(fanPrefab,
            new Vector3(fx, obstacleY, 0), Quaternion.identity);

        // Find the nearest opening to this fan and point toward it
        float nearestOpeningCenter = GetNearestOpeningCenter(fx, openings);
        float directionToOpening = nearestOpeningCenter < fx ? -1f : 1f;

        fanObj.GetComponent<FanObstacle>().SetDirection(directionToOpening);
        spawnedObjects.Add(fanObj);
    }
}

if (Random.value < steamChance)
{
    float sx = GetSafeObstacleX(platformSegments);
    if (sx != float.MinValue)
    {
        // Find which segment this X belongs to so we know its width
        float segWidth = GetSegmentWidthAtX(sx, platformSegments);
        GameObject steamObj = Instantiate(steamPipePrefab,
            new Vector3(sx, obstacleY, 0), Quaternion.identity);
        steamObj.GetComponent<SteamPipeObstacle>().SetWidth(segWidth);
        spawnedObjects.Add(steamObj);
    }
}
}

// Returns a centre X for a new gap that doesn't overlap existing openings
private float TryGetNonOverlappingX(
    List<(float start, float end)> openings, float width, float half)
{
    float margin = 0.5f;
    for (int i = 0; i < 15; i++)
    {
        float candidate = Random.Range(-half + width / 2f + margin,
                                        half - width / 2f - margin);
        float cStart = candidate - width / 2f;
        float cEnd   = candidate + width / 2f;

        bool overlaps = false;
        foreach (var o in openings)
        {
            if (cStart < o.end && cEnd > o.start) { overlaps = true; break; }
        }
        if (!overlaps) return candidate;
    }
    return float.MinValue;
}

    private List<(float start, float end)> CarveOpenings(int count, float half)
    {
        var openings = new List<(float start, float end)>();

        // Divide usable width into zones so openings are spread out
        float margin = 0.75f;
        float usableStart = -half + margin;
        float usableEnd = half - margin;
        float usableWidth = usableEnd - usableStart;
        float zoneWidth = usableWidth / count;

        for (int i = 0; i < count; i++)
        {
            float zoneLeft = usableStart + i * zoneWidth;
            float zoneRight = zoneLeft + zoneWidth;

            // Clamp opening width to inspector values
            float openingWidth = Random.Range(minOpeningWidth, maxOpeningWidth);

            float minCenter = zoneLeft + openingWidth / 2f + 0.1f;
            float maxCenter = zoneRight - openingWidth / 2f - 0.1f;

            if (minCenter > maxCenter)
            {
                // Zone too narrow to randomise, just centre it
                float center = (zoneLeft + zoneRight) / 2f;
                openings.Add((center - openingWidth / 2f, center + openingWidth / 2f));
                continue;
            }

            // Try to find a center that is offset from all previous floor openings
            float chosenCenter = TryFindOffsetCenter(minCenter, maxCenter, openingWidth);
            openings.Add((chosenCenter - openingWidth / 2f, chosenCenter + openingWidth / 2f));
        }

        return openings;
    }

    // Tries to place an opening center that is far enough from every opening
    // on the previous floor. Falls back to best available if nothing ideal is found.
    private float TryFindOffsetCenter(float minCenter, float maxCenter, float openingWidth)
    {
        if (previousFloorOpeningCenters.Count == 0)
            return Random.Range(minCenter, maxCenter);

        const int attempts = 20;
        float bestCenter = (minCenter + maxCenter) / 2f;
        float bestDistance = 0f;

        for (int i = 0; i < attempts; i++)
        {
            float candidate = Random.Range(minCenter, maxCenter);

            // Find the minimum distance from this candidate to any previous opening
            float minDist = float.MaxValue;
            foreach (float prev in previousFloorOpeningCenters)
                minDist = Mathf.Min(minDist, Mathf.Abs(candidate - prev));

            // If this candidate is far enough away, use it immediately
            if (minDist >= minOffsetFromLastFloor)
                return candidate;

            // Otherwise track the best we've found so far as a fallback
            if (minDist > bestDistance)
            {
                bestDistance = minDist;
                bestCenter = candidate;
            }
        }

        // Return best found even if it didn't fully clear the offset threshold
        return bestCenter;
    }

    private float GetSafeObstacleX(List<(float start, float end)> segments)
    {
        var shuffled = new List<(float start, float end)>(segments);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        float edgeBuffer = 0.6f;
        foreach (var seg in shuffled)
        {
            float safeStart = seg.start + edgeBuffer;
            float safeEnd = seg.end - edgeBuffer;
            if (safeEnd > safeStart)
                return Random.Range(safeStart, safeEnd);
        }

        return float.MinValue;
    }

    private void SpawnFullPlatform(float y)
    {
        GameObject p = Instantiate(platformPrefab, new Vector3(0, y, 0), Quaternion.identity);
        p.transform.localScale = new Vector3(levelWidth, platformThickness, 1f);
        spawnedObjects.Add(p);
    }

    private List<(float, float)> BuildSegments(float start, float end,
        List<(float start, float end)> gaps)
    {
        var segments = new List<(float, float)>();
        float cursor = start;

        gaps.Sort((a, b) => a.start.CompareTo(b.start));

        foreach (var gap in gaps)
        {
            if (gap.start > cursor)
                segments.Add((cursor, gap.start));
            cursor = gap.end;
        }

        if (cursor < end)
            segments.Add((cursor, end));

        return segments;
    }

    private float GetSegmentWidthAtX(float x, List<(float start, float end)> segments)
{
    foreach (var seg in segments)
    {
        if (x >= seg.start && x <= seg.end)
            return seg.end - seg.start;
    }
    return 3f; // fallback
}

private float GetNearestOpeningCenter(float fromX, List<(float start, float end)> openings)
{
    float nearest = 0f;
    float nearestDist = float.MaxValue;

    foreach (var o in openings)
    {
        float center = (o.start + o.end) / 2f;
        float dist = Mathf.Abs(center - fromX);
        if (dist < nearestDist)
        {
            nearestDist = dist;
            nearest = center;
        }
    }

    return nearest;
}
}