using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 把一块更高精度的卫星图按经纬度叠在大底图上。
/// 飞行范围图约 2 m/像素，大图约 132 m/像素；放大后主要飞行区走这张插图层。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class MapInsetOverlay : MonoBehaviour
{
    [Header("地理范围")]
    [SerializeField] private double lonMin = 103.91916083;
    [SerializeField] private double latMin = 30.58753045;
    [SerializeField] private double lonMax = 104.25195738;
    [SerializeField] private double latMax = 30.75583896;

    private PlaneDisplayController display;
    private RectTransform rect;

    private void OnEnable()
    {
        Apply();
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled)
                Apply();
        };
#endif
    }

    public void Apply()
    {
        if (display == null)
            display = FindAnyObjectByType<PlaneDisplayController>();
        if (display == null || display.MapRoot == null)
            return;

        if (rect == null)
            rect = transform as RectTransform;
        if (rect == null)
            return;

        if (rect.parent != display.MapRoot)
            rect.SetParent(display.MapRoot, false);

        Vector2 sw = display.ConvertToAnchoredPosition((float)lonMin, (float)latMin);
        Vector2 ne = display.ConvertToAnchoredPosition((float)lonMax, (float)latMax);
        Vector2 min = new Vector2(Mathf.Min(sw.x, ne.x), Mathf.Min(sw.y, ne.y));
        Vector2 max = new Vector2(Mathf.Max(sw.x, ne.x), Mathf.Max(sw.y, ne.y));

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.anchoredPosition = (min + max) * 0.5f;
        rect.sizeDelta = max - min;
        rect.SetAsFirstSibling();
        var planes = display.MapRoot.Find("PlaneIcons");
        if (planes != null)
            planes.SetSiblingIndex(1);
    }
}
