using System.Collections.Generic;
using UnityEngine;

public static class ProceduralConnector
{
    private static readonly Color PipeColor = new Color(0.25f, 0.25f, 0.28f, 1f);
    public const float PipeWidth       = 0.8f;  // was 1.2f — skinnier for tighter turns
    public const float ConnectorHeight = 4f;
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
        root.transform.position = new Vector3(0f, midY, 0f);

        List<float> allColumns = new List<float>();
        foreach (float x in fromPositions)
            if (!ContainsApprox(allColumns, x)) allColumns.Add(x);
        foreach (float x in toPositions)
            if (!ContainsApprox(allColumns, x)) allColumns.Add(x);
        allColumns.Sort();
        if (allColumns.Count > 2)
        {
            float leftMost = allColumns[0];
            float rightMost = allColumns[allColumns.Count - 1];
            allColumns = new List<float> { leftMost, rightMost };
        }

        bool multiColumn = allColumns.Count > 1;

        if (!multiColumn)
        {
            float x = allColumns[0];
            float overlapY   = baseY  - PipeWidth * 0.5f;
            float overlapTop = exitY  + PipeWidth * 0.5f;
            float height     = overlapTop - overlapY;
            CreatePipe(root, "VStraight",
                new Vector3(x, (overlapY + overlapTop) * 0.5f, 0f),
                new Vector2(PipeWidth, height));
        }
        else
        {
            float leftX  = allColumns[0];
            float rightX = allColumns[1];

            float barWidth   = (rightX - leftX) + PipeWidth;
            float barCenterX = (leftX + rightX) * 0.5f;
            CreatePipe(root, "HBar",
                new Vector3(barCenterX, midY, 0f),
                new Vector2(barWidth, PipeWidth));

            foreach (float x in fromPositions)
            {
                float stubBottom = baseY  - PipeWidth * 0.5f;
                float stubTop    = midY   + PipeWidth * 0.5f;
                float height     = stubTop - stubBottom;
                CreatePipe(root, "VBot_" + x,
                    new Vector3(x, (stubBottom + stubTop) * 0.5f, 0f),
                    new Vector2(PipeWidth, height));
            }

            foreach (float x in toPositions)
            {
                float stubBottom = midY  - PipeWidth * 0.5f;
                float stubTop    = exitY + PipeWidth * 0.5f;
                float height     = stubTop - stubBottom;
                CreatePipe(root, "VTop_" + x,
                    new Vector3(x, (stubBottom + stubTop) * 0.5f, 0f),
                    new Vector2(PipeWidth, height));
            }

            foreach (float fx in fromPositions)
            {
                if (ContainsApprox(new float[]{ leftX, rightX }, fx)) continue;

                float stubBottom = baseY - PipeWidth * 0.5f;
                float stubTop    = midY  + PipeWidth * 0.5f;
                float height     = stubTop - stubBottom;
                CreatePipe(root, "VBotCenter_" + fx,
                    new Vector3(fx, (stubBottom + stubTop) * 0.5f, 0f),
                    new Vector2(PipeWidth, height));

                CreateJunction(root, new Vector3(fx, midY, 0f),
                    up:    false,
                    down:  true,
                    left:  fx > leftX + 0.01f,
                    right: fx < rightX - 0.01f);
            }

            foreach (float tx in toPositions)
            {
                if (ContainsApprox(new float[]{ leftX, rightX }, tx)) continue;

                float stubBottom = midY  - PipeWidth * 0.5f;
                float stubTop    = exitY + PipeWidth * 0.5f;
                float height     = stubTop - stubBottom;
                CreatePipe(root, "VTopCenter_" + tx,
                    new Vector3(tx, (stubBottom + stubTop) * 0.5f, 0f),
                    new Vector2(PipeWidth, height));

                CreateJunction(root, new Vector3(tx, midY, 0f),
                    up:    true,
                    down:  true,
                    left:  true,
                    right: true);
            }

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

    private static void CreatePipe(GameObject parent, string name,
        Vector3 worldPos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = worldPos - parent.transform.position;

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
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _squareSprite = Sprite.Create(tex,
            new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _squareSprite;
    }
}