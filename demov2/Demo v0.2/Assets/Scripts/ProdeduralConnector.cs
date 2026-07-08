using System.Collections.Generic;
using UnityEngine;

public static class ProceduralConnector
{
    private static readonly Color PipeColor = new Color(0.25f, 0.25f, 0.28f, 1f);
    public const float PipeWidth       = 1.2f;
    public const float ConnectorHeight = 4f; // taller so stubs clearly reach mazes
    // Small overlap to avoid 1-pixel rendering seams between pieces
    private const float Overlap = 0.02f;

    public static GameObject Build(
        float[] fromPositions,
        float[] toPositions,
        float baseY,
        out float exitY)
    {
        exitY = baseY + ConnectorHeight;
        float midY = baseY + ConnectorHeight * 0.5f;

        GameObject root = new GameObject("Connector");
        // Place the root at the connector's vertical centre so cleanup
        // (which checks the root.transform.position.y) can correctly
        // determine when the whole connector is behind the player.
        root.transform.position = new Vector3(0f, midY, 0f);

        // Collect unique X columns, max 2
        List<float> allColumns = new List<float>();
        foreach (float x in fromPositions)
            if (!ContainsApprox(allColumns, x)) allColumns.Add(x);
        foreach (float x in toPositions)
            if (!ContainsApprox(allColumns, x)) allColumns.Add(x);
        allColumns.Sort();
        if (allColumns.Count > 2)
        {
            // Keep the two outermost columns (leftmost and rightmost)
            // so we always connect the extreme maze columns instead of
            // accidentally dropping the right-most column.
            float leftMost = allColumns[0];
            float rightMost = allColumns[allColumns.Count - 1];
            allColumns = new List<float> { leftMost, rightMost };
        }

        bool multiColumn = allColumns.Count > 1;

        if (!multiColumn)
        {
            // ── Straight through — single vertical pipe ───────────
            float x = allColumns[0];
            // Extend slightly past baseY and exitY so it overlaps with
            // the maze pipes above and below (no visible gap)
            float overlapY   = baseY  - PipeWidth * 0.5f;
            float overlapTop = exitY  + PipeWidth * 0.5f;
            float height     = overlapTop - overlapY;
            CreatePipe(root, "VStraight",
                new Vector3(x, (overlapY + overlapTop) * 0.5f, 0f),
                new Vector2(PipeWidth, height));
        }
        else
        {
            // ── Two columns: horizontal bar + stubs ───────────────
            float leftX  = allColumns[0];
            float rightX = allColumns[1];

            // Horizontal bar — slightly wider than the column span
            // so it overlaps the vertical stubs cleanly
            float barWidth   = (rightX - leftX) + PipeWidth;
            float barCenterX = (leftX + rightX) * 0.5f;
            CreatePipe(root, "HBar",
                new Vector3(barCenterX, midY, 0f),
                new Vector2(barWidth, PipeWidth));

            // Bottom stubs — from just below baseY up to midY
            // The extra overlap downward hides the seam with the maze below
            foreach (float x in fromPositions)
            {
                float stubBottom = baseY  - PipeWidth * 0.5f;
                float stubTop    = midY   + PipeWidth * 0.5f;
                float height     = stubTop - stubBottom;
                CreatePipe(root, "VBot_" + x,
                    new Vector3(x, (stubBottom + stubTop) * 0.5f, 0f),
                    new Vector2(PipeWidth, height));
            }

            // Top stubs — from midY up to just above exitY
            // The extra overlap upward hides the seam with the maze above
            foreach (float x in toPositions)
            {
                float stubBottom = midY  - PipeWidth * 0.5f;
                float stubTop    = exitY + PipeWidth * 0.5f;
                float height     = stubTop - stubBottom;
                CreatePipe(root, "VTop_" + x,
                    new Vector3(x, (stubBottom + stubTop) * 0.5f, 0f),
                    new Vector2(PipeWidth, height));
            }

            // Center entry stub — when fromPositions has a column that is
            // NOT one of the two final maze columns (e.g. single center → double)
            foreach (float fx in fromPositions)
            {
                if (ContainsApprox(new float[]{ leftX, rightX }, fx)) continue;

                float stubBottom = baseY - PipeWidth * 0.5f;
                float stubTop    = midY  + PipeWidth * 0.5f;
                float height     = stubTop - stubBottom;
                CreatePipe(root, "VBotCenter_" + fx,
                    new Vector3(fx, (stubBottom + stubTop) * 0.5f, 0f),
                    new Vector2(PipeWidth, height));

                // Junction at center where player hits the bar
                CreateJunction(root, new Vector3(fx, midY, 0f),
                    up:    false,
                    down:  true,
                    left:  fx > leftX + 0.01f,
                    right: fx < rightX - 0.01f);
            }

            // Center exit stub — when toPositions has a column that is
            // NOT one of the two source columns (e.g. double → single center)
            foreach (float tx in toPositions)
            {
                if (ContainsApprox(new float[]{ leftX, rightX }, tx)) continue;

                float stubBottom = midY  - PipeWidth * 0.5f;
                float stubTop    = exitY + PipeWidth * 0.5f;
                float height     = stubTop - stubBottom;
                CreatePipe(root, "VTopCenter_" + tx,
                    new Vector3(tx, (stubBottom + stubTop) * 0.5f, 0f),
                    new Vector2(PipeWidth, height));

                // Create a junction at the center so player can move UP
                // from the horizontal bar into the single maze above
                CreateJunction(root, new Vector3(tx, midY, 0f),
                    up:    true,
                    down:  true,
                    left:  true,
                    right: true);
            }

            // Junctions at midY for left and right columns
            for (int i = 0; i < allColumns.Count; i++)
            {
                float x      = allColumns[i];
                bool isFrom  = ContainsApprox(fromPositions, x);
                bool isTo    = ContainsApprox(toPositions,   x);
                bool hasLeft  = (i > 0);
                bool hasRight = (i < allColumns.Count - 1);

                if (!isFrom && !isTo) continue;

                CreateJunction(root, new Vector3(x, midY, 0f),
                    up:    isTo,
                    down:  isFrom,
                    left:  hasLeft,
                    right: hasRight);
            }
        }

        return root;
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static void CreatePipe(GameObject parent, string name,
        Vector3 worldPos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform);

        // Position child relative to the parent's world position so the
        // parent's Y reflects the connector's actual vertical location
        // (used by LevelManager cleanup).
        go.transform.localPosition = worldPos - parent.transform.position;

        // Slightly inflate sizes to avoid visible seams from sprite edges
        Vector2 adjustedSize = new Vector2(size.x + Overlap, size.y + Overlap);
        go.transform.localScale = new Vector3(adjustedSize.x, adjustedSize.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = GetSquareSprite();
        sr.color        = PipeColor;
        sr.sortingOrder = 0;
    }

    private static void CreateJunction(GameObject parent, Vector3 worldPos,
        bool up, bool down, bool left, bool right)
    {
        GameObject go = new GameObject("Junction");
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = worldPos - parent.transform.position;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = new Vector2(PipeWidth * 1.5f, PipeWidth * 1.5f);

        JunctionTrigger jt = go.AddComponent<JunctionTrigger>();
        jt.up    = up;
        jt.down  = down;
        jt.left  = left;
        jt.right = right;

        // Draw a small filled square at the junction so the intersection
        // area is visually covered (this prevents a 1-unit hole at
        // single<->double transitions on some devices).
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquareSprite();
        sr.color = PipeColor;
        sr.sortingOrder = 0;
        go.transform.localScale = new Vector3(PipeWidth + Overlap, PipeWidth + Overlap, 1f);
    }

    private static bool ContainsApprox(IEnumerable<float> list, float value,
        float tolerance = 0.1f)
    {
        foreach (float f in list)
            if (Mathf.Abs(f - value) < tolerance) return true;
        return false;
    }

    private static Sprite _squareSprite;
    private static Sprite GetSquareSprite()
    {
        if (_squareSprite != null) return _squareSprite;
        // Use a plain 1x1 white texture to avoid any bordered UI sprite
        // artifacts which can cause thin seams between scaled pieces.
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _squareSprite = Sprite.Create(tex,
            new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _squareSprite;
    }
}