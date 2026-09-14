using UnityEngine;
using UnityEngine.UI;

// Procedural themed frame for uGUI: a rounded rectangle with a gold-style
// border and a vertical gradient fill, drawn by the UI/FrameSDF shader.
// No sprite needed, crisp at any size, and every frame shares one material
// (params are packed into vertex data), so recolour the whole HUD from a
// preset instead of juggling mismatched PNGs.
//
// Set `fill` (0..1) to use it as a horizontal progress bar (rounded left cap,
// straight cut on the right) — that's how the XP bar's fill is driven.
//
// ponytail: params via vertex UVs, one shared material. Ornate scrollwork
// (the big popup frame) still needs real art — this is for pills/bars/rows.
[AddComponentMenu("UI/Themed Frame (SDF)")]
[RequireComponent(typeof(CanvasRenderer))]
public class ThemedFrame : MaskableGraphic
{
    [Min(0f)] public float cornerRadius = 24f;
    [Min(0f)] public float borderThickness = 4f;
    public Color borderColor = new Color(1f, 0.84f, 0.40f, 1f);      // gold
    [Tooltip("Fill at the top edge.")]
    public Color fillTop = new Color(0.14f, 0.08f, 0.28f, 0.96f);
    [Tooltip("Fill at the bottom edge. Set equal to Fill Top for a flat fill.")]
    public Color fillBottom = new Color(0.05f, 0.02f, 0.12f, 0.96f);

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Horizontal fill fraction (1 = full frame; <1 = progress bar).")]
    float m_Fill = 1f;

    /// Horizontal fill fraction, 0..1. Drives the frame like a progress bar.
    public float fill
    {
        get => m_Fill;
        set
        {
            float v = Mathf.Clamp01(value);
            if (v == m_Fill) return;
            m_Fill = v;
            SetVerticesDirty();
        }
    }

    public const string ShaderName = "UI/FrameSDF";

    static Material s_Shared;

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureMaterial();
    }

    void EnsureMaterial()
    {
        // A FrameSDF material asset assigned by the scene builder wins — it
        // serialises, survives domain reloads and can't be stripped from a
        // build. This lookup is only a fallback for frames made at runtime.
        var current = material;
        if (current != null && current.shader != null && current.shader.name == ShaderName) return;

        if (s_Shared == null)
        {
            var sh = Shader.Find(ShaderName);
            if (sh == null)
            {
                Debug.LogError($"ThemedFrame: shader '{ShaderName}' not found — " +
                               "frames will render as flat untextured quads.", this);
                return;
            }
            s_Shared = new Material(sh) { hideFlags = HideFlags.DontSave };
        }
        material = s_Shared;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        EnsureMaterial();
        SetVerticesDirty();
    }
#endif

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float f = Mathf.Clamp01(m_Fill);
        if (f <= 0f) return;

        Rect r = GetPixelAdjustedRect();
        float w = r.width, h = r.height;
        if (w <= 0f || h <= 0f) return;

        float maxR   = Mathf.Min(w, h) * 0.5f;
        float radius = Mathf.Clamp(cornerRadius, 0f, maxR);
        float border = Mathf.Clamp(borderThickness, 0f, maxR);

        // Right edge is cut to the fill fraction; UVs stay normalised to the
        // full rect so the SDF keeps the true rounded-rect shape (rounded left
        // cap, straight right edge while filling).
        float xRight = r.xMin + w * f;

        Vector4 uv1 = new Vector4(radius, border, 0f, 0f);
        Vector4 uv2 = borderColor;

        AddVert(vh, new Vector3(r.xMin,  r.yMin), fillBottom, new Vector4(0f, 0f, w, h), uv1, uv2);
        AddVert(vh, new Vector3(r.xMin,  r.yMax), fillTop,    new Vector4(0f, 1f, w, h), uv1, uv2);
        AddVert(vh, new Vector3(xRight,  r.yMax), fillTop,    new Vector4(f,  1f, w, h), uv1, uv2);
        AddVert(vh, new Vector3(xRight,  r.yMin), fillBottom, new Vector4(f,  0f, w, h), uv1, uv2);

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    static void AddVert(VertexHelper vh, Vector3 pos, Color col, Vector4 uv0, Vector4 uv1, Vector4 uv2)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = pos;
        v.color = col;
        v.uv0 = uv0;
        v.uv1 = uv1;
        v.uv2 = uv2;
        vh.AddVert(v);
    }
}
