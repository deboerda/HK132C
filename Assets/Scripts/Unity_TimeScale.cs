using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

/// <summary>
/// 时间尺度：优先驱动场景里已有的 UGUI 刻度（0s/120s/…），
/// 收到 TASK_TIME_INFO 后改成任务时钟（如 08:00 … 08:30）。
/// 若挂了 UIDocument 仍走 UI Toolkit 作为补充。
/// </summary>
[ExecuteAlways]
public class Unity_TimeScale : MonoBehaviour
{
    [Header("UI Toolkit (optional)")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;

    [Header("Layout (UI Toolkit only)")]
    [SerializeField] private Vector2 displaySize = new Vector2(1200f, 40f);
    [SerializeField] private Vector2 screenPosition = new Vector2(360f, 920f);

    [Header("Tick Settings")]
    [SerializeField] private float majorTickHeight = 12f;
    [SerializeField] private float minorTickHeight = 6f;
    [SerializeField] private float tickWidth = 1.5f;
    [SerializeField] private int minorTicksPerMajor = 4;
    [SerializeField] private int fontSize = 11;

    [Header("Style")]
    [SerializeField] private Color bgColor = new Color(0.04f, 0.06f, 0.08f, 0.85f);
    [SerializeField] private Color majorTickColor = new Color(0.7f, 0.85f, 1f, 0.9f);
    [SerializeField] private Color minorTickColor = new Color(0.4f, 0.5f, 0.6f, 0.6f);
    [SerializeField] private Color numberColor = new Color(0.85f, 0.9f, 1f, 0.95f);
    [SerializeField] private Color lineColor = new Color(0.3f, 0.4f, 0.5f, 0.5f);

    private VisualElement root;
    private VisualElement rulerRoot;
    private VisualElement ticksContainer;
    private Label timeRangeLabel;

    private float minSec = 0f;
    private float maxSec = 600f;
    private float currentSec = 0f;
    private string startTimeStr = "";
    private string endTimeStr = "";
    private bool hasTimeRange;

    private static readonly Regex TickLabelNameRegex = new Regex(@"^Label_(\d+)$", RegexOptions.Compiled);
    private static readonly string[] TimeFormats =
    {
        "yyyy-MM-dd HH:mm:ss.ffffff",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss.ff",
        "yyyy-MM-dd HH:mm:ss.f",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy/M/d H:mm:ss",
        "HH:mm:ss",
    };

    private void OnEnable()
    {
        EnsureRoot();
        ApplyToUgui();
    }

    private void EnsureRoot()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
            return;

        root = uiDocument.rootVisualElement;
        if (root == null)
            return;

        ApplyStyleSheet();
        BuildUI();
    }

    private void ApplyStyleSheet()
    {
        if (styleSheet == null)
        {
#if UNITY_EDITOR
            styleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI Toolkit/TimeScale.uss");
#endif
        }
        if (styleSheet != null && root != null && !root.styleSheets.Contains(styleSheet))
            root.styleSheets.Add(styleSheet);
    }

    private void BuildUI()
    {
        if (root == null)
            return;
        root.Clear();

        rulerRoot = new VisualElement { name = "TimeScaleRoot" };
        rulerRoot.style.position = Position.Absolute;
        rulerRoot.style.left = screenPosition.x;
        rulerRoot.style.top = screenPosition.y;
        rulerRoot.style.width = displaySize.x;
        rulerRoot.style.height = displaySize.y;
        rulerRoot.style.backgroundColor = bgColor;
        rulerRoot.style.flexDirection = FlexDirection.Column;
        rulerRoot.style.overflow = Overflow.Hidden;

        ticksContainer = new VisualElement { name = "TicksContainer" };
        ticksContainer.style.flexGrow = 1;
        ticksContainer.style.overflow = Overflow.Hidden;
        ticksContainer.style.position = Position.Relative;
        rulerRoot.Add(ticksContainer);

        timeRangeLabel = new Label("--:--:-- ~ --:--:--");
        timeRangeLabel.style.fontSize = fontSize;
        timeRangeLabel.style.color = numberColor;
        timeRangeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        timeRangeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        timeRangeLabel.style.height = 18f;
        rulerRoot.Add(timeRangeLabel);

        root.Add(rulerRoot);
        DrawTicks();
    }

