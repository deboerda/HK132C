using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 实心圆弧（楔形/扇形块）UI 图形：从圆心到圆弧的填充三角扇。
/// 角度约定与雷达扇形一致：0° = 正上方，顺时针。
/// 使用 UI/OverlayAlways 材质，保证绘制在飞机图标之上。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RadarArcGraphic : MaskableGraphic
{
    private float startAngleDeg;
    private float endAngleDeg = 360f;
    private float radius = 16f;
    private float innerRadius;
    private int segments = 24;

    public int sortingOrder { get; set; }
    public string sortingLayerName { get; set; }

    public override Material defaultMaterial
    {
        get
        {
            var overlay = RadarPolylineGraphic.GetOverlayMaterial();
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
        raycastTarget = false;
        StretchToParent();
    }

    public void SetArc(float startDeg, float endDeg, float radiusValue, int segmentCount, float thickness = 0f)
    {
        startAngleDeg = startDeg;
        endAngleDeg = endDeg;
        radius = radiusValue;
        innerRadius = thickness > 0.01f ? Mathf.Clamp(radiusValue - thickness, 0f, radiusValue) : 0f;
        segments = Mathf.Max(4, segmentCount);
        SetVerticesDirty();
    }

    public void SetFilled()
    {
        if (innerRadius <= 0.01f)
            return;
        innerRadius = 0f;
        SetVerticesDirty();
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
        if (radius <= 0f || endAngleDeg - startAngleDeg <= 0f)
            return;

        int index = vh.currentVertCount;
        if (innerRadius <= 0.01f)
        {
            vh.AddVert(Vector2.zero, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(startAngleDeg, endAngleDeg, (float)i / segments) * Mathf.Deg2Rad;
                vh.AddVert(new Vector2(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius), color, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
                vh.AddTriangle(index, index + 1 + i, index + 2 + i);
            return;
        }

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(startAngleDeg, endAngleDeg, (float)i / segments) * Mathf.Deg2Rad;
            float s = Mathf.Sin(angle);
            float c = Mathf.Cos(angle);
            vh.AddVert(new Vector2(s * innerRadius, c * innerRadius), color, Vector2.zero);
            vh.AddVert(new Vector2(s * radius, c * radius), color, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
        {
            int a = index + i * 2;
            vh.AddTriangle(a, a + 1, a + 3);
            vh.AddTriangle(a, a + 3, a + 2);
        }
    }
}
