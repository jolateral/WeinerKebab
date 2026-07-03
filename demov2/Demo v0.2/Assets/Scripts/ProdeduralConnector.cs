using System.Collections.Generic;
using UnityEngine;

public static class ProceduralConnector
{
    private static readonly Color PipeColor = new Color(0.25f, 0.25f, 0.28f, 1f);
    private const float PipeWidth      = 1.2f;
    private const float ConnectorHeight = 3f;

    public static GameObject Build(
        float[] fromPositions,
        float[] toPositions,
        float baseY,
        out float exitY)
    {
        exitY = baseY + ConnectorHeight;
        float midY = baseY + ConnectorHeight * 0.5f;

        GameObject root = new GameObject("Connector");
        root.transform.position = Vector3.zero;

        // Collect all unique X columns, clamped to max 2
        List<float> allColumns = new List<float>();
        foreach (float x in fromPositions)
            if (!ContainsApprox(allColumns, x)) allColumns.Add(x);
        foreach (float x in toPositions)
            if (!ContainsApprox(allColumns, x)) allColumns.Add(x);
        allColumns.Sort();
        if (allColumns.Count > 2) allColumns = allColumns.GetRange(0, 2);

        bool multiColumn = allColumns.Count > 1;

        if (!multiColumn)
        {
            // ── Straight single pipe, no bar needed ──────────────
            float x = allColumns[0];
            CreatePipe(root, "VStraight",
                new Vector3(x, (baseY + exitY) * 0.5f, 0f),
                new Vector2(PipeWidth, exitY - baseY));
            // No junction needed — player just goes straight through
        }
        else
        {
            // ── Two columns: need a horizontal bar + stubs ────────
            float leftX  = allColumns[0];
            float rightX = allColumns[1];

            // Horizontal bar connecting both columns at midY
            float barWidth   = (rightX - leftX) + PipeWidth;
            float barCenterX = (leftX + rightX) * 0.5f;
            CreatePipe(root, "HBar",
                new Vector3(barCenterX, midY, 0f),
                new Vector2(barWidth, PipeWidth));

            // Bottom stubs: one for EACH fromPosition going from baseY up to midY
            foreach (float x in fromPositions)
            {
                float height = midY - baseY;
                CreatePipe(root, "VBot_" + x,
                    new Vector3(x, baseY + height * 0.5f, 0f),
                    new Vector2(PipeWidth, height));
            }

            // Top stubs: one for EACH toPosition going from midY up to exitY
            foreach (float x in toPositions)
            {
                float height = exitY - midY;
                CreatePipe(root, "VTop_" + x,
                    new Vector3(x, midY + height * 0.5f, 0f),
                    new Vector2(PipeWidth, height));
            }

            // Junctions at midY for each column
            for (int i = 0; i < allColumns.Count; i++)
            {
                float x      = allColumns[i];
                bool isFrom  = ContainsApprox(fromPositions, x);
                bool isTo    = ContainsApprox(toPositions,   x);
                bool hasLeft  = (i > 0);
                bool hasRight = (i < allColumns.Count - 1);

                // Place a junction here if the player can arrive OR leave
                // through this column — they need to know they can turn
                if (!isFrom && !isTo) continue;

                CreateJunction(root, new Vector3(x, midY, 0f),
                    up:    isTo,
                    down:  isFrom,
                    left:  hasLeft,
                    right: hasRight);
            }

            // If fromPositions only has ONE entry that is NOT one of the
            // two final columns (e.g. center=0 going to left=-3 and right=3),
            // we need an extra junction where that center stub hits the bar
            foreach (float fx in fromPositions)
            {
                if (ContainsApprox(allColumns, fx)) continue;

                // This from-column hits the bar but isn't a maze column
                // Draw its bottom stub
                float height = midY - baseY;
                CreatePipe(root, "VBotCenter_" + fx,
                    new Vector3(fx, baseY + height * 0.5f, 0f),
                    new Vector2(PipeWidth, height));

                // Extend the bar to cover this center column if needed
                // (handled already by bar spanning leftX to rightX)

                // Junction at the center hit point — player can go left or right
                CreateJunction(root, new Vector3(fx, midY, 0f),
                    up:    false,
                    down:  true,
                    left:  fx > leftX,
                    right: fx < rightX);
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
        go.transform.position   = worldPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

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
        go.transform.position = worldPos;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = new Vector2(PipeWidth * 1.5f, PipeWidth * 1.5f);

        JunctionTrigger jt = go.AddComponent<JunctionTrigger>();
        jt.up    = up;
        jt.down  = down;
        jt.left  = left;
        jt.right = right;
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
        _squareSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        if (_squareSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _squareSprite = Sprite.Create(tex,
                new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        return _squareSprite;
    }
}