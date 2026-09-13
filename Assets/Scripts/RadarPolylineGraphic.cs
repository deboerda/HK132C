using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class RadarPolylineGraphic : MaskableGraphic
{
    private static Material overlayMaterial;
    private readonly List<Vector3> points = new List<Vector3>();
    private float lineWidth = 2f;

    public int positionCount
    {
        get => points.Count;
        set
        {
            if (value <= 0)
                points.Clear();
            else
                while (points.Count < value) points.Add(Vector3.zero);
            SetVerticesDirty();
        }
    }
    public float startWidth { get => lineWidth; set => lineWidth = value; }
    public float endWidth { get => lineWidth; set => lineWidth = value; }
    public Color startColor { get => color; set => color = value; }
    public Color endColor { get => color; set => color = value; }
    public int sortingOrder { get; set; }
    public string sortingLayerName { get; set; }

    public override Material defaultMaterial
    {
        get
        {
            var overlay = GetOverlayMaterial();
            return overlay != null ? overlay : base.defaultMaterial;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        maskable = true;
        raycastTarget = false;
        StretchToParent();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        maskable = true;
        StretchToParent();
    }

    public void SetPositions(Vector3[] values)
    {
        points.Clear();
        if (values != null) points.AddRange(values);
        SetVerticesDirty();
    }

    public void SetPosition(int index, Vector3 value)
    {
        while (points.Count <= index) points.Add(Vector3.zero);
        points[index] = value;
        SetVerticesDirty();
    }

    private static Material GetOverlayMaterial()
    {
        if (overlayMaterial != null)
            return overlayMaterial;

        var shader = Shader.Find("UI/OverlayAlways");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        if (shader == null)
            return null;

        overlayMaterial = new Material(shader)
        {
            name = "RadarOverlayAlways",
            hideFlags = HideFlags.HideAndDontSave
        };
        overlayMaterial.EnableKeyword("UNITY_UI_CLIP_RECT");
        overlayMaterial.SetVector("_ClipRect", new Vector4(-32767f, -32767f, 32767f, 32767f));
        return overlayMaterial;
    }

    private void StretchToParent()
    {
        var rt = rectTransform;
        if (rt == null)
            return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        var pos = rt.localPosition;
        pos.z = 0f;
        rt.localPosition = pos;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points.Count < 2 || lineWidth <= 0f) return;
        float half = lineWidth * 0.5f;
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = points[i - 1];
            Vector2 b = points[i];
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 0.000001f) continue;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * half;
            int index = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
