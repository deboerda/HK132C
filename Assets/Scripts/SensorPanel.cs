#pragma warning disable CS0414
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 传感器信息面板—按飞机编号分组显示传感器配置。/// 数据来源：FlightDataStreamReceiver.SnapshotStates (TrackTimetableState)
/// 列：# / 飞机编号 / 探测器类型/ 范围类型 / 探测范围 / 左右偏转角/ 上下偏转角/ 中心点/// 每个飞机编号作为分组标题，其下显示该飞机的多个传感器行。/// </summary>
[ExecuteAlways]
public class SensorPanel : MonoBehaviour
{
    [Serializable]
    private class RowView
    {
        public RectTransform root;
        public Text rankText;
        public Text idText;
        public Text detectorText;
        public InputField detectorInput;
        public Text rangeTypeText;
        public Text rangeText;
        public InputField rangeInput;
        public Text yawText;
        public Text pitchText;
        public Text centerText;
        public Button colorButton;
        public Image colorSwatch;
        public Button showButton;
        public Text showButtonLabel;
        public Button clearButton;
        public Text clearButtonLabel;
        public string boundTrackId;
        public string boundDetector;
    }

    [Header("Data")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private bool autoFindReceiver = true;
    [SerializeField] private float refreshInterval = 0.1f;

    [Header("Range Config (探测距离配置栏)")]
    [SerializeField] private RectTransform rangeConfigRoot;
    [SerializeField] private float rangeConfigHeight = 30f;
    [SerializeField] private InputField rangeInput;
    [SerializeField] private Text rangeStatusText;

    [Header("Layout")]
    [SerializeField] private Dropdown planeDropdown;
    [SerializeField] private RectTransform selectorRoot;
    [SerializeField] private RectTransform headerRoot;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform rowTemplate;
    [Tooltip("标题和数据列使用 Scene 里拖好的坐标；运行时只改文字。")]
    [SerializeField] private bool preserveSceneLayout = true;
    [SerializeField] private bool autoCreateLayout = true;
    [SerializeField] private float selectorHeight = 34f;
    [SerializeField] private float headerHeight = 32f;
    [SerializeField] private float footerHeight = 36f;
    [SerializeField] private float rowHeight = 30f;
    [SerializeField] private float groupHeaderHeight = 28f;
    [SerializeField] private float scrollbarWidth = 14f;
    [SerializeField] private float columnSpacing = 4f;
    [SerializeField] private int maxRows = 64;

    [Header("Style")]
    [SerializeField] private Font font;
    [SerializeField] private int headerFontSize = 13;
    [SerializeField] private int rowFontSize = 13;
    [SerializeField] private int groupFontSize = 14;
    [SerializeField] private Color panelBackground = new Color(0.06f, 0.08f, 0.1f, 0.7f);
    [SerializeField] private Color headerBackground = new Color(0.08f, 0.1f, 0.13f, 0.95f);
    [SerializeField] private Color oddRowBackground = new Color(0.12f, 0.14f, 0.18f, 0.82f);
    [SerializeField] private Color evenRowBackground = new Color(0.16f, 0.18f, 0.22f, 0.82f);
    [SerializeField] private Color groupBackground = new Color(0.15f, 0.2f, 0.28f, 0.95f);
    [SerializeField] private Color textColor = new Color(0.92f, 0.96f, 1f, 1f);
    [SerializeField] private Color mutedTextColor = new Color(0.66f, 0.74f, 0.82f, 1f);
    [SerializeField] private Color accentTextColor = new Color(0.43f, 0.82f, 1f, 1f);
    [SerializeField] private Color groupTextColor = new Color(0.55f, 0.85f, 1f, 1f);
    [SerializeField] private Color highlightColor = new Color(0.35f, 1f, 0.5f, 1f);
    [SerializeField] private Color selectedRowBackground = new Color(0.18f, 0.42f, 0.62f, 0.95f);

    private readonly List<RowView> rows = new List<RowView>();
    private readonly List<RectTransform> groupHeaders = new List<RectTransform>();
    private readonly List<string> dropdownTrackIds = new List<string>();
    private const string AllPlanesOption = "__ALL__";
    private const string MaxDetectRangePrefKey = "SensorPanel.MaxDetectRangeMeters";
    private const string RangeStatusDefault = "回车生效";
    private float rangeStatusUntil;
    private bool rangePrefApplied;
    


























































    private RowView headerRow;
    private float nextRefreshTime;
    private string selectedTrackId = AllPlanesOption;
    private string selectedRowTrackId;
    private string selectedRowDetector;
    private string addTargetTrackId;
    private bool batchMode;
    private Toggle batchToggle;
    private Text addHint;
    private float hintClearTime;
    private bool layoutInitialized;
    private RadarDetectionSystem radarSystem;
    private PlaneDisplayController planeDisplay;

    private static readonly Color[] DefaultSensorPalette =
    {
        new Color(0.05f, 0.85f, 1f, 1f),
        new Color(1f, 0.82f, 0.12f, 1f),
        new Color(0.35f, 1f, 0.35f, 1f),
        new Color(1f, 0.32f, 0.32f, 1f),
        new Color(0.72f, 0.45f, 1f, 1f),
        new Color(1f, 0.48f, 0.08f, 1f),
        new Color(0.1f, 0.45f, 1f, 1f),
        new Color(1f, 0.22f, 0.75f, 1f),
    };

    private RadarDetectionSystem Radar
    {
        get
        {
            if (radarSystem == null)
                radarSystem = FindAnyObjectByType<RadarDetectionSystem>();
            return radarSystem;
        }
    }

    private PlaneDisplayController Planes
    {
        get
        {
            if (planeDisplay == null)
                planeDisplay = FindAnyObjectByType<PlaneDisplayController>();
            return planeDisplay;
        }
    }

    private void Awake()
    {
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (autoCreateLayout && !preserveSceneLayout)
            EnsureLayoutObjects();

        if (planeDropdown != null)
        {
            planeDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            planeDropdown.onValueChanged.AddListener(OnDropdownChanged);
        }

        if (!preserveSceneLayout)
        {
            EnsureVerticalLayout(headerRoot, false);
            EnsureVerticalLayout(contentRoot, true);
            ApplyHeaderViewportInset();
        }

        if (headerRoot != null)
        {
            headerRow = FindOrCreateHeaderRow();
            SetRowTexts(headerRow, "#", "编号", "类型", "", "距离", "左右", "上下", "中心");
        }

        CollectSceneDataRows();
        EnsureFooterBar();
        layoutInitialized = true;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;
        var store = PanelMemory.Load();
        if (Radar != null)
            Radar.CaptureMemory(store);
        if (Planes != null)
            Planes.CaptureMemory(store);
        if (receiver != null)
            receiver.SaveSensorCatalog();
        PanelMemory.Save(store);
    }

    private void CollectSceneDataRows()
    {
        if (rowTemplate == null)
            rowTemplate = PanelSceneLayout.FindRowTemplate(contentRoot, rowTemplate);
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (layoutInitialized)
            return;

        if (autoCreateLayout)
            EnsureLayoutObjects();

        if (planeDropdown != null)
        {
            planeDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            planeDropdown.onValueChanged.AddListener(OnDropdownChanged);
        }

        EnsureVerticalLayout(headerRoot, false);
        EnsureVerticalLayout(contentRoot, true);
        ApplyHeaderViewportInset();

        if (headerRoot != null)
        {
            headerRow = FindOrCreateHeaderRow();
            SetRowTexts(headerRow, "#", "编号", "类型", "", "距离", "左右", "上下", "中心");
        }

        EnsureFooterBar();
        layoutInitialized = true;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;
        if (batchToggle == null)
            EnsureFooterBar();
        if (Time.unscaledTime < nextRefreshTime)
            return;
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);
        Refresh();
    }