    public void SetTimeRange(float minSeconds, float maxSeconds, string startLocalTime, string endLocalTime = null)
    {
        minSec = minSeconds;
        maxSec = Mathf.Max(minSeconds, maxSeconds);
        startTimeStr = startLocalTime ?? "";
        endTimeStr = endLocalTime ?? "";
        hasTimeRange = maxSec > minSec + 0.01f;
        EnsureRoot();
        DrawTicks();
        UpdateRangeLabel();
        ApplyToUgui();
    }

    public void SetCurrentTime(float currentSeconds)
    {
        currentSec = Mathf.Clamp(currentSeconds, minSec, maxSec);
    }

    private void ApplyToUgui()
    {
        var texts = GetComponentsInChildren<Text>(true);
        var tickLabels = new List<Text>();
        Text rangeTitle = null;

        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t == null)
                continue;
            if (t.gameObject.name == "TimeScaleText")
            {
                rangeTitle = t;
                continue;
            }
            if (TickLabelNameRegex.IsMatch(t.gameObject.name) ||
                (t.transform.parent != null && t.transform.parent.name == "TimeScaleLabels"))
                tickLabels.Add(t);
        }

        tickLabels.Sort((a, b) =>
        {
            int na = ParseLabelIndex(a.gameObject.name);
            int nb = ParseLabelIndex(b.gameObject.name);
            if (na >= 0 && nb >= 0 && na != nb)
                return na.CompareTo(nb);
            return a.rectTransform.anchoredPosition.x.CompareTo(b.rectTransform.anchoredPosition.x);
        });

        int n = tickLabels.Count;
        float range = maxSec - minSec;
        if (n > 0 && range > 0.01f)
        {
            for (int i = 0; i < n; i++)
            {
                float seconds = n == 1 ? minSec : minSec + range * i / (n - 1);
                tickLabels[i].text = FormatTickValue(seconds);
            }
        }

        // 有刻度后不再显示 TimeScaleText（起止已在左右端点）
        if (rangeTitle != null)
            rangeTitle.gameObject.SetActive(false);
    }

    private static int ParseLabelIndex(string name)
    {
        if (string.IsNullOrEmpty(name))
            return -1;
        var m = TickLabelNameRegex.Match(name);
        if (!m.Success)
            return -1;
        return int.TryParse(m.Groups[1].Value, out int n) ? n : -1;
    }

    private void DrawTicks()
    {
        if (ticksContainer == null)
            return;
        ticksContainer.Clear();

        float width = displaySize.x;
        float height = displaySize.y - 18f;
        if (height < 10f)
            height = 10f;

        var baseline = new VisualElement();
        baseline.style.position = Position.Absolute;
        baseline.style.left = 0f;
        baseline.style.top = height - majorTickHeight;
        baseline.style.width = width;
        baseline.style.height = 1f;
        baseline.style.backgroundColor = lineColor;
        ticksContainer.Add(baseline);

        float range = maxSec - minSec;
        if (range <= 0f)
            return;

        float majorInterval = ChooseMajorInterval(range, 8);
        float minorInterval = majorInterval / Mathf.Max(1, minorTicksPerMajor);

        var tickValues = new List<float>();
        void AddTick(float value)
        {
            value = Mathf.Clamp(value, minSec, maxSec);
            for (int i = 0; i < tickValues.Count; i++)
            {
                if (Mathf.Abs(tickValues[i] - value) < 0.001f)
                    return;
            }
            tickValues.Add(value);
        }

        AddTick(minSec);
        AddTick(maxSec);
        float startTick = Mathf.Ceil((minSec + 0.0001f) / minorInterval) * minorInterval;
        for (float t = startTick; t < maxSec - 0.0001f; t += minorInterval)
            AddTick(t);

        float labelGuard = majorInterval * 0.35f;
        for (int i = 0; i < tickValues.Count; i++)
        {
            float tickValue = tickValues[i];
            float normalized = (tickValue - minSec) / range;
            float x = normalized * width;
            bool isEnd = Mathf.Abs(tickValue - minSec) < 0.001f || Mathf.Abs(tickValue - maxSec) < 0.001f;
            bool isMajor = isEnd
                || Mathf.Abs(tickValue / majorInterval - Mathf.Round(tickValue / majorInterval)) < 0.001f;

            float tickH = isMajor ? majorTickHeight : minorTickHeight;
            float tickY = height - tickH;

            var tick = new VisualElement();
            tick.style.position = Position.Absolute;
            tick.style.left = x - tickWidth * 0.5f;
            tick.style.top = tickY;
            tick.style.width = tickWidth;
            tick.style.height = tickH;
            tick.style.backgroundColor = isMajor ? majorTickColor : minorTickColor;
            ticksContainer.Add(tick);

            bool showLabel = isEnd || (isMajor
                && Mathf.Abs(tickValue - minSec) > labelGuard
                && Mathf.Abs(tickValue - maxSec) > labelGuard);
            if (showLabel)
            {
                var label = new Label(FormatTickValue(tickValue));
                label.style.position = Position.Absolute;
                float labelWidth = 72f;
                float left = x - labelWidth * 0.5f;
                if (Mathf.Abs(tickValue - minSec) < 0.001f)
                    left = 0f;
                else if (Mathf.Abs(tickValue - maxSec) < 0.001f)
                    left = width - labelWidth;
                label.style.left = left;
                label.style.top = tickY - 16f;
                label.style.width = labelWidth;
                label.style.fontSize = fontSize;
                label.style.color = numberColor;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                ticksContainer.Add(label);
            }
        }

        UpdateRangeLabel();
    }

    private void UpdateRangeLabel()
    {
        if (timeRangeLabel == null)
            return;

        string startStr = FormatClock(startTimeStr, minSec);
        string endStr = !string.IsNullOrWhiteSpace(endTimeStr)
            ? FormatClock(endTimeStr, 0f)
            : FormatClock(startTimeStr, maxSec);
        if (startStr == null || endStr == null)
        {
            timeRangeLabel.text = hasTimeRange ? $"{FormatElapsed(minSec)} ~ {FormatElapsed(maxSec)}" : "--:--:-- ~ --:--:--";
            return;
        }
        timeRangeLabel.text = $"{startStr} ~ {endStr}";
    }

    private string FormatTickValue(float seconds)
    {
        if (Mathf.Abs(seconds - maxSec) < 0.05f && !string.IsNullOrWhiteSpace(endTimeStr))
        {
            string endClock = FormatClock(endTimeStr, 0f);
            if (endClock != null)
                return endClock;
        }

        string clock = FormatClock(startTimeStr, seconds);
        if (clock != null)
            return clock;
        return FormatElapsed(seconds);
    }

    private static string FormatElapsed(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        if (seconds >= 3600f)
            return $"{seconds / 3600f:0.#}h";
        if (seconds >= 60f && Mathf.Abs(seconds % 60f) < 0.05f)
            return $"{seconds / 60f:0}m";
        return $"{seconds:0}s";
    }

    private static string FormatClock(string timeStr, float addSeconds)
    {
        if (TryParseLocalTime(timeStr, out var dt))
            return dt.AddSeconds(addSeconds).ToString("HH:mm");
        return null;
    }

    private static float ChooseMajorInterval(float range, int targetDivisions)
    {
        float raw = range / targetDivisions;
        float[] niceSteps = { 1f, 2f, 5f, 10f, 15f, 30f, 60f, 120f, 300f, 600f, 900f, 1800f, 3600f };
        foreach (float step in niceSteps)
            if (step >= raw)
                return step;
        return Mathf.Ceil(raw / 60f) * 60f;
    }

    private static bool TryParseLocalTime(string value, out DateTime time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (DateTime.TryParseExact(
            value.Trim(),
            TimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time))
            return true;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out time);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && uiDocument != null && root == null)
            root = uiDocument.rootVisualElement;
        if (root != null)
            BuildUI();
    }
#endif
}
