using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 传感器信息面板：按飞机编号分组显示每架飞机的全部传感器信息。
/// 仅展示，不做控制。
/// </summary>
public class SensorControlPanel : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private bool autoFindReceiver = true;
    [SerializeField] private float refreshInterval = 0.5f;

    [Header("Layout")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private float groupHeaderHeight = 28f;
    [SerializeField] private float sensorRowHeight = 52f;
    [SerializeField] private float scrollbarWidth = 14f;

    [Header("Style")]
    [SerializeField] private Font font;
    [SerializeField] private int headerFontSize = 14;
    [SerializeField] private int rowFontSize = 12;
    [SerializeField] private Color panelBackground = new Color(0.06f, 0.08f, 0.1f, 0.92f);
    [SerializeField] private Color groupHeaderColor = new Color(0.12f, 0.2f, 0.35f, 0.95f);
    [SerializeField] private Color rowColor = new Color(0.1f, 0.12f, 0.16f, 0.8f);
    [SerializeField] private Color textColor = new Color(0.92f, 0.96f, 1f, 1f);
    [SerializeField] private Color accentColor = new Color(0.43f, 0.82f, 1f, 1f);
    [SerializeField] private Color mutedColor = new Color(0.66f, 0.74f, 0.82f, 1f);
    [SerializeField] private Color okColor = new Color(0.3f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color warnColor = new Color(1f, 0.5f, 0.2f, 1f);

    private float nextRefreshTime;

    private void Awake()
    {
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (contentRoot == null)
            contentRoot = GetComponent<RectTransform>();
        EnsureLayout();
    }

    private void Start()
    {
        if (receiver == null && autoFindReceiver)
            receiver = FlightDataStreamReceiver.Instance;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;
        nextRefreshTime = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    // ── Refresh ─────────────────────────────────────────────────

    private void Refresh()
    {
        if (receiver == null && autoFindReceiver)
            receiver = FlightDataStreamReceiver.Instance;
        if (receiver == null)
            return;

        var states = receiver.SnapshotStates;
        if (states == null || states.Count == 0)
            return;

        // Collect all sensor info grouped by trackId
        var sensorsByTrack = new Dictionary<string, List<FlightDataStreamReceiver.TrackTimetableState>>();
        foreach (var s in states)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.trackId))
                continue;
            if (!sensorsByTrack.TryGetValue(s.trackId, out var list))
            {
                list = new List<FlightDataStreamReceiver.TrackTimetableState>();
                sensorsByTrack[s.trackId] = list;
            }
            list.Add(s);
        }

        RebuildGroups(sensorsByTrack);
    }

    // ── UI rebuild ──────────────────────────────────────────────

    private void RebuildGroups(Dictionary<string, List<FlightDataStreamReceiver.TrackTimetableState>> sensorsByTrack)
    {
        // Clear old content
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        var trackIds = new List<string>(sensorsByTrack.Keys);
        trackIds.Sort();

        foreach (var trackId in trackIds)
            CreateGroup(trackId, sensorsByTrack[trackId]);
    }

    private void CreateGroup(string trackId, List<FlightDataStreamReceiver.TrackTimetableState> sensors)
    {
        // Group header
        var headerGo = new GameObject($"Group_{trackId}");
        headerGo.transform.SetParent(contentRoot, false);
        headerGo.layer = 5;
        var headerRT = headerGo.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0f, groupHeaderHeight);
        var headerImg = headerGo.AddComponent<Image>();
        headerImg.color = groupHeaderColor;
        var headerText = headerGo.AddComponent<Text>();
        headerText.text = $"▶ {trackId}";
        headerText.font = font;
        headerText.fontSize = headerFontSize;
        headerText.color = accentColor;
        headerText.alignment = TextAnchor.MiddleLeft;
        headerText.raycastTarget = false;

        // Sensor rows — each row shows all fields of one sensor
        foreach (var s in sensors)
        {
            CreateSensorRow(s);
        }
    }

    private void CreateSensorRow(FlightDataStreamReceiver.TrackTimetableState s)
    {
        string detectorType = NormalizeDetector(s.detectorType);
        if (string.IsNullOrEmpty(detectorType))
            detectorType = s.detectorType ?? "-";

        var rowGo = new GameObject($"Sensor_{s.trackId}_{detectorType}");
        rowGo.transform.SetParent(contentRoot, false);
        rowGo.layer = 5;
        var rowRT = rowGo.AddComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0f, 1f);
        rowRT.anchorMax = new Vector2(1f, 1f);
        rowRT.pivot = new Vector2(0.5f, 1f);
        rowRT.anchoredPosition = Vector2.zero;
        rowRT.sizeDelta = new Vector2(0f, sensorRowHeight);
        var rowBg = rowGo.AddComponent<Image>();
        rowBg.color = rowColor;

        // Row content: two lines
        // Line 1: 探测器类型 | 范围类型 | 探测范围
        // Line 2: 左右偏转 | 上下偏转 | 中心点 | 状态

        // Line 1
        var line1 = CreateText(rowRT, "Line1", 0f, 0.5f, 1f, 1f, 10f, 2f);
        line1.fontSize = rowFontSize;
        line1.alignment = TextAnchor.MiddleLeft;
        line1.color = textColor;
        line1.text = $"{detectorType}  |  {FallbackText(s.rangeType, "-")}  |  {s.detectRange:F0}m";

        // Line 2
        var line2 = CreateText(rowRT, "Line2", 0f, 0f, 1f, 0.5f, 10f, 2f);
        line2.fontSize = rowFontSize - 1;
        line2.alignment = TextAnchor.MiddleLeft;
        line2.color = mutedColor;
        string radarStatus = FallbackText(s.radarStatus, "未知");
        line2.text = $"偏转: 左右{s.yawAngle:F0}° 上下{s.pitchAngle:F0}°  |  中心: {FallbackText(s.centerPoint, "-")}  |  {radarStatus}";

        // State badge (right side)
        string stateText = string.IsNullOrWhiteSpace(s.state) ? "-" : s.state;
        Color stateColor = mutedColor;
        if (stateText.Contains("正常") || stateText.ToLowerInvariant().Contains("normal"))
            stateColor = okColor;
        else if (stateText.Contains("故障") || stateText.Contains("异常") || stateText.ToLowerInvariant().Contains("fault"))
            stateColor = warnColor;

        var stateGo = new GameObject("State");
        stateGo.transform.SetParent(rowRT, false);
        stateGo.layer = 5;
        var stateRT = stateGo.AddComponent<RectTransform>();
        stateRT.anchorMin = new Vector2(0.65f, 0f);
        stateRT.anchorMax = new Vector2(1f, 1f);
        stateRT.offsetMin = new Vector2(4f, 2f);
        stateRT.offsetMax = new Vector2(-8f, -2f);
        var stateLabel = stateGo.AddComponent<Text>();
        stateLabel.text = stateText;
        stateLabel.font = font;
        stateLabel.fontSize = rowFontSize;
        stateLabel.color = stateColor;
        stateLabel.alignment = TextAnchor.MiddleRight;
        stateLabel.raycastTarget = false;
    }

    // ── Helpers ─────────────────────────────────────────────────

    private Text CreateText(RectTransform parent, string name, float anchorXMin, float anchorYMin, float anchorXMax, float anchorYMax, float leftPad, float topPad)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = 5;
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorXMin, anchorYMin);
        rt.anchorMax = new Vector2(anchorXMax, anchorYMax);
        rt.offsetMin = new Vector2(leftPad, 0f);
        rt.offsetMax = new Vector2(-4f, -topPad);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    private static string NormalizeDetector(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        string s = raw.Trim();
        if (s.Contains("雷达") || s.ToLowerInvariant().Contains("radar"))
            return "雷达";
        if (s.Contains("光测") || s.ToLowerInvariant().Contains("optic"))
            return "光测";
        if (s.Contains("电测") || s.ToLowerInvariant().Contains("elect"))
            return "电测";
        return s;
    }

    private static string FallbackText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private void EnsureLayout()
    {
        var panel = GetComponent<RectTransform>();
        if (panel == null) return;

        var bg = GetComponent<Image>();
        if (bg == null)
            bg = gameObject.AddComponent<Image>();
        bg.color = panelBackground;

        // Scroll View
        var scrollGo = new GameObject("Scroll View");
        scrollGo.transform.SetParent(panel, false);
        scrollGo.layer = 5;
        var scrollRT = scrollGo.AddComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = new Vector2(-scrollbarWidth, 0f);
        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = new Color(0, 0, 0, 0);

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(scrollRT, false);
        viewportGo.layer = 5;
        var viewportRT = viewportGo.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = new Color(1, 1, 1, 0.01f);
        var mask = viewportGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content
        contentRoot = new GameObject("Content").AddComponent<RectTransform>();
        contentRoot.SetParent(viewportRT, false);
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = Vector2.one;
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.offsetMin = Vector2.zero;
        contentRoot.offsetMax = Vector2.zero;
        contentRoot.sizeDelta = new Vector2(0f, 0f);

        var vlg = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 0f;

        var fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRoot;

        // Scrollbar
        var sbGo = new GameObject("Vertical Scrollbar");
        sbGo.transform.SetParent(panel, false);
        sbGo.layer = 5;
        var sbRT = sbGo.AddComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(1f, 0f);
        sbRT.anchorMax = new Vector2(1f, 1f);
        sbRT.pivot = new Vector2(1f, 0.5f);
        sbRT.offsetMin = new Vector2(-scrollbarWidth, 0f);
        sbRT.offsetMax = Vector2.zero;
        var sbBg = sbGo.AddComponent<Image>();
        sbBg.color = new Color(0.04f, 0.05f, 0.07f, 0.65f);

        var saGo = new GameObject("Sliding Area");
        saGo.transform.SetParent(sbRT, false);
        var saRT = saGo.AddComponent<RectTransform>();
        saRT.anchorMin = Vector2.zero;
        saRT.anchorMax = Vector2.one;
        saRT.offsetMin = new Vector2(2f, 2f);
        saRT.offsetMax = new Vector2(-2f, -2f);

        var handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(saRT, false);
        var handleRT = handleGo.AddComponent<RectTransform>();
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = Vector2.one;
        handleRT.offsetMin = Vector2.zero;
        handleRT.offsetMax = Vector2.zero;
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color = new Color(0.43f, 0.82f, 1f, 0.9f);

        var scrollbar = sbGo.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRT;
        scrollbar.targetGraphic = handleImg;
        scrollbar.size = 1f;

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalScrollbarSpacing = 2f;
    }
}