    public void Refresh()
    {
        if (receiver == null && autoFindReceiver)
            receiver = FlightDataStreamReceiver.Instance;

        ApplySavedRangeOnce();
        UpdateRangeStatusExpiry();

        if (receiver == null)
        {
            SetActiveRowCount(0);
            return;
        }

        receiver.SeedSensorsIfEmpty(receiver.SnapshotStates);
        var states = receiver.SnapshotSensors;
        if ((states == null || states.Count == 0) && !receiver.HasSensorEdits)
            states = receiver.SnapshotStates;
        UpdateDropdownOptions(states);

        // 按 飞机 + 探测器增量展示：有光电就保留光电，再来雷达就加一行，无探测器类型不显示。
        var grouped = new Dictionary<string, List<FlightDataStreamReceiver.TrackTimetableState>>();
        if (states != null)
        {
            foreach (var s in states)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.trackId) || string.IsNullOrWhiteSpace(s.detectorType))
                    continue;
                if (selectedTrackId != AllPlanesOption && s.trackId != selectedTrackId)
                    continue;
                if (!grouped.TryGetValue(s.trackId, out var list))
                {
                    list = new List<FlightDataStreamReceiver.TrackTimetableState>();
                    grouped[s.trackId] = list;
                }
                bool exists = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].detectorType, s.detectorType, StringComparison.OrdinalIgnoreCase))
                    {
                        list[i] = s;
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    list.Add(s);
            }
        }

        int totalRows = 0;
        var displayItems = new List<DisplayItem>();

        // 飞机条目来源：实时有数据、有传感器、以及删过传感器的飞机。
        // 删空一架飞机的传感器后仍保留它的分组标题。
        var trackIds = new List<string>();
        var liveStates = receiver.SnapshotStates;
        if (liveStates != null)
        {
            foreach (var s in liveStates)
            {
                if (s != null && !string.IsNullOrWhiteSpace(s.trackId) && !trackIds.Contains(s.trackId))
                    trackIds.Add(s.trackId);
            }
        }
        foreach (string id in grouped.Keys)
        {
            if (!string.IsNullOrWhiteSpace(id) && !trackIds.Contains(id))
                trackIds.Add(id);
        }
        foreach (string key in receiver.RemovedSensorKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            int sep = key.IndexOf('|');
            string planeId = sep > 0 ? key.Substring(0, sep) : key;
            if (!string.IsNullOrWhiteSpace(planeId) && !trackIds.Contains(planeId))
                trackIds.Add(planeId);
        }
        if (selectedTrackId != AllPlanesOption)
            trackIds.RemoveAll(id => id != selectedTrackId);
        trackIds.Sort(StringComparer.Ordinal);

        foreach (string trackId in trackIds)
        {
            grouped.TryGetValue(trackId, out var sensors);
            displayItems.Add(new DisplayItem { type = DisplayType.GroupHeader, trackId = trackId });
            totalRows++;
            if (sensors == null || sensors.Count == 0)
                continue;
            sensors.Sort((a, b) => string.Compare(a.detectorType, b.detectorType, StringComparison.Ordinal));

            int sensorIndex = 1;
            foreach (var s in sensors)
            {
                displayItems.Add(new DisplayItem
                {
                    type = DisplayType.SensorRow,
                    rank = sensorIndex,
                    state = s,
                });
                totalRows++;
                sensorIndex++;
            }
        }

        // Ensure enough row objects (reuse for both group headers and sensor rows)
        EnsureRowCount(totalRows);
        SetActiveRowCount(totalRows);

        for (int i = 0; i < totalRows; i++)
        {
            var item = displayItems[i];
            if (item.type == DisplayType.GroupHeader)
            {
                ConfigureGroupRow(rows[i], item.trackId);
            }
            else
            {
                var s = item.state;
                string detector = string.IsNullOrWhiteSpace(s.detectorType) ? "-" : s.detectorType;
                string rangeType = string.IsNullOrWhiteSpace(s.rangeType) ? "-" : s.rangeType;
                // 探测范围列 = 面板配置的探测距离（10 变量模型不含距离，为全局配置）
                string range = Radar != null
                    ? FormatRange(Radar.GetSensorRangeMeters(s.trackId, detector))
                    : "-";
                // 有数据就显示数值（0 也显示 0），没下发才显示 -。
                string yaw = s.hasYawAngle ? $"{s.yawAngle:F1}°" : "-";
                string pitch = s.hasPitchAngle ? $"{s.pitchAngle:F1}°" : "-";
                string center = string.IsNullOrWhiteSpace(s.centerPoint) ? "-" : s.centerPoint;

                ConfigureSensorRow(rows[i], item.rank.ToString(), s.trackId, detector, rangeType, range, yaw, pitch, center);

                bool isRadar = detector.Contains("雷达") || detector.ToLowerInvariant().Contains("radar");
                if (rows[i].detectorText != null)
                    rows[i].detectorText.color = isRadar ? highlightColor : textColor;
            }
        }
    }

    private enum DisplayType { GroupHeader, SensorRow }

    private class DisplayItem
    {
        public DisplayType type;
        public string trackId;
        public int rank;
        public FlightDataStreamReceiver.TrackTimetableState state;
    }

    private void ConfigureGroupRow(RowView row, string trackId)
    {
        if (row == null) return;

        // Style as group header
        var img = row.root.GetComponent<Image>();
        if (img != null) img.color = groupBackground;

        var le = row.root.GetComponent<LayoutElement>();
        if (le != null) le.preferredHeight = groupHeaderHeight;

        if (row.rankText != null) row.rankText.text = "";
        if (row.idText != null)
        {
            row.idText.text = trackId;
            row.idText.color = groupTextColor;
            row.idText.fontStyle = FontStyle.Bold;
            row.idText.fontSize = groupFontSize;
        }
        if (row.detectorText != null) row.detectorText.text = "";
        if (row.rangeTypeText != null) row.rangeTypeText.text = "";
        if (row.rangeText != null) row.rangeText.text = "";
        if (row.yawText != null) row.yawText.text = "";
        if (row.pitchText != null) row.pitchText.text = "";
        if (row.centerText != null) row.centerText.text = "";
        if (row.rangeInput != null) row.rangeInput.gameObject.SetActive(false);
        if (row.detectorInput != null)
        {
            row.detectorInput.interactable = false;
            if (!row.detectorInput.isFocused)
                row.detectorInput.SetTextWithoutNotify("");
        }
        row.boundTrackId = trackId;
        row.boundDetector = "";
        BindGroupTrailControls(row);
        BindGroupHeaderClick(row, trackId);
        if (img != null && addTargetTrackId == trackId)
            img.color = selectedRowBackground;
    }

    private void ConfigureSensorRow(RowView row, string rank, string planeId, string detector, string rangeType, string range, string yaw, string pitch, string center)
    {
        if (row == null) return;

        // Restore normal row styling
        int visualIndex = row.root.GetSiblingIndex();
        var img = row.root.GetComponent<Image>();
        if (img != null)
        {
            bool selected = selectedRowTrackId == planeId && selectedRowDetector == detector;
            img.color = selected
                ? selectedRowBackground
                : (visualIndex % 2 == 0 ? oddRowBackground : evenRowBackground);
        }

        var le = row.root.GetComponent<LayoutElement>();
        if (le != null) le.preferredHeight = rowHeight;

        if (row.idText != null)
        {
            row.idText.fontSize = rowFontSize;
            row.idText.fontStyle = FontStyle.Normal;
            row.idText.color = textColor;
        }

        if (row.rankText != null) row.rankText.text = rank;
        if (row.idText != null) row.idText.text = planeId;
        if (row.detectorText != null) row.detectorText.text = detector;
        if (row.rangeTypeText != null) row.rangeTypeText.text = rangeType;
        if (row.yawText != null) row.yawText.text = yaw;
        if (row.pitchText != null) row.pitchText.text = pitch;
        if (row.centerText != null) row.centerText.text = center;

        // 传感器可视化配置（仿数据配置面板的颜色/显隐交互）
        row.boundTrackId = planeId;
        row.boundDetector = detector;
        ApplyRowDetectorName(row, detector, RadarDetectionSystem.IsSphericalRadar(rangeType));
        ApplyRowRangeValue(row, range);
        var radar = Radar;
        if (row.colorButton != null)
        {
            row.colorButton.gameObject.SetActive(true);
            row.colorButton.interactable = true;
            if (row.colorSwatch != null)
                row.colorSwatch.color = radar != null
                    ? radar.GetSensorColor(planeId, detector)
                    : DefaultSensorPalette[0];
        }
        if (row.showButton != null)
        {
            row.showButton.gameObject.SetActive(true);
            row.showButton.onClick.RemoveAllListeners();
            row.showButton.onClick.AddListener(() => ToggleSensorVisible(row));
            if (row.showButtonLabel != null)
                row.showButtonLabel.text = radar != null && !radar.IsSensorVisible(planeId, detector) ? "隐" : "显";
        }
        if (row.colorButton != null)
        {
            row.colorButton.onClick.RemoveAllListeners();
            row.colorButton.onClick.AddListener(() => CycleSensorColor(row));
        }
        if (row.clearButton != null)
            row.clearButton.gameObject.SetActive(false);
        BindSensorRowClick(row, planeId, detector);
    }

    private void CycleSensorColor(RowView row)
    {
        var radar = Radar;
        if (radar == null || row == null || row.colorSwatch == null)
            return;

        IReadOnlyList<Color> palette = radar.SensorPalette;
        if (palette == null || palette.Count == 0)
            palette = DefaultSensorPalette;

        Color current = radar.GetSensorColor(row.boundTrackId, row.boundDetector);
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < palette.Count; i++)
        {
            Color d = palette[i] - current;
            float dist = d.r * d.r + d.g * d.g + d.b * d.b;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        Color next = palette[(best + 1) % palette.Count];
        radar.SetSensorColor(row.boundTrackId, row.boundDetector, next);
        row.colorSwatch.color = next;
        var store = PanelMemory.Load();
        radar.CaptureMemory(store);
        PanelMemory.Save(store);
    }

    private void ToggleSensorVisible(RowView row)
    {
        var radar = Radar;
        if (radar == null || row == null || row.showButtonLabel == null)
            return;

        bool visible = !radar.IsSensorVisible(row.boundTrackId, row.boundDetector);
        radar.SetSensorVisible(row.boundTrackId, row.boundDetector, visible);
        row.showButtonLabel.text = visible ? "显" : "隐";
        var store = PanelMemory.Load();
        radar.CaptureMemory(store);
        PanelMemory.Save(store);
    }

    private void OnDropdownChanged(int index)
    {
        if (index >= 0 && index < dropdownTrackIds.Count)
        {
            selectedTrackId = dropdownTrackIds[index];
            Refresh();
        }
    }

    private void UpdateDropdownOptions(List<FlightDataStreamReceiver.TrackTimetableState> states)
    {
        if (planeDropdown == null) return;

        var trackIds = new List<string> { AllPlanesOption };
        if (states != null)
        {
            foreach (var s in states)
            {
                string id = s?.trackId;
                if (!string.IsNullOrWhiteSpace(id) && !trackIds.Contains(id))
                    trackIds.Add(id);
            }
        }

        // 传感器删光了也要保留飞机可选，和列表条目保持一致。
        var live = receiver != null ? receiver.SnapshotStates : null;
        if (live != null)
        {
            foreach (var s in live)
            {
                string id = s?.trackId;
                if (!string.IsNullOrWhiteSpace(id) && !trackIds.Contains(id))
                    trackIds.Add(id);
            }
        }
        if (receiver != null)
        {
            foreach (string key in receiver.RemovedSensorKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                int sep = key.IndexOf('|');
                string planeId = sep > 0 ? key.Substring(0, sep) : key;
                if (!string.IsNullOrWhiteSpace(planeId) && !trackIds.Contains(planeId))
                    trackIds.Add(planeId);
            }
        }

        bool changed = trackIds.Count != dropdownTrackIds.Count;
        if (!changed)
        {
            for (int i = 0; i < trackIds.Count; i++)
            {
                if (trackIds[i] != dropdownTrackIds[i]) { changed = true; break; }
            }
        }
        if (!changed) return;

        dropdownTrackIds.Clear();
        dropdownTrackIds.AddRange(trackIds);

        var options = new List<Dropdown.OptionData>();
        foreach (string id in dropdownTrackIds)
            options.Add(new Dropdown.OptionData(id == AllPlanesOption ? "全部飞机" : id));

        planeDropdown.ClearOptions();
        planeDropdown.AddOptions(options);

        int idx = dropdownTrackIds.IndexOf(selectedTrackId);
        if (idx < 0) { idx = 0; selectedTrackId = AllPlanesOption; }
        planeDropdown.SetValueWithoutNotify(idx);
        planeDropdown.RefreshShownValue();
    }

    // ── Layout creation (mirrors AlarmPanel) ────────────────────

    private void EnsureLayoutObjects()
    {
        RectTransform panel = GetComponent<RectTransform>();
        if (panel == null) return;

        var background = GetComponent<Image>();
        if (background == null)
            background = gameObject.AddComponent<Image>();
        background.color = panelBackground;

        selectorRoot = FindOrCreateRect(panel, "SelectorRoot");
        StretchTop(selectorRoot, selectorHeight, 0f);
        planeDropdown = EnsureDropdown(selectorRoot);

        // 探测距离配置栏（下拉框与表头之间）
        rangeConfigRoot = FindOrCreateRect(panel, "RangeConfigRoot");
        StretchTop(rangeConfigRoot, rangeConfigHeight, selectorHeight);
        EnsureRangeConfigBar(rangeConfigRoot);

        headerRoot = FindOrCreateRect(panel, "HeaderRoot");
        StretchTop(headerRoot, headerHeight, selectorHeight + rangeConfigHeight);
        headerRoot.offsetMax = new Vector2(-scrollbarWidth - 2f, headerRoot.offsetMax.y);

        RectTransform scrollRoot = FindOrCreateRect(panel, "Scroll View");
        StretchFillBelow(scrollRoot, selectorHeight + rangeConfigHeight + headerHeight);
        scrollRoot.offsetMin = new Vector2(0f, footerHeight);
        EnsureScrollArea(scrollRoot);

        EnsureFooterBar();

        selectorRoot.SetAsLastSibling();
    }

    private void EnsureFooterBar()
    {
        RectTransform panel = GetComponent<RectTransform>();
        if (panel == null)
            return;
        var inputBarRoot = FindOrCreateRect(panel, "InputBarRoot");
        StretchBottom(inputBarRoot, footerHeight);
        EnsureSensorInputBar(inputBarRoot);

        var scrollRoot = panel.Find("Scroll View") as RectTransform;
        if (scrollRoot != null)
            scrollRoot.offsetMin = new Vector2(scrollRoot.offsetMin.x, footerHeight);
    }

    // ── 探测距离配置栏（10 变量模型不含距离，距离在此面板配置并即时生效） ──

    private void EnsureRangeConfigBar(RectTransform root)
    {
        var bg = root.GetComponent<Image>();
        if (bg == null)
            bg = root.gameObject.AddComponent<Image>();
        bg.color = headerBackground;

        var horizontal = root.GetComponent<HorizontalLayoutGroup>();
        if (horizontal == null)
            horizontal = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;
        horizontal.spacing = columnSpacing;
        horizontal.padding = new RectOffset(8, 8, 2, 2);

        FindOrCreateBarText(root, "RangeLabel", textColor, "探测距离(m)", 88f, true);
        rangeInput = FindOrCreateRangeInput(root, "RangeInput");
        rangeStatusText = FindOrCreateBarText(root, "RangeStatus", mutedTextColor, RangeStatusDefault, 108f, false);

        rangeInput.onEndEdit.RemoveListener(OnRangeInputEndEdit);
        rangeInput.onEndEdit.AddListener(OnRangeInputEndEdit);

        ApplySavedRangeOnce();
    }

    private Text FindOrCreateBarText(RectTransform parent, string name, Color color, string defaultText, float width, bool setDefault)
    {
        Transform existing = parent.Find(name);
        Text text;
        if (existing != null && existing.TryGetComponent(out text))
        {
            ConfigureBarText(text, color, width);
            if (setDefault && string.IsNullOrEmpty(text.text))
                text.text = defaultText;
            return text;
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        text = go.GetComponent<Text>();
        ConfigureBarText(text, color, width);
        text.text = defaultText;
        return text;
    }

    private void ConfigureBarText(Text text, Color color, float width)
    {
        text.font = font;
        text.fontSize = headerFontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        var le = text.GetComponent<LayoutElement>();
        if (le == null)
            le = text.gameObject.AddComponent<LayoutElement>();
        le.minWidth = width;
        le.preferredWidth = width;
        le.flexibleWidth = 0f;
    }

    private InputField FindOrCreateRangeInput(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        InputField input;
        GameObject go;
        if (existing != null && existing.TryGetComponent(out input))
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(InputField));
            go.transform.SetParent(parent, false);
            input = go.GetComponent<InputField>();
        }

        var bg = go.GetComponent<Image>();
        if (bg == null)
            bg = go.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.13f, 0.95f);

        var le = go.GetComponent<LayoutElement>();
        if (le == null)
            le = go.AddComponent<LayoutElement>();
        le.minWidth = 90f;
        le.flexibleWidth = 1f;

        Text valueText;
        Transform valueT = go.transform.Find("ValueText");
        if (valueT == null || !valueT.TryGetComponent(out valueText))
        {
            var valueGo = new GameObject("ValueText", typeof(RectTransform), typeof(Text));
            valueGo.transform.SetParent(go.transform, false);
            valueText = valueGo.GetComponent<Text>();
        }
        valueText.font = font;
        valueText.fontSize = headerFontSize;
        valueText.color = textColor;
        valueText.alignment = TextAnchor.MiddleLeft;
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        valueText.verticalOverflow = VerticalWrapMode.Truncate;
        valueText.raycastTarget = false;
        valueText.rectTransform.anchorMin = Vector2.zero;
        valueText.rectTransform.anchorMax = Vector2.one;
        valueText.rectTransform.offsetMin = new Vector2(8f, 1f);
        valueText.rectTransform.offsetMax = new Vector2(-8f, -1f);

        Text placeholder;
        Transform phT = go.transform.Find("Placeholder");
        if (phT == null || !phT.TryGetComponent(out placeholder))
        {
            var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            phGo.transform.SetParent(go.transform, false);
            placeholder = phGo.GetComponent<Text>();
        }
        placeholder.font = font;
        placeholder.fontSize = headerFontSize;
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.color = new Color(0.66f, 0.74f, 0.82f, 0.45f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.horizontalOverflow = HorizontalWrapMode.Overflow;
        placeholder.verticalOverflow = VerticalWrapMode.Truncate;
        placeholder.raycastTarget = false;
        if (string.IsNullOrEmpty(placeholder.text))
            placeholder.text = "如 50000";
        placeholder.rectTransform.anchorMin = Vector2.zero;
        placeholder.rectTransform.anchorMax = Vector2.one;
        placeholder.rectTransform.offsetMin = new Vector2(8f, 1f);
        placeholder.rectTransform.offsetMax = new Vector2(-8f, -1f);

        input.targetGraphic = bg;
        input.textComponent = valueText;
        input.placeholder = placeholder;
        input.contentType = InputField.ContentType.Standard;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    private const string SensorRangePrefPrefix = "SensorPanel.SensorRange.";

    private InputField EnsureRowRangeInput(Text rangeText)
    {
        if (rangeText == null)
            return null;
        var go = rangeText.gameObject;
        if (go == null)
            return null;

        // Text 已是 Graphic，不能再往同一物体加 Image，否则 AddComponent 失败、距离栏点不了。
        rangeText.raycastTarget = true;
        rangeText.alignment = TextAnchor.MiddleCenter;
        rangeText.supportRichText = false;

        var input = go.GetComponent<InputField>();
        if (input == null)
            input = go.AddComponent<InputField>();
        if (input == null)
            return null;

        input.textComponent = rangeText;
        input.targetGraphic = rangeText;
        input.contentType = InputField.ContentType.Standard;
        input.lineType = InputField.LineType.SingleLine;
        input.interactable = true;
        return input;
    }

    private InputField EnsureRowNameInput(Text detectorText)
    {
        if (detectorText == null)
            return null;
        var go = detectorText.gameObject;
        if (go == null)
            return null;
        detectorText.raycastTarget = true;
        detectorText.supportRichText = false;
        var input = go.GetComponent<InputField>();
        if (input == null)
            input = go.AddComponent<InputField>();
        if (input == null)
            return null;
        input.textComponent = detectorText;
        input.targetGraphic = detectorText;
        input.contentType = InputField.ContentType.Standard;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    private void ApplyRowDetectorName(RowView row, string detector, bool editable)
    {
        if (row == null)
            return;
        if (row.detectorText != null && (row.detectorInput == null || !row.detectorInput.isFocused))
            row.detectorText.text = detector ?? "";
        if (row.detectorInput == null)
            return;
        row.detectorInput.gameObject.SetActive(true);
        row.detectorInput.interactable = editable;
        if (!row.detectorInput.isFocused)
            row.detectorInput.SetTextWithoutNotify(detector ?? "");
        row.detectorInput.onEndEdit.RemoveAllListeners();
        if (editable)
            row.detectorInput.onEndEdit.AddListener(_ => OnRowNameEndEdit(row));
    }

    private void OnRowNameEndEdit(RowView row)
    {
        if (row == null || row.detectorInput == null || receiver == null)
            return;
        string next = (row.detectorInput.text ?? "").Trim();
        string old = row.boundDetector;
        if (string.IsNullOrEmpty(next) || next == old)
        {
            row.detectorInput.SetTextWithoutNotify(old ?? "");
            return;
        }
        if (!receiver.RenameSensor(row.boundTrackId, old, next))
        {
            ShowHint("名称无效或已存在", new Color(1f, 0.78f, 0f, 1f));
            row.detectorInput.SetTextWithoutNotify(old ?? "");
            return;
        }
        if (Radar != null)
            Radar.TransferSensorKey(row.boundTrackId, old, next);
        string oldPref = SensorRangePrefPrefix + RadarDetectionSystem.MakeSensorKey(row.boundTrackId, old);
        string newPref = SensorRangePrefPrefix + RadarDetectionSystem.MakeSensorKey(row.boundTrackId, next);
        if (PlayerPrefs.HasKey(oldPref))
        {
            PlayerPrefs.SetFloat(newPref, PlayerPrefs.GetFloat(oldPref));
            PlayerPrefs.DeleteKey(oldPref);
            PlayerPrefs.Save();
        }
        row.boundDetector = next;
        ShowHint($"已改名为 {next}", highlightColor);
        Refresh();
    }

    private void ApplyRowRangeValue(RowView row, string range)
    {
        if (row == null)
            return;
        var radar = Radar;
        if (radar != null && !string.IsNullOrEmpty(row.boundTrackId))
        {
            string prefKey = SensorRangePrefPrefix + RadarDetectionSystem.MakeSensorKey(row.boundTrackId, row.boundDetector);
            if (PlayerPrefs.HasKey(prefKey))
                radar.SetSensorRangeMeters(row.boundTrackId, row.boundDetector, PlayerPrefs.GetFloat(prefKey));
            range = FormatRange(radar.GetSensorRangeMeters(row.boundTrackId, row.boundDetector));
        }

        if (row.rangeInput != null)
        {
            row.rangeInput.gameObject.SetActive(true);
            if (!row.rangeInput.isFocused)
                row.rangeInput.SetTextWithoutNotify(range ?? "");
            row.rangeInput.onEndEdit.RemoveAllListeners();
            row.rangeInput.onEndEdit.AddListener(_ => OnRowRangeEndEdit(row));
        }
        else if (row.rangeText != null)
            row.rangeText.text = range;
    }

    private void OnRowRangeEndEdit(RowView row)
    {
        if (row == null || row.rangeInput == null)
            return;
        var radar = Radar;
        if (radar == null)
            return;
        if (!TryParseRangeMeters(row.rangeInput.text, out float meters) || meters < 0f)
        {
            row.rangeInput.SetTextWithoutNotify(FormatRange(radar.GetSensorRangeMeters(row.boundTrackId, row.boundDetector)));
            return;
        }
        radar.SetSensorRangeMeters(row.boundTrackId, row.boundDetector, meters);
        PlayerPrefs.SetFloat(SensorRangePrefPrefix + RadarDetectionSystem.MakeSensorKey(row.boundTrackId, row.boundDetector), meters);
        PlayerPrefs.Save();
        var store = PanelMemory.Load();
        radar.CaptureMemory(store);
        PanelMemory.Save(store);
        row.rangeInput.SetTextWithoutNotify(FormatRange(meters));
    }

    private void OnRangeInputEndEdit(string text)
    {
        var radar = Radar;
        if (radar == null || rangeStatusText == null)
            return;

        if (!TryParseRangeMeters(text, out float meters) || meters < 0f)
        {
            rangeStatusText.text = "输入无效";
            rangeStatusText.color = new Color(1f, 0.35f, 0.35f, 1f);
            rangeStatusUntil = Time.unscaledTime + 2.5f;
            if (rangeInput != null)
                rangeInput.text = FormatRange(radar.MaxDetectRangeMeters);
            return;
        }

        radar.SetMaxDetectRangeMeters(meters);
        PlayerPrefs.SetFloat(MaxDetectRangePrefKey, meters);
        rangeStatusText.text = $"已应用 {meters:F0}m";
        rangeStatusText.color = highlightColor;
        rangeStatusUntil = Time.unscaledTime + 2.5f;
    }

    private void ApplySavedRangeOnce()
    {
        if (rangePrefApplied || !Application.isPlaying)
            return;

        var radar = Radar;
        if (radar == null)
            return;

        rangePrefApplied = true;

        // 上次在面板里配置的距离跨次生效（未配置过则用雷达 Inspector 默认值）
        if (PlayerPrefs.HasKey(MaxDetectRangePrefKey))
            radar.SetMaxDetectRangeMeters(PlayerPrefs.GetFloat(MaxDetectRangePrefKey, radar.MaxDetectRangeMeters));

        if (rangeInput != null && string.IsNullOrEmpty(rangeInput.text))
            rangeInput.text = FormatRange(radar.MaxDetectRangeMeters);
    }

    private void UpdateRangeStatusExpiry()
    {
        if (rangeStatusText == null)
            return;
        if (rangeStatusUntil > 0f && Time.unscaledTime > rangeStatusUntil)
        {
            rangeStatusUntil = 0f;
            rangeStatusText.text = RangeStatusDefault;
            rangeStatusText.color = mutedTextColor;
        }
    }

    private static bool TryParseRangeMeters(string raw, out float meters)
    {
        meters = 0f;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        string s = raw.Trim().Replace("米", string.Empty).Replace("m", string.Empty).Replace("M", string.Empty).Trim();
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out meters)
            || float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out meters);
    }

    private static string FormatRange(float meters)
    {
        return meters >= 1000f ? ((int)Mathf.Round(meters)).ToString() : meters.ToString("0.#");
    }

    private Dropdown EnsureDropdown(RectTransform root)
    {
        RectTransform dropdownRect = FindOrCreateRect(root, "PlaneSelector");
        StretchFull(dropdownRect);

        var image = dropdownRect.GetComponent<Image>();
        if (image == null)
            image = dropdownRect.gameObject.AddComponent<Image>();
        image.color = new Color(0.1f, 0.13f, 0.16f, 0.95f);

        var dropdown = dropdownRect.GetComponent<Dropdown>();
        if (dropdown == null)
            dropdown = dropdownRect.gameObject.AddComponent<Dropdown>();

        Text label = EnsureText(dropdownRect, "Label", textColor, TextAnchor.MiddleLeft);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(8f, 0f);
        label.rectTransform.offsetMax = new Vector2(-28f, 0f);
        label.text = "全部飞机";

        Text arrow = EnsureText(dropdownRect, "Arrow", mutedTextColor, TextAnchor.MiddleCenter);
        arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
        arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
        arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
        arrow.rectTransform.offsetMin = new Vector2(-26f, 0f);
        arrow.rectTransform.offsetMax = Vector2.zero;
        arrow.text = "v";

        RectTransform template = EnsureDropdownTemplate(dropdownRect);
        dropdown.targetGraphic = image;
        dropdown.captionText = label;
        dropdown.template = template;
        dropdown.itemText = template.Find("Viewport/Content/Item/Item Label")?.GetComponent<Text>();
        return dropdown;
    }

    private RectTransform EnsureDropdownTemplate(RectTransform dropdownRect)
    {
        RectTransform template = FindOrCreateRect(dropdownRect, "Template");
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.offsetMin = new Vector2(0f, -Mathf.Max(120f, rowHeight * 5f));
        template.offsetMax = Vector2.zero;
        template.SetAsLastSibling();
        template.gameObject.SetActive(false);

        var templateCanvas = template.GetComponent<Canvas>();
        if (templateCanvas == null)
            templateCanvas = template.gameObject.AddComponent<Canvas>();
        templateCanvas.overrideSorting = true;
        templateCanvas.sortingOrder = 100;
        if (template.GetComponent<GraphicRaycaster>() == null)
            template.gameObject.AddComponent<GraphicRaycaster>();

        var templateImage = template.GetComponent<Image>();
        if (templateImage == null)
            templateImage = template.gameObject.AddComponent<Image>();
        templateImage.color = new Color(0.08f, 0.1f, 0.13f, 0.98f);

        var scrollRect = template.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = template.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        RectTransform viewport = FindOrCreateRect(template, "Viewport");
        StretchFull(viewport);
        viewport.offsetMax = new Vector2(-scrollbarWidth - 2f, 0f);
        var viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null)
            viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        var mask = viewport.GetComponent<Mask>();
        if (mask == null)
            mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform content = FindOrCreateRect(viewport, "Content");
        StretchContentTop(content);
        content.sizeDelta = new Vector2(0f, rowHeight);
        EnsureVerticalLayout(content, true);

        RectTransform item = FindOrCreateRect(content, "Item");
        item.anchorMin = new Vector2(0f, 1f);
        item.anchorMax = new Vector2(1f, 1f);
        item.pivot = new Vector2(0.5f, 1f);
        item.anchoredPosition = Vector2.zero;
        item.sizeDelta = new Vector2(0f, rowHeight);
        var toggle = item.GetComponent<Toggle>();
        if (toggle == null)
            toggle = item.gameObject.AddComponent<Toggle>();
        var itemImage = item.GetComponent<Image>();
        if (itemImage == null)
            itemImage = item.gameObject.AddComponent<Image>();
        itemImage.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        toggle.targetGraphic = itemImage;

        Text itemLabel = EnsureText(item, "Item Label", textColor, TextAnchor.MiddleLeft);
        itemLabel.rectTransform.anchorMin = Vector2.zero;
        itemLabel.rectTransform.anchorMax = Vector2.one;
        itemLabel.rectTransform.offsetMin = new Vector2(8f, 0f);
        itemLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);
        toggle.graphic = null;

        var itemLayout = item.GetComponent<LayoutElement>();
        if (itemLayout == null)
            itemLayout = item.gameObject.AddComponent<LayoutElement>();
        itemLayout.minHeight = rowHeight;
        itemLayout.preferredHeight = rowHeight;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.verticalScrollbar = EnsureVerticalScrollbar(template);
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalScrollbarSpacing = 2f;
        return template;
    }

    private Text EnsureText(RectTransform parent, string name, Color color, TextAnchor alignment)
    {
        RectTransform rect = FindOrCreateRect(parent, name);
        var text = rect.GetComponent<Text>();
        if (text == null)
            text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = rowFontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private void EnsureScrollArea(RectTransform scrollRoot)
    {
        var scrollImage = scrollRoot.GetComponent<Image>();
        if (scrollImage == null)
            scrollImage = scrollRoot.gameObject.AddComponent<Image>();
        scrollImage.color = new Color(0f, 0f, 0f, 0f);
        scrollImage.raycastTarget = true;

        RectTransform viewport = FindOrCreateRect(scrollRoot, "Viewport");
        StretchViewport(viewport);

        var viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null)
            viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;

        var mask = viewport.GetComponent<Mask>();
        if (mask == null)
            mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        contentRoot = FindOrCreateRect(viewport, "ContentRoot");
        StretchContentTop(contentRoot);

        var scrollRect = scrollRoot.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = contentRoot;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        scrollRect.verticalScrollbar = EnsureVerticalScrollbar(scrollRoot);
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalScrollbarSpacing = 2f;
    }

    private Scrollbar EnsureVerticalScrollbar(RectTransform scrollRoot)
    {
        RectTransform scrollbarRoot = FindOrCreateRect(scrollRoot, "Vertical Scrollbar");
        scrollbarRoot.anchorMin = new Vector2(1f, 0f);
        scrollbarRoot.anchorMax = new Vector2(1f, 1f);
        scrollbarRoot.pivot = new Vector2(1f, 0.5f);
        scrollbarRoot.offsetMin = new Vector2(-scrollbarWidth, 0f);
        scrollbarRoot.offsetMax = Vector2.zero;

        var bg = scrollbarRoot.GetComponent<Image>();
        if (bg == null)
            bg = scrollbarRoot.gameObject.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.05f, 0.07f, 0.65f);

        RectTransform slidingArea = FindOrCreateRect(scrollbarRoot, "Sliding Area");
        StretchFull(slidingArea);
        slidingArea.offsetMin = new Vector2(2f, 2f);
        slidingArea.offsetMax = new Vector2(-2f, -2f);

        RectTransform handle = FindOrCreateRect(slidingArea, "Handle");
        StretchFull(handle);

        var handleImage = handle.GetComponent<Image>();
        if (handleImage == null)
            handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.43f, 0.82f, 1f, 0.9f);

        var scrollbar = scrollbarRoot.GetComponent<Scrollbar>();
        if (scrollbar == null)
            scrollbar = scrollbarRoot.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.transition = Selectable.Transition.ColorTint;
        scrollbar.size = 1f;
        scrollbar.value = 1f;
        return scrollbar;
    }

    private void ApplyHeaderViewportInset()
    {
        if (headerRoot != null)
            headerRoot.offsetMax = new Vector2(-scrollbarWidth - 2f, headerRoot.offsetMax.y);
    }

    private static RectTransform FindOrCreateRect(RectTransform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
            return existingRect;
        var obj = new GameObject(childName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    private static void StretchTop(RectTransform rect, float height, float topOffset)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -topOffset - height);
        rect.offsetMax = new Vector2(0f, -topOffset);
    }

    private static void StretchFillBelow(RectTransform rect, float topOffset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(0f, -topOffset);
    }

    private static void StretchBottom(RectTransform rect, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(0f, height);
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void StretchViewport(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(-scrollbarWidth - 2f, 0f);
    }

    private static void StretchContentTop(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private void EnsureVerticalLayout(RectTransform root, bool fitHeight)
    {
        if (root == null) return;
        var vertical = root.GetComponent<VerticalLayoutGroup>();
        if (vertical == null)
            vertical = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = false;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.spacing = 2f;

        var fitter = root.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = fitHeight ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
    }

    private void EnsureRowCount(int count)
    {
        while (rows.Count < count)
        {
            int index = rows.Count;
            Color bg = index % 2 == 0 ? oddRowBackground : evenRowBackground;
            string rowName = $"SensorRow_{index + 1}";
            RectTransform template = PanelSceneLayout.FindRowTemplate(contentRoot, rowTemplate);
            if (template == null && rows.Count > 0)
                template = rows[0].root;
            RectTransform target = null;
            if (preserveSceneLayout && index == 0 && template != null)
            {
                target = template;
                target.gameObject.SetActive(true);
                PanelSceneLayout.FreezeRowLayout(target);
            }
            else
                target = PanelSceneLayout.GetOrCloneRow(contentRoot, rowName, preserveSceneLayout ? template : null);
            if (target != null)
                rows.Add(BindRow(target, bg, rowFontSize, false, true));
            else
                rows.Add(CreateRow(contentRoot, rowName, bg, rowFontSize));
        }
    }

    private void SetActiveRowCount(int count)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].root != null)
                rows[i].root.gameObject.SetActive(i < count);
        }
    }

    private RowView FindOrCreateHeaderRow()
    {
        if (headerRoot != null)
        {
            Transform existing = headerRoot.Find("Header");
            if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
                return BindRow(existingRect, headerBackground, headerFontSize, true, true);
        }
        return CreateRow(headerRoot, "Header", headerBackground, headerFontSize, true);
    }

    private RowView CreateRow(RectTransform parentRoot, string rowName, Color background, int fontSize, bool header = false)
    {
        var rowObject = new GameObject(rowName, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObject.transform.SetParent(parentRoot != null ? parentRoot : transform, false);
        return BindRow(rowObject.GetComponent<RectTransform>(), background, fontSize, header, false);
    }

    private RowView BindRow(RectTransform rect, Color background, int fontSize, bool header, bool preserveExistingHeaderCells)
    {
        GameObject rowObject = rect.gameObject;

        if (!preserveExistingHeaderCells)
        {
            if (header)
                StretchFull(rect);
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, rowHeight);
            }
        }

        var image = rowObject.GetComponent<Image>();
        if (image == null)
            image = rowObject.AddComponent<Image>();
        image.color = background;

        var layoutElement = rowObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = header;
        layoutElement.minHeight = header ? 0f : rowHeight;
        layoutElement.preferredHeight = header ? 0f : rowHeight;

        var horizontal = rowObject.GetComponent<HorizontalLayoutGroup>();
        if (preserveExistingHeaderCells || preserveSceneLayout)
        {
            PanelSceneLayout.FreezeRowLayout(rect);
        }
        else if (!preserveExistingHeaderCells)
        {
            if (horizontal == null)
                horizontal = rowObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.enabled = true;
            












































































































































































































































































































































































































































































































































































































































































































































































            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;
            horizontal.spacing = columnSpacing;
            horizontal.padding = new RectOffset(8, 8, 2, 2);
        }

        var rowView = new RowView
        {
            root = rect,
            rankText = FindOrCreateCell(rowObject.transform, "Rank", header ? mutedTextColor : accentTextColor, fontSize, 24f, 0f, preserveExistingHeaderCells),
            idText = FindOrCreateCell(rowObject.transform, "PlaneId", textColor, fontSize, 72f, 0f, preserveExistingHeaderCells),
            detectorText = FindOrCreateCell(rowObject.transform, "Detector", textColor, fontSize, 62f, 0f, preserveExistingHeaderCells),
            rangeTypeText = FindOrCreateCell(rowObject.transform, "RangeType", mutedTextColor, fontSize, 42f, 0f, preserveExistingHeaderCells),
            rangeText = FindOrCreateCell(rowObject.transform, "Range", mutedTextColor, fontSize, 58f, 0f, preserveExistingHeaderCells),
            yawText = FindOrCreateCell(rowObject.transform, "Yaw", mutedTextColor, fontSize, 40f, 0f, preserveExistingHeaderCells),
            pitchText = FindOrCreateCell(rowObject.transform, "Pitch", mutedTextColor, fontSize, 40f, 0f, preserveExistingHeaderCells),
            centerText = FindOrCreateCell(rowObject.transform, "Center", mutedTextColor, fontSize, 34f, 0f, preserveExistingHeaderCells),
        };

        BindSensorControls(rowView, rowObject.transform, header);
        if (!header)
        {
            if (rowView.rangeText == null)
            {
                var rangeTr = rowObject.transform.Find("Range");
                if (rangeTr != null)
                {
                    if (!rangeTr.TryGetComponent(out rowView.rangeText))
                        rowView.rangeText = rangeTr.gameObject.AddComponent<Text>();
                }
            }
            rowView.rangeInput = EnsureRowRangeInput(rowView.rangeText);
            rowView.detectorInput = EnsureRowNameInput(rowView.detectorText);
        }
        HideCrowdedSensorCells(rowView);

        return rowView;
    }

    private static void HideCrowdedSensorCells(RowView row)
    {
        if (row == null)
            return;
        if (row.rangeTypeText != null)
            row.rangeTypeText.gameObject.SetActive(false);
    }

    private void BindSensorControls(RowView rv, Transform parent, bool header)
    {
        rv.colorButton = FindOrCreateColorButton(parent, "SensorColor", 26f);
        rv.colorSwatch = rv.colorButton != null ? rv.colorButton.GetComponent<Image>() : null;
        if (rv.colorButton != null)
        {
            rv.colorButton.onClick.RemoveAllListeners();
            if (!header)
                rv.colorButton.onClick.AddListener(() => CycleSensorColor(rv));
        }

        rv.showButton = FindOrCreateShowButton(parent, "SensorShow", 26f, out rv.showButtonLabel);
        if (rv.showButton != null)
        {
            rv.showButton.onClick.RemoveAllListeners();
            if (!header)
                rv.showButton.onClick.AddListener(() => ToggleSensorVisible(rv));
        }

        rv.clearButton = FindOrCreateClearButton(parent, "SensorClear", rv.showButton, out rv.clearButtonLabel);
        if (rv.clearButton != null)
        {
            rv.clearButton.onClick.RemoveAllListeners();
            rv.clearButton.gameObject.SetActive(header);
        }

        if (header)
        {
            if (rv.colorButton != null)
            {
                rv.colorButton.gameObject.SetActive(true);
                rv.colorButton.interactable = false;
                if (rv.colorSwatch != null)
                    rv.colorSwatch.color = headerBackground;
                var colorLabel = EnsureText(rv.colorButton.GetComponent<RectTransform>(), "Label", mutedTextColor, TextAnchor.MiddleCenter);
                colorLabel.text = "颜色";
                colorLabel.fontSize = headerFontSize;
                colorLabel.raycastTarget = false;
            }
            if (rv.showButton != null)
                rv.showButton.gameObject.SetActive(true);
            if (rv.showButtonLabel != null)
                rv.showButtonLabel.text = "显示";
            if (rv.clearButton != null)
            {
                rv.clearButton.gameObject.SetActive(true);
                rv.clearButton.interactable = false;
            }
            if (rv.clearButtonLabel != null)
                rv.clearButtonLabel.text = "清迹";
        }
    }

    private void BindGroupTrailControls(RowView row)
    {
        if (row == null)
            return;
        var planes = Planes;
        if (row.colorButton != null)
        {
            row.colorButton.gameObject.SetActive(true);
            row.colorButton.interactable = true;
            row.colorButton.onClick.RemoveAllListeners();
            row.colorButton.onClick.AddListener(() => CycleTrailColor(row));
            if (row.colorSwatch != null)
                row.colorSwatch.color = planes != null ? planes.GetTrailColor(row.boundTrackId) : DefaultSensorPalette[0];
        }
        if (row.showButton != null)
        {
            row.showButton.gameObject.SetActive(true);
            row.showButton.interactable = true;
            row.showButton.onClick.RemoveAllListeners();
            row.showButton.onClick.AddListener(() => ToggleTrailVisible(row));
            if (row.showButtonLabel != null)
                row.showButtonLabel.text = planes != null && !planes.IsPlaneTrailVisible(row.boundTrackId) ? "隐" : "显";
        }
        if (row.clearButton != null)
        {
            row.clearButton.gameObject.SetActive(true);
            row.clearButton.interactable = true;
            row.clearButton.onClick.RemoveAllListeners();
            row.clearButton.onClick.AddListener(() => ClearPlaneTrail(row));
        }
        if (row.clearButtonLabel != null)
            row.clearButtonLabel.text = "清";
    }

    private void CycleTrailColor(RowView row)
    {
        var planes = Planes;
        if (planes == null || row == null || row.colorSwatch == null)
            return;
        IReadOnlyList<Color> palette = planes.TrailPalette;
        if (palette == null || palette.Count == 0)
            palette = DefaultSensorPalette;
        Color current = planes.GetTrailColor(row.boundTrackId);
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < palette.Count; i++)
        {
            Color d = palette[i] - current;
            float dist = d.r * d.r + d.g * d.g + d.b * d.b;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }
        Color next = palette[(best + 1) % palette.Count];
        planes.SetTrailColor(row.boundTrackId, next);
        row.colorSwatch.color = next;
        var store = PanelMemory.Load();
        planes.CaptureMemory(store);
        PanelMemory.Save(store);
    }

    private void ToggleTrailVisible(RowView row)
    {
        var planes = Planes;
        if (planes == null || row == null)
            return;
        bool visible = !planes.IsPlaneTrailVisible(row.boundTrackId);
        planes.SetPlaneTrailVisible(row.boundTrackId, visible);
        if (row.showButtonLabel != null)
            row.showButtonLabel.text = visible ? "显" : "隐";
        var store = PanelMemory.Load();
        planes.CaptureMemory(store);
        PanelMemory.Save(store);
    }

    private void ClearPlaneTrail(RowView row)
    {
        if (row == null || Planes == null)
            return;
        Planes.ClearTrail(row.boundTrackId);
    }

    private Button FindOrCreateClearButton(Transform parent, string name, Button showButton, out Text label)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing != null)
            go = existing.gameObject;
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            if (showButton != null)
            {
                var showRt = showButton.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(showRt.anchoredPosition.x + showRt.sizeDelta.x + 4f, showRt.anchoredPosition.y);
                rt.sizeDelta = showRt.sizeDelta.sqrMagnitude > 1f ? showRt.sizeDelta : new Vector2(28f, 20f);
            }
            else
            {
                rt.anchoredPosition = new Vector2(310f, 0f);
                rt.sizeDelta = new Vector2(28f, 20f);
            }
        }

        var img = go.GetComponent<Image>();
        if (img == null)
            img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);
        var btn = go.GetComponent<Button>();
        if (btn == null)
            btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        label = EnsureText(go.GetComponent<RectTransform>(), "Label", textColor, TextAnchor.MiddleCenter);
        if (label != null)
        {
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.fontSize = 11;
            label.raycastTarget = false;
            if (string.IsNullOrEmpty(label.text))
                label.text = "清";
        }
        return btn;
    }

    private Button FindOrCreateColorButton(Transform parent, string name, float width)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing != null)
            go = existing.gameObject;
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Button));
            go.transform.SetParent(parent, false);
        }

        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width; le.minWidth = width; le.flexibleWidth = 0f;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    private Button FindOrCreateShowButton(Transform parent, string name, float width, out Text label)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing != null)
            go = existing.gameObject;
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Button));
            go.transform.SetParent(parent, false);
        }

        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width; le.minWidth = width; le.flexibleWidth = 0f;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);
        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        label = EnsureText(go.GetComponent<RectTransform>(), "Label", textColor, TextAnchor.MiddleCenter);
        if (label != null)
        {
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.fontSize = 12;
            label.raycastTarget = false;
        }
        return btn;
    }

    private Text FindOrCreateCell(Transform parent, string name, Color color, int fontSize, float width, float flexibleWidth, bool preserveExisting)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out Text existingText))
        {
            if (!preserveExisting)
                ConfigureCell(existingText, color, fontSize, width, flexibleWidth);
            return existingText;
        }
        return CreateCell(parent, name, color, fontSize, width, flexibleWidth);
    }

    private Text CreateCell(Transform parent, string name, Color color, int fontSize, float width, float flexibleWidth)
    {
        var cellObject = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        cellObject.transform.SetParent(parent, false);
        var text = cellObject.GetComponent<Text>();
        ConfigureCell(text, color, fontSize, width, flexibleWidth);
        return text;
    }

    private void ConfigureCell(Text text, Color color, int fontSize, float width, float flexibleWidth)
    {
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        var layoutElement = text.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = text.gameObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.flexibleWidth = flexibleWidth;
    }


    private static readonly string[] SensorTypeChoices =
    {
        "雷达", "光电", "红外", "电子侦察", "球形雷达",
    };

    private void BindSensorRowClick(RowView row, string trackId, string detector)
    {
        if (row == null || row.root == null)
            return;
        var img = row.root.GetComponent<Image>();
        if (img != null)
            img.raycastTarget = true;
        var btn = row.root.GetComponent<Button>();
        if (btn == null)
            btn = row.root.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;
        btn.onClick.RemoveAllListeners();
        string capturedTrack = trackId;
        string capturedDetector = detector;
        btn.onClick.AddListener(() => SelectSensorRow(capturedTrack, capturedDetector));
    }

    private void BindGroupHeaderClick(RowView row, string trackId)
    {
        if (row == null || row.root == null || string.IsNullOrEmpty(trackId))
            return;
        var img = row.root.GetComponent<Image>();
        if (img != null)
            img.raycastTarget = true;
        var btn = row.root.GetComponent<Button>();
        if (btn == null)
            btn = row.root.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;
        btn.onClick.RemoveAllListeners();
        string captured = trackId;
        btn.onClick.AddListener(() => SelectAddTarget(captured));
    }

    private void SelectSensorRow(string trackId, string detector)
    {
        selectedRowTrackId = trackId;
        selectedRowDetector = detector;
        Refresh();
    }

    private void SelectAddTarget(string trackId)
    {
        addTargetTrackId = trackId;
        Refresh();
    }

    private void ShowHint(string msg, Color color)
    {
    }

    private List<string> CollectKnownTrackIds()
    {
        var ids = new List<string>();
        if (receiver != null)
        {
            ids.AddRange(receiver.SnapshotTrackIds());
            var states = receiver.SnapshotSensors;
            if (states != null)
            {
                foreach (var s in states)
                {
                    if (s != null && !string.IsNullOrWhiteSpace(s.trackId) && !ids.Contains(s.trackId))
                        ids.Add(s.trackId);
                }
            }
        }
        ids.RemoveAll(id => string.IsNullOrWhiteSpace(id) || id == AllPlanesOption);
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    private List<string> ResolveAddTargets()
    {
        var all = CollectKnownTrackIds();
        if (batchMode)
            return all;
        if (selectedTrackId != AllPlanesOption && !string.IsNullOrWhiteSpace(selectedTrackId))
            return new List<string> { selectedTrackId };
        if (!string.IsNullOrWhiteSpace(addTargetTrackId) && addTargetTrackId != AllPlanesOption)
            return new List<string> { addTargetTrackId };
        return all.Count > 0 ? new List<string> { all[0] } : new List<string>();
    }

    private FlightDataStreamReceiver.TrackTimetableState FindTemplate(string detector)
    {
        if (receiver == null || string.IsNullOrWhiteSpace(detector))
            return null;
        var states = receiver.SnapshotSensors;
        if (states != null)
        {
            foreach (var s in states)
            {
                if (s != null && string.Equals(s.detectorType, detector, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
        }
        states = receiver.SnapshotStates;
        if (states != null)
        {
            foreach (var s in states)
            {
                if (s != null && string.Equals(s.detectorType, detector, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
        }
        return null;
    }

    private FlightDataStreamReceiver.TrackTimetableState BuildSensorTemplate(string detector)
    {
        bool addSpherical = detector == "球形雷达";
        if (!addSpherical)
        {
            var existing = FindTemplate(detector);
            if (existing != null)
                return existing;
        }
        float range = Radar != null ? Radar.MaxDetectRangeMeters : 50000f;
        bool spherical = addSpherical;
        return new FlightDataStreamReceiver.TrackTimetableState
        {
            detectorType = detector,
            rangeType = spherical ? "球形" : "锥形",
            detectRange = range,
            yawAngle = 0f,
            pitchAngle = 0f,
            yawScanAngle = spherical ? 360f : 40f,
            pitchScanAngle = spherical ? 360f : 40f,
            centerPoint = "中",
            centerPoint1 = "中",
            radarStatus = "有效",
        };
    }

    private string UniqueSphericalName(string trackId)
    {
        string name = "球形雷达";
        if (receiver == null)
            return name;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var states = receiver.SnapshotSensors;
        if (states != null)
        {
            foreach (var s in states)
            {
                if (s != null && s.trackId == trackId && !string.IsNullOrWhiteSpace(s.detectorType))
                    used.Add(s.detectorType.Trim());
            }
        }
        if (!used.Contains(name))
            return name;
        for (int i = 2; i < 100; i++)
        {
            string next = name + i;
            if (!used.Contains(next))
                return next;
        }
        return name + Time.frameCount;
    }

    private void OnAddSensorClicked()
    {
        var matches = new List<string>(SensorTypeChoices);
        var states = receiver != null ? receiver.SnapshotSensors : null;
        if (states != null)
        {
            foreach (var s in states)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.detectorType))
                    continue;
                if (!matches.Contains(s.detectorType))
                    matches.Add(s.detectorType);
            }
        }
        ShowSensorSuggestion(matches, "选择要添加的传感器类型");
    }

    private void AddSensorOfType(string detector)
    {
        if (receiver == null || string.IsNullOrWhiteSpace(detector))
            return;
        receiver.SeedSensorsIfEmpty(receiver.SnapshotStates);
        var targets = ResolveAddTargets();
        if (targets.Count == 0)
        {
            ShowHint("没有可添加的飞机", new Color(1f, 0.78f, 0f, 1f));
            return;
        }
        var template = BuildSensorTemplate(detector);
        int added = 0;
        foreach (string trackId in targets)
        {
            if (RadarDetectionSystem.IsSphericalRadar(template.rangeType))
                template.detectorType = UniqueSphericalName(trackId);
            if (receiver.AddSensor(trackId, template))
                added++;
        }
        if (added == 0)
        {
            ShowHint($"{detector} 已存在", new Color(1f, 0.78f, 0f, 1f));
            return;
        }
        ShowHint(batchMode && targets.Count > 1
            ? $"已为 {added} 架飞机添加 {detector}"
            : $"已添加传感器 {detector}", highlightColor);
        Refresh();
    }

    private void OnDeleteSensorClicked()
    {
        if (string.IsNullOrEmpty(selectedRowTrackId) || string.IsNullOrEmpty(selectedRowDetector))
        {
            ShowHint("请先点击要删除的传感器行", new Color(1f, 0.78f, 0f, 1f));
            return;
        }
        if (receiver == null)
            return;
        receiver.SeedSensorsIfEmpty(receiver.SnapshotStates);
        int n = 0;
        if (batchMode)
        {
            var ids = CollectKnownTrackIds();
            foreach (string id in ids)
            {
                if (receiver.RemoveSensor(id, selectedRowDetector))
                    n++;
            }
            ShowHint($"已从 {n} 架飞机删除 {selectedRowDetector}", mutedTextColor);
        }
        else if (receiver.RemoveSensor(selectedRowTrackId, selectedRowDetector))
        {
            ShowHint($"已删除 {selectedRowDetector}", mutedTextColor);
        }
        else
        {
            ShowHint("删除失败", new Color(1f, 0.35f, 0.35f, 1f));
        }
        selectedRowTrackId = null;
        selectedRowDetector = null;
        Refresh();
    }

    private void OnBatchToggleChanged(bool on)
    {
        batchMode = on;
        ShowHint(on ? "批量：所有飞机同时添加/删除" : "单架：仅当前飞机添加/删除", accentTextColor);
    }

    private void BindAddDeleteButtons()
    {
        Button addBtn = null;
        Transform footer = transform.Find("InputBarRoot");
        var buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            if (b == null)
                continue;
            string n = b.gameObject.name;
            var label = b.GetComponentInChildren<Text>(true);
            string t = label != null ? label.text : "";
            if (n == "DeleteBtn" || t.Contains("删除行") || t.Contains("删除传感器"))
                continue;
            if (n == "AddBtn" || n.Contains("添加") || t.Contains("添加行") || t.Contains("添加传感器") || t.Contains("添加"))
            {
                addBtn = b;
                footer = b.transform.parent;
                break;
            }
        }
        if (footer == null)
            footer = transform;

        if (addBtn == null)
        {
            var go = new GameObject("AddBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(footer, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.15f, 0.45f, 0.75f, 0.95f);
            addBtn = go.GetComponent<Button>();
            addBtn.targetGraphic = img;
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var lt = labelGo.GetComponent<Text>();
            if (font != null) lt.font = font;
            lt.fontSize = rowFontSize; lt.color = Color.white;
            lt.alignment = TextAnchor.MiddleCenter;
            lt.text = "+ 添加传感器";
            lt.raycastTarget = false;
        }
        var addRt = addBtn.GetComponent<RectTransform>();
        addRt.anchorMin = new Vector2(0f, 0.5f);
        addRt.anchorMax = new Vector2(0f, 0.5f);
        addRt.pivot = new Vector2(0f, 0.5f);
        addRt.anchoredPosition = new Vector2(8f, 0f);
        addRt.sizeDelta = new Vector2(110f, 28f);
        addBtn.onClick.RemoveAllListeners();
        addBtn.onClick.AddListener(OnAddSensorClicked);
        var addLabel = addBtn.GetComponentInChildren<Text>(true);
        if (addLabel != null)
            addLabel.text = "+ 添加传感器";

        Button deleteBtn = null;
        var existing = footer.Find("DeleteBtn");
        if (existing != null)
            deleteBtn = existing.GetComponent<Button>();
        if (deleteBtn == null)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var label = buttons[i].GetComponentInChildren<Text>(true);
                if (label != null && (label.text.Contains("删除行") || label.text.Contains("删除传感器")))
                {
                    deleteBtn = buttons[i];
                    break;
                }
            }
        }
        if (deleteBtn == null)
        {
            var go = new GameObject("DeleteBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(footer, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.55f, 0.18f, 0.18f, 0.95f);
            deleteBtn = go.GetComponent<Button>();
            deleteBtn.targetGraphic = img;
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var lt = labelGo.GetComponent<Text>();
            if (font != null) lt.font = font;
            lt.fontSize = rowFontSize; lt.color = Color.white;
            lt.alignment = TextAnchor.MiddleCenter;
            lt.text = "删除行";
            lt.raycastTarget = false;
        }
        var delRt = deleteBtn.GetComponent<RectTransform>();
        delRt.anchorMin = new Vector2(1f, 0.5f);
        delRt.anchorMax = new Vector2(1f, 0.5f);
        delRt.pivot = new Vector2(1f, 0.5f);
        delRt.anchoredPosition = new Vector2(-8f, 0f);
        delRt.sizeDelta = new Vector2(88f, 28f);
        deleteBtn.onClick.RemoveAllListeners();
        deleteBtn.onClick.AddListener(OnDeleteSensorClicked);
        var delLabel = deleteBtn.GetComponentInChildren<Text>(true);
        if (delLabel != null)
            delLabel.text = "删除行";

        EnsureBatchToggle(footer);
        HideHint(footer);
        var hlg = footer.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
            hlg.enabled = false;
    }

    private static void HideHint(Transform footer)
    {
        if (footer == null)
            return;
        for (int i = footer.childCount - 1; i >= 0; i--)
        {
            var child = footer.GetChild(i);
            if (child != null && child.name.StartsWith("Hint"))
                child.gameObject.SetActive(false);
        }
    }

    private void EnsureBatchToggle(Transform footer)
    {
        if (footer == null)
            return;
        Transform existing = footer.Find("BatchToggle");
        GameObject go = existing != null ? existing.gameObject : new GameObject("BatchToggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
        if (existing == null)
            go.transform.SetParent(footer, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-102f, 0f);
        rt.sizeDelta = new Vector2(88f, 24f);

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = new Color(0.1f, 0.13f, 0.16f, 0.95f);

        var toggle = go.GetComponent<Toggle>();
        if (toggle == null) toggle = go.AddComponent<Toggle>();
        batchToggle = toggle;
        toggle.targetGraphic = img;

        Transform checkT = go.transform.Find("Checkmark");
        GameObject checkGo = checkT != null ? checkT.gameObject : new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        if (checkT == null)
            checkGo.transform.SetParent(go.transform, false);
        var checkRt = checkGo.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0f, 0.5f);
        checkRt.anchorMax = new Vector2(0f, 0.5f);
        checkRt.pivot = new Vector2(0f, 0.5f);
        checkRt.anchoredPosition = new Vector2(6f, 0f);
        checkRt.sizeDelta = new Vector2(14f, 14f);
        var checkImg = checkGo.GetComponent<Image>();
        if (checkImg == null) checkImg = checkGo.AddComponent<Image>();
        checkImg.color = accentTextColor;
        toggle.graphic = checkImg;

        var label = EnsureText(rt, "Label", textColor, TextAnchor.MiddleLeft);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(24f, 0f);
        label.rectTransform.offsetMax = new Vector2(-4f, 0f);
        label.text = "批量";
        label.fontSize = rowFontSize;
        label.raycastTarget = false;

        toggle.onValueChanged.RemoveAllListeners();
        toggle.SetIsOnWithoutNotify(batchMode);
        toggle.onValueChanged.AddListener(OnBatchToggleChanged);
    }

    private void EnsureSensorInputBar(RectTransform root)
    {
        var bg = root.GetComponent<Image>();
        if (bg == null)
            bg = root.gameObject.AddComponent<Image>();
        bg.color = headerBackground;
        BindAddDeleteButtons();
    }

    private void ShowSensorSuggestion(List<string> matches, string title)
    {
        RectTransform container = GetComponent<RectTransform>();
        if (container == null)
            return;
        RectTransform overlay = FindOrCreateRect(container, "SuggestionOverlay");
        var overlayImg = overlay.GetComponent<Image>();
        if (overlayImg == null)
            overlayImg = overlay.gameObject.AddComponent<Image>();
        overlayImg.color = new Color(0.05f, 0.07f, 0.1f, 0.98f);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.pivot = new Vector2(0.5f, 0.5f);
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;
        overlay.SetAsLastSibling();

        for (int i = overlay.childCount - 1; i >= 0; i--)
        {
            var child = overlay.GetChild(i);
            if (child.name.StartsWith("MatchBtn_"))
                DestroyImmediate(child.gameObject);
        }

        RectTransform titleRect = FindOrCreateRect(overlay, "Title");
        var titleText = EnsureText(titleRect, "Label", mutedTextColor, TextAnchor.MiddleCenter);
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleText.rectTransform.sizeDelta = new Vector2(0f, 24f);
        titleText.rectTransform.anchoredPosition = Vector2.zero;
        titleText.text = title;

        RectTransform closeRect = FindOrCreateRect(overlay, "CloseBtn");
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.sizeDelta = new Vector2(60f, 24f);
        closeRect.offsetMin = new Vector2(-60f, -24f);
        closeRect.offsetMax = Vector2.zero;
        var cImg = closeRect.GetComponent<Image>();
        if (cImg == null) cImg = closeRect.gameObject.AddComponent<Image>();
        cImg.color = new Color(0.6f, 0.2f, 0.2f, 0.9f);
        var cBtn = closeRect.GetComponent<Button>();
        if (cBtn == null) cBtn = closeRect.gameObject.AddComponent<Button>();
        cBtn.targetGraphic = cImg;
        cBtn.onClick.RemoveAllListeners();
        cBtn.onClick.AddListener(HideSensorSuggestion);
        var cText = EnsureText(closeRect, "Label", Color.white, TextAnchor.MiddleCenter);
        cText.rectTransform.anchorMin = Vector2.zero;
        cText.rectTransform.anchorMax = Vector2.one;
        cText.rectTransform.offsetMin = Vector2.zero;
        cText.rectTransform.offsetMax = Vector2.zero;
        cText.text = "关闭";

        float btnHeight = rowHeight + 4f;
        float startY = -30f;
        for (int i = 0; i < matches.Count; i++)
        {
            string match = matches[i];
            RectTransform btnRect = FindOrCreateRect(overlay, $"MatchBtn_{i}");
            btnRect.anchorMin = new Vector2(0.1f, 1f);
            btnRect.anchorMax = new Vector2(0.9f, 1f);
            btnRect.pivot = new Vector2(0.5f, 1f);
            btnRect.sizeDelta = new Vector2(0f, btnHeight);
            btnRect.anchoredPosition = new Vector2(0f, startY - i * btnHeight);

            var btnImg = btnRect.GetComponent<Image>();
            if (btnImg == null) btnImg = btnRect.gameObject.AddComponent<Image>();
            btnImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            var btn = btnRect.GetComponent<Button>();
            if (btn == null) btn = btnRect.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.RemoveAllListeners();
            string captured = match;
            btn.onClick.AddListener(() =>
            {
                HideSensorSuggestion();
                AddSensorOfType(captured);
            });
            var btnLabel = EnsureText(btnRect, "Label", textColor, TextAnchor.MiddleLeft);
            btnLabel.rectTransform.anchorMin = Vector2.zero;
            btnLabel.rectTransform.anchorMax = Vector2.one;
            btnLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
            btnLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);
            btnLabel.text = match;
        }
        overlay.gameObject.SetActive(true);
    }

    private void HideSensorSuggestion()
    {
        var overlay = transform.Find("SuggestionOverlay");
        if (overlay != null)
            overlay.gameObject.SetActive(false);
    }

    private void SetRowTexts(RowView row, string rank, string planeId, string detector, string rangeType, string range, string yaw, string pitch, string center)
    {
        if (row == null) return;
        if (row.rankText != null) row.rankText.text = rank;
        if (row.idText != null) row.idText.text = planeId;
        if (row.detectorText != null) row.detectorText.text = detector;
        if (row.rangeTypeText != null) row.rangeTypeText.text = rangeType;
        if (row.rangeText != null) row.rangeText.text = range;
        if (row.yawText != null) row.yawText.text = yaw;
        if (row.pitchText != null) row.pitchText.text = pitch;
        if (row.centerText != null) row.centerText.text = center;
    }
}
