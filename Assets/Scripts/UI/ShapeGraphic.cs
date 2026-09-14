using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum UIShape { Star, TriangleDown, Circle, PuzzlePiece }

// Flat vector shapes drawn straight into the UI mesh, so a star, a wheel
// pointer, a badge disc and a puzzle-piece chip cost no sprites, no atlas
// entries and no import settings, and stay sharp at any size.
//
// This also sidesteps a real problem: neither shipped font contains U+2605 or
// U+25BC, so those characters render as missing-glyph boxes in TextMeshPro.
// RequireComponent must be declared here, not inherited from Graphic: without
// it the CanvasRenderer is missing and every one of these inside a RectMask2D
// throws MissingComponentException on each clip update. ThemedFrame declares
// the same attribute for the same reason.
[AddComponentMenu("UI/Shape Graphic")]
[RequireComponent(typeof(CanvasRenderer))]
public class ShapeGraphic : MaskableGraphic
{
    public UIShape shape = UIShape.Star;

    [Min(3)] public int points = 5;
    [Range(0.1f, 0.95f)] public float innerRatio = 0.45f;

    static readonly List<Vector2> Rim = new List<Vector2>(64);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f) return;

        Vector2 c = r.center;
        float size = Mathf.Min(r.width, r.height);

        switch (shape)
        {
            case UIShape.TriangleDown:
                AddTriangleDown(vh, r);
                return;
            case UIShape.Star:
                BuildStar(size * 0.5f);
                break;
            case UIShape.Circle:
                BuildCircle(size * 0.5f);
                break;
            default:
                BuildPuzzlePiece(size);
                break;
        }

        AddFan(vh, c);
    }

    void BuildStar(float outer)
    {
        Rim.Clear();
        float inner = outer * innerRatio;
        int n = points * 2;
        for (int i = 0; i < n; i++)
        {
            // Start at 12 o'clock so a single star reads upright.
            float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / n;
            float rad = (i & 1) == 0 ? outer : inner;
            Rim.Add(new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad));
        }
    }

    void BuildCircle(float radius)
    {
        Rim.Clear();
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Rim.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
        }
    }

    // A square with a tab on the right edge and one on the top edge. Every rim
    // point stays visible from the centre, so the triangle fan below is valid.
    void BuildPuzzlePiece(float size)
    {
        Rim.Clear();
        float s = size * 0.33f;
        float tab = size * 0.145f;
        const int arc = 10;

        Rim.Add(new Vector2(-s, -s));
        Rim.Add(new Vector2(s, -s));

        // Right tab, swept from the bottom of the edge to the top.
        for (int i = 0; i <= arc; i++)
        {
            float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, i / (float)arc);
            Rim.Add(new Vector2(s + Mathf.Cos(a) * tab, Mathf.Sin(a) * tab));
        }

        Rim.Add(new Vector2(s, s));

        // Top tab, swept right to left to match the rim direction.
        for (int i = 0; i <= arc; i++)
        {
            float a = Mathf.Lerp(0f, Mathf.PI, i / (float)arc);
            Rim.Add(new Vector2(Mathf.Cos(a) * tab, s + Mathf.Sin(a) * tab));
        }

        Rim.Add(new Vector2(-s, s));
    }

    void AddFan(VertexHelper vh, Vector2 centre)
    {
        Color32 col = color;
        vh.AddVert(new Vector3(centre.x, centre.y), col, new Vector2(0.5f, 0.5f));
        for (int i = 0; i < Rim.Count; i++)
            vh.AddVert(new Vector3(centre.x + Rim[i].x, centre.y + Rim[i].y), col, Vector2.zero);

        for (int i = 0; i < Rim.Count; i++)
            vh.AddTriangle(0, 1 + i, 1 + (i + 1) % Rim.Count);
    }

    void AddTriangleDown(VertexHelper vh, Rect r)
    {
        Color32 col = color;
        vh.AddVert(new Vector3(r.xMin, r.yMax), col, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMax), col, Vector2.one);
        vh.AddVert(new Vector3(r.center.x, r.yMin), col, new Vector2(0.5f, 0f));
        vh.AddTriangle(0, 1, 2);
    }
}
