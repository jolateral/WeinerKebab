using UnityEngine;
using System.Collections.Generic;

// Attach to Enemy Cat prefab. Requires Collider2D (Is Trigger) and a Rigidbody2D (set Body Type
// to Kinematic). The Rigidbody2D is required for reliable OnTrigger callbacks against the bullet,
// since Bullet.cs moves itself via transform.Translate and has no Rigidbody2D of its own - Unity's
// 2D physics needs at least one of the two colliding objects to own a Rigidbody2D for trigger
// events to fire consistently.
//
// BEHAVIOR: this is a Pac-Man-ghost-style wanderer, NOT a hunter. It has no awareness of the
// player at all. At every junction it prefers to continue downward (toward the rising floor) and
// otherwise picks a random open direction, only backtracking the way it came when it hits a dead
// end. It never chases, blocks, or reacts to the player in any way - the player has to read its
// path and route around it (or shoot it).
public class EnemyCat : MonoBehaviour
{
    public float moveSpeed = 2.2f;

    [Range(0f, 1f)]
    [Tooltip("Chance, at a junction with a South (downward) opening available, that the cat takes it instead of a random alternative. Keep this high so cats read as generally flowing downward.")]
    public float downwardBias = 0.75f;

    private MazeGenerator mazeGen;
    private Vector3 targetCellCenter;
    private int currentGlobalRow;
    private int currentColumn;
    private int previousRow = int.MinValue;
    private int previousColumn = int.MinValue;
    private bool initialized = false;

    void Start()
    {
        mazeGen = FindFirstObjectByType<MazeGenerator>();
        if (mazeGen == null)
        {
            enabled = false;
            return;
        }

        // Use the maze's own layout-aware conversion instead of re-deriving row/col by hand - the
        // old hand-rolled math assumed every band was the default width, which breaks (silently
        // placing the cat's logical cell outside the real corridor) on any wide "choice" band.
        mazeGen.WorldToCell(transform.position, out currentGlobalRow, out currentColumn);

        targetCellCenter = mazeGen.CellWorldCenter(currentGlobalRow, currentColumn);
        initialized = true;
    }

    void Update()
    {
        if (!initialized || mazeGen == null) return;

        transform.position = Vector3.MoveTowards(transform.position, targetCellCenter, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetCellCenter) < 0.05f)
        {
            SelectNextCell();
        }
    }

    private void SelectNextCell()
    {
        MazeCell currentCell = mazeGen.CellAt(currentGlobalRow, currentColumn);
        if (currentCell == null) return;

        // Open directions as WORLD-SPACE offsets, not index deltas. Bands can differ in width and
        // x-offset (wide "choice" bands), so "column stays the same, row +/- 1" only holds within a
        // single band. Stepping by a real world-space cellSize and re-resolving the destination
        // through WorldToCell is immune to that - it's what actually fixed the void-fall at the
        // band boundary.
        float cs = mazeGen.CellSize;
        var openOffsets = new List<Vector3>();
        if (!currentCell.wallS) openOffsets.Add(new Vector3(0f, -cs, 0f));
        if (!currentCell.wallN) openOffsets.Add(new Vector3(0f, cs, 0f));
        if (!currentCell.wallE) openOffsets.Add(new Vector3(cs, 0f, 0f));
        if (!currentCell.wallW) openOffsets.Add(new Vector3(-cs, 0f, 0f));

        if (openOffsets.Count == 0) return;

        var candidates = new List<(int r, int c, bool isSouth)>();
        foreach (var offset in openOffsets)
        {
            Vector3 candidateWorldPos = targetCellCenter + offset;
            mazeGen.WorldToCell(candidateWorldPos, out int nr, out int nc);
            if (nr < 0) continue; // never step below the bottom of the maze
            candidates.Add((nr, nc, offset.y < 0f));
        }
        if (candidates.Count == 0) return;

        // Don't immediately backtrack unless it's the only option (avoids jittery back-and-forth
        // in plain corridors while still allowing it at genuine dead ends).
        var nonReverse = candidates.FindAll(cand => !(cand.r == previousRow && cand.c == previousColumn));
        var choices = nonReverse.Count > 0 ? nonReverse : candidates;

        int chosenIndex = -1;

        // Prefer flowing downward most of the time, purely based on the maze layout - never based
        // on where the player is.
        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i].isSouth && Random.value < downwardBias)
            {
                chosenIndex = i;
                break;
            }
        }
        if (chosenIndex == -1) chosenIndex = Random.Range(0, choices.Count);

        var chosen = choices[chosenIndex];
        previousRow = currentGlobalRow;
        previousColumn = currentColumn;
        currentGlobalRow = chosen.r;
        currentColumn = chosen.c;
        targetCellCenter = mazeGen.CellWorldCenter(currentGlobalRow, currentColumn);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Hit by the visual bullet tracer (the actual hit/miss decision is made instantly by
        // PlayerController's hitscan when the button is pressed - this just cleans up the tracer
        // and covers the case where the cat is still here for it to physically touch).
        if (other.CompareTag("Bullet"))
        {
            Destroy(other.gameObject);
            Kill();
        }
        // Hit Player -> Instant Game Over
        else if (other.CompareTag("Player"))
        {
            var cameraFollow = FindFirstObjectByType<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.TriggerCatCatchDeath();
            }
        }
    }

    // Requires this prefab to be tagged "Cat" so PlayerController's hitscan can find it.
    public void Kill()
    {
        Destroy(gameObject);
    }
}