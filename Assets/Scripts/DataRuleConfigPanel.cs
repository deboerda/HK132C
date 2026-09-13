#pragma warning disable CS0414
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 数据规则配置面板 —按飞机编号分组显示飞行数据变量及规则检查。/// "全部飞机"模式：参照SensorPanel，按飞机分组，每组有分组标题。/// 单个飞机模式：参照AlarmPanel，平铺显示，首列为飞机编号。/// 添加条目：InputField 输入变量名，自动补全提示，无效变量名则提示失败。/// 规则列：InputField 可编辑（播放前），播放后只读。/// </summary>
public class DataRuleConfigPanel : MonoBehaviour
{
    private enum FieldType { Numeric, String }

    private struct FieldDef
    {
        public string displayName;
        public string key;
        public FieldType type;
    }

    private static readonly FieldDef[] AllFields =
    {
        new FieldDef { displayName = "经度",       key = "lng",          type = FieldType.Numeric },
        new FieldDef { displayName = "纬度",       key = "lat",          type = FieldType.Numeric },
        new FieldDef { displayName = "高度",       key = "alt",          type = FieldType.Numeric },
        new FieldDef { displayName = "航向",       key = "heading",      type = FieldType.Numeric },
        new FieldDef { displayName = "俯仰",       key = "pitch",        type = FieldType.Numeric },
        new FieldDef { displayName = "横滚",       key = "roll",         type = FieldType.Numeric },
        new FieldDef { displayName = "速度",       key = "speed",        type = FieldType.Numeric },
        new FieldDef { displayName = "电量",       key = "battery",      type = FieldType.Numeric },
        new FieldDef { displayName = "探测范围",    key = "detectRange",  type = FieldType.Numeric },
        new FieldDef { displayName = "左右偏转角",  key = "yawAngle",     type = FieldType.Numeric },
        new FieldDef { displayName = "上下偏转角",  key = "pitchAngle",   type = FieldType.Numeric },
        new FieldDef { displayName = "周期号",     key = "cycleId",      type = FieldType.Numeric },
        new FieldDef { displayName = "状态",       key = "state",        type = FieldType.String },
        new FieldDef { displayName = "告警",       key = "alarm",        type = FieldType.String },
        new FieldDef { displayName = "探测器类型",  key = "detectorType", type = FieldType.String },
        new FieldDef { displayName = "范围类型",    key = "rangeType",    type = FieldType.String },
        new FieldDef { displayName = "中心点",     key = "centerPoint",  type = FieldType.String },
        new FieldDef { displayName = "雷达状态",    key = "radarStatus",  type = FieldType.String },
        new FieldDef { displayName = "飞行时间",    key = "flightTime",   type = FieldType.String },
        new FieldDef { displayName = "计划号",     key = "planId",       type = FieldType.String },
    };

    private static readonly Color[] MarkerPalette =
    {
        new Color(1f, 0.22f, 0.22f, 1f),
        new Color(1f, 0.55f, 0.12f, 1f),
        new Color(1f, 0.85f, 0.15f, 1f),
        new Color(0.35f, 1f, 0.4f, 1f),
        new Color(0.2f, 0.85f, 1f, 1f),
        new Color(0.45f, 0.5f, 1f, 1f),
        new Color(0.9f, 0.35f, 1f, 1f),
        new Color(1f, 1f, 1f, 1f),
    };

    private static Color MarkerColorFor(string fieldKey)
    {
        switch (fieldKey)
        {
            case "alt": return MarkerPalette[0];
            case "heading": return MarkerPalette[1];
            case "pitch": return MarkerPalette[2];
            case "roll": return MarkerPalette[3];
            case "speed": return MarkerPalette[4];
            case "battery": return MarkerPalette[5];
            default: return MarkerPalette[0];
        }
    }

    private static Color EnsureMarkerColor(Color color, string fieldKey)
    {
        if (color.a < 0.01f)
            return MarkerColorFor(fieldKey);
        return color;
    }

    [Serializable]
    private class TrackRuleEntry
    {
        public string fieldKey;
        public string rule;
        public bool hideMarker;
        public Color markerColor;
    }

    private class RowView
    {
        public RectTransform root;
        public Text rankText;
        public Text trackIdText;
        public Text varText; // Also serves as InputField.textComponent for editable rows
        public Text valueText;
        public InputField ruleInput;
        public Text statusText;
        public Image colorSwatch;
        public Button colorButton;
        public Button showButton;
        public Text showButtonLabel;
        public string boundTrackId;
        public string boundFieldKey;
    }

    [Header("Data")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private bool autoFindReceiver = true;
    [SerializeField] private float refreshInterval = 0.2f;
    [SerializeField] private bool enableInEditor = true; // Run Refresh in edit mode too

    [Header("Playback State")]
    [SerializeField] private bool isReadOnly = false;

    [Header("Default Rules")]
    [SerializeField] private string altRule = ">0";
    [SerializeField] private string headingRule = "0~360";
    [SerializeField] private string pitchRule = "-90~90";
    [SerializeField] private string rollRule = "-180~180";
    [SerializeField] private string speedRule = ">=0";
    [SerializeField] private string batteryRule = "0~100";

    [Header("Layout")]
    [SerializeField] private Dropdown planeDropdown;
    [SerializeField] private RectTransform selectorRoot;
    [SerializeField] private RectTransform headerRoot;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform addButtonRoot;
    [SerializeField] private RectTransform inputBarRoot;
    [SerializeField] private bool autoCreateLayout = true;
    [SerializeField] private float selectorHeight = 34f;
    [SerializeField] private float headerHeight = 32f;
    [SerializeField] private float footerHeight = 36f;
    [SerializeField] private float rowHeight = 30f;
    [SerializeField] private float groupHeaderHeight = 28f;
    [SerializeField] private float scrollbarWidth = 14f;
    [SerializeField] private float columnSpacing = 4f;
    [SerializeField] private int maxRows = 128;

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
    [SerializeField] private Color normalColor = new Color(0.35f, 1f, 0.5f, 1f);
    [SerializeField] private Color violationColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.78f, 0f, 1f);
    [SerializeField] private Color addButtonColor = new Color(0.15f, 0.45f, 0.75f, 0.95f);
    [SerializeField] private Color inputBgColor = new Color(0.08f, 0.1f, 0.13f, 0.95f);
    [SerializeField] private Color errorColor = new Color(1f, 0.3f, 0.3f, 1f);

    private readonly List<RowView> rows = new List<RowView>();
    private readonly List<string> dropdownTrackIds = new List<string>();
    private const string AllPlanesOption = "__ALL__";
    private RowView headerRow;
    private float nextRefreshTime;
    private string selectedTrackId = AllPlanesOption;
    private InputField addVarInput; // kept for compatibility but unused in new flow
    private Text addVarHint;
    private float hintClearTime;
    private FlightProgressController cachedProgressController;
    private readonly HashSet<string> activeViolations = new HashSet<string>();

    // Serialized storage for trackEntries (survives Play Mode enter/exit)
    [Serializable]
    private class SerializableTrackEntry
    {
        public string trackId;
        public List<TrackRuleEntry> entries;
    }

    [SerializeField] private List<SerializableTrackEntry> serializedTrackEntries = new List<SerializableTrackEntry>();

    private readonly Dictionary<string, List<TrackRuleEntry>> trackEntries = new Dictionary<string, List<TrackRuleEntry>>();

    private void SyncSerializedToDict()
    {
        // Only add entries from serialized that aren't already in dict (don't clear runtime state)
        foreach (var ste in serializedTrackEntries)
        {
            if (!string.IsNullOrEmpty(ste.trackId) && ste.entries != null && !trackEntries.ContainsKey(ste.trackId))
                trackEntries[ste.trackId] = new List<TrackRuleEntry>(ste.entries);
        }
    }

    private void SyncDictToSerialized()
    {
        serializedTrackEntries.Clear();
        foreach (var kv in trackEntries)
        {
            serializedTrackEntries.Add(new SerializableTrackEntry
            {
                trackId = kv.Key,
                entries = new List<TrackRuleEntry>(kv.Value)
            });
        }
    }

    private List<TrackRuleEntry> GetDefaultEntries()
    {
        return new List<TrackRuleEntry>
        {
            new TrackRuleEntry { fieldKey = "alt",      rule = altRule,     markerColor = MarkerColorFor("alt") },
            new TrackRuleEntry { fieldKey = "heading",  rule = headingRule, markerColor = MarkerColorFor("heading") },
            new TrackRuleEntry { fieldKey = "pitch",    rule = pitchRule,   markerColor = MarkerColorFor("pitch") },
            new TrackRuleEntry { fieldKey = "roll",     rule = rollRule,    markerColor = MarkerColorFor("roll") },
            new TrackRuleEntry { fieldKey = "speed",    rule = speedRule,   markerColor = MarkerColorFor("speed") },
            new TrackRuleEntry { fieldKey = "battery",  rule = batteryRule, markerColor = MarkerColorFor("battery") },
        };
    }

    private List<TrackRuleEntry> GetEntries(string trackId)
    {
        if (!trackEntries.TryGetValue(trackId, out var entries))
        {
            entries = GetDefaultEntries();
            trackEntries[trackId] = entries;
        }
        return entries;
    }

    private bool IsAllPlanesMode => selectedTrackId == AllPlanesOption;

    // ── Value extraction ───────────────────────────────────────

    private string GetStringValue(FlightDataStreamReceiver.TrackPosition pos,
        FlightDataStreamReceiver.TrackTimetableState state,
        string key)
    {
        switch (key)
        {
            case "lng":          return pos?.lng.ToString("F5") ?? "-";
            case "lat":          return pos?.lat.ToString("F5") ?? "-";
            case "alt":          return pos?.alt.ToString("F1") ?? "-";
            case "heading":      return pos?.heading.ToString("F1") ?? "-";
            case "pitch":        return pos?.pitch.ToString("F1") ?? "-";
            case "roll":         return pos?.roll.ToString("F1") ?? "-";
            case "speed":        return state?.speed.ToString("F1") ?? "-";
            case "battery":      return state?.battery.ToString("F0") ?? "-";
            case "detectRange":  return state?.detectRange.ToString("F0") ?? "-";
            case "yawAngle":     return state?.yawAngle.ToString("F1") ?? "-";
            case "pitchAngle":   return state?.pitchAngle.ToString("F1") ?? "-";
            case "cycleId":      return "-";
            case "state":        return state?.state ?? "-";
            case "alarm":        return state?.alarm ?? "-";
            case "detectorType": return state?.detectorType ?? "-";
            case "rangeType":    return state?.rangeType ?? "-";
            case "centerPoint":  return state?.centerPoint ?? "-";
            case "radarStatus":  return state?.radarStatus ?? "-";
            case "flightTime":   return pos?.flightTime ?? "-";
            case "planId":       return pos?.planId ?? "-";
            default:             return "-";
        }
    }

    private float? GetNumericValue(FlightDataStreamReceiver.TrackPosition pos,
        FlightDataStreamReceiver.TrackTimetableState state, string key)
    {
        switch (key)
        {
            case "lng":         return pos?.lng;
            case "lat":         return pos?.lat;
            case "alt":         return pos?.alt;
            case "heading":     return pos?.heading;
            case "pitch":       return pos?.pitch;
            case "roll":        return pos?.roll;
            case "speed":       return state?.speed;
            case "battery":     return state?.battery;
            case "detectRange": return state?.detectRange;
            case "yawAngle":    return state?.yawAngle;
            case "pitchAngle":  return state?.pitchAngle;
            default:            return null;
        }
    }

    private static bool IsNumericField(string key)
    {
        switch (key)
        {
            case "lng": case "lat": case "alt": case "heading": case "pitch":
            case "roll": case "speed": case "battery": case "detectRange":
            case "yawAngle": case "pitchAngle":
                return true;
            default: return false;
        }
    }

    private static string GetFieldDisplayName(string key)
    {
        foreach (var f in AllFields) if (f.key == key) return f.displayName;
        return key;
    }

    private static string FindFieldKey(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();
        foreach (var f in AllFields)
        {
            if (f.key == input || f.displayName == input) return f.key;
            if (f.key.Equals(input, StringComparison.OrdinalIgnoreCase)) return f.key;
            if (f.displayName.Equals(input, StringComparison.OrdinalIgnoreCase)) return f.key;
        }
        return null;
    }

    // ── Rule checking ──────────────────────────────────────────

    private enum RuleStatus { Pass, Fail, Invalid, Empty }

    private static bool TryParseFloat(string text, out float value)
    {
        text = (text ?? string.Empty).Trim().Replace("，", ".");
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private string NormalizeRule(string rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
            return string.Empty;
        rule = rule.Trim();
        rule = rule.Replace("＝", "=").Replace("～", "~").Replace("＞", ">").Replace("＜", "<");
        rule = rule.Replace("==", "=");
        // Allow "高度=0" / "alt=0" in the rule cell by stripping a leading field name.
        foreach (var field in AllFields)
        {
            if (rule.StartsWith(field.displayName, StringComparison.OrdinalIgnoreCase))
            {
                rule = rule.Substring(field.displayName.Length).Trim();
                break;
            }
            if (rule.StartsWith(field.key, StringComparison.OrdinalIgnoreCase))
            {
                rule = rule.Substring(field.key.Length).Trim();
                break;
            }
        }
        return rule;
    }

    private RuleStatus EvaluateRule(float value, string rule)
    {
        rule = NormalizeRule(rule);
        if (string.IsNullOrWhiteSpace(rule))
            return RuleStatus.Empty;

        if (rule.Contains("~"))
        {
            var parts = rule.Split('~');
            if (parts.Length == 2 && TryParseFloat(parts[0], out float min) && TryParseFloat(parts[1], out float max))
                return value >= min && value <= max ? RuleStatus.Pass : RuleStatus.Fail;
            return RuleStatus.Invalid;
        }

        if (rule.StartsWith(">=") && TryParseFloat(rule.Substring(2), out float ge))
            return value >= ge ? RuleStatus.Pass : RuleStatus.Fail;
        if (rule.StartsWith("<=") && TryParseFloat(rule.Substring(2), out float le))
            return value <= le ? RuleStatus.Pass : RuleStatus.Fail;
        if (rule.StartsWith("!=") && TryParseFloat(rule.Substring(2), out float ne))
            return !Mathf.Approximately(value, ne) ? RuleStatus.Pass : RuleStatus.Fail;
        if (rule.StartsWith(">") && TryParseFloat(rule.Substring(1), out float gt))
            return value > gt ? RuleStatus.Pass : RuleStatus.Fail;
        if (rule.StartsWith("<") && TryParseFloat(rule.Substring(1), out float lt))
            return value < lt ? RuleStatus.Pass : RuleStatus.Fail;
        if (rule.StartsWith("=") && TryParseFloat(rule.Substring(1), out float eqPrefixed))
            return Mathf.Approximately(value, eqPrefixed) ? RuleStatus.Pass : RuleStatus.Fail;
        if (TryParseFloat(rule, out float eq))
            return Mathf.Approximately(value, eq) ? RuleStatus.Pass : RuleStatus.Fail;
        return RuleStatus.Invalid;
    }

    // ── Lifecycle ───────────────────────────────────────────────

    private void Awake()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        SyncSerializedToDict(); // Restore user-added entries from serialized storage
        if (autoCreateLayout) EnsureLayoutObjects();
        if (planeDropdown != null)
        {
            planeDropdown.onValueChanged.RemoveListener(OnPlaneDropdownChanged);
            planeDropdown.onValueChanged.AddListener(OnPlaneDropdownChanged);
        }
        EnsureVerticalLayout(headerRoot, false);
        EnsureVerticalLayout(contentRoot, true);
        if (headerRoot != null)
        {
            headerRow = FindOrCreateHeaderRow();
            SetHeaderTexts(headerRow);
        }
    }

    private void OnEnable()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        SyncSerializedToDict(); // Restore entries on every enable (panel switch)
        if (autoCreateLayout) EnsureLayoutObjects();
        if (planeDropdown != null)
        {
            planeDropdown.onValueChanged.RemoveListener(OnPlaneDropdownChanged);
            planeDropdown.onValueChanged.AddListener(OnPlaneDropdownChanged);
        }
        EnsureVerticalLayout(headerRoot, false);
        EnsureVerticalLayout(contentRoot, true);
        if (headerRoot != null)
        {
            headerRow = FindOrCreateHeaderRow();
            SetHeaderTexts(headerRow);
        }
        nextRefreshTime = 0f;
        Refresh(); // Force immediate refresh so data shows right away
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        // Clear hint after delay
        if (addVarHint != null && addVarHint.gameObject.activeSelf && Time.unscaledTime > hintClearTime)
            addVarHint.gameObject.SetActive(false);

        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);

        Refresh();
    }

    public void SetReadOnly(bool readOnly)
    {
        isReadOnly = readOnly;
        Refresh(); // Force immediate refresh when read-only state changes
    }

    // ── Refresh ────────────────────────────────────────────────

    private void Refresh()
    {
        // Always prefer the live singleton when autoFindReceiver is enabled,
        // even if the serialized field was previously set (it may be stale after domain reload)
        if (autoFindReceiver)
        {
            var inst = FlightDataStreamReceiver.Instance;
            if (inst != null) receiver = inst;
        }

        var positions = receiver?.SnapshotPositions;
        var states = receiver?.SnapshotStates;
        if (positions != null || states != null)
            UpdatePlaneDropdown(positions, states);

        // Build trackId list from receiver data AND from configured entries
        var trackIds = new List<string>();
        if (positions != null) foreach (var p in positions) if (p != null && !string.IsNullOrWhiteSpace(p.trackId) && !trackIds.Contains(p.trackId)) trackIds.Add(p.trackId);
        if (states != null) foreach (var s in states) if (s != null && !string.IsNullOrWhiteSpace(s.trackId) && !trackIds.Contains(s.trackId)) trackIds.Add(s.trackId);

        // Ensure every real trackId has at least default entries
        foreach (string realId in trackIds)
        {
            if (!trackEntries.ContainsKey(realId))
            {
                trackEntries[realId] = GetDefaultEntries();
                SyncDictToSerialized();
            }
        }

        // Migrate "默认飞机" entries to ALL real trackIds if data is available
        if (trackEntries.ContainsKey("默认飞机") && trackIds.Count > 0)
        {
            var sourceEntries = trackEntries["默认飞机"];
            bool migrated = false;
            foreach (string realId in trackIds)
            {
                if (!trackEntries.ContainsKey(realId))
                {
                    // Create fresh copy of source entries
                    var migratedEntries = new List<TrackRuleEntry>();
                    foreach (var e in sourceEntries)
                        migratedEntries.Add(new TrackRuleEntry { fieldKey = e.fieldKey, rule = e.rule, hideMarker = e.hideMarker, markerColor = e.markerColor });
                    trackEntries[realId] = migratedEntries;
                    migrated = true;
                }
                else
                {
                    // Merge any user-added entries from "默认飞机" that are missing in realId
                    var realEntries = trackEntries[realId];
                    var realKeys = new HashSet<string>();
                    foreach (var e in realEntries) realKeys.Add(e.fieldKey);
                    foreach (var e in sourceEntries)
                    {
                        if (!realKeys.Contains(e.fieldKey))
                        {
                            realEntries.Add(new TrackRuleEntry { fieldKey = e.fieldKey, rule = e.rule, hideMarker = e.hideMarker, markerColor = e.markerColor });
                            migrated = true;
                        }
                    }
                }
            }
            if (migrated)
            {
                trackEntries.Remove("默认飞机");
                SyncDictToSerialized();
            }
        }

        // Remove "默认飞机" from display list if real data exists
        if (trackIds.Count > 0)
            trackIds.RemoveAll(id => id == "默认飞机");

        // Also include trackIds from configured entries (so they show even without live data)
        foreach (var kv in trackEntries)
            if (!trackIds.Contains(kv.Key)) trackIds.Add(kv.Key);
        trackIds.Sort(StringComparer.Ordinal);

        if (!IsAllPlanesMode) trackIds = trackIds.FindAll(id => id == selectedTrackId);

        var displayItems = new List<DisplayItem>();
        foreach (var trackId in trackIds)
        {
            if (IsAllPlanesMode)
                displayItems.Add(new DisplayItem { type = DisplayType.GroupHeader, trackId = trackId });

            var pos = positions?.Find(p => p != null && p.trackId == trackId);
            var state = states?.Find(s => s != null && s.trackId == trackId);
            bool hasLiveData = pos != null || state != null;
            var entries = GetEntries(trackId);

            for (int i = 0; i < entries.Count; i++)
            {
                // Read back user-edited values from InputFields before refreshing
                SyncEntryFromRow(entries[i], i, trackId);
            }
            SyncDictToSerialized(); // Persist user edits

            for (int i = 0; i < entries.Count; i++)
            {
                string key = FindFieldKey(entries[i].fieldKey) ?? entries[i].fieldKey;
                string rule = entries[i].rule;
                bool isNum = IsNumericField(key);
                displayItems.Add(new DisplayItem
                {
                    type = DisplayType.DataRow, rank = i + 1, trackId = trackId,
                    fieldKey = key, displayName = GetFieldDisplayName(key),
                    strValue = GetStringValue(pos, state, key),
                    numValue = isNum ? GetNumericValue(pos, state, key) : null,
                    isNumeric = isNum, rule = rule,
                    hasLiveData = hasLiveData,
                    hideMarker = entries[i].hideMarker,
                    markerColor = EnsureMarkerColor(entries[i].markerColor, key),
                });
            }
        }

        EnsureRowCount(displayItems.Count);
        SetActiveRowCount(displayItems.Count);

        for (int i = 0; i < displayItems.Count; i++)
        {
            var item = displayItems[i];
            if (item.type == DisplayType.GroupHeader)
                ConfigureGroupRow(rows[i], item.trackId);
            else
                ConfigureDataRow(rows[i], item.rank, item.trackId, item.fieldKey, item.displayName, item.strValue, item.numValue, item.isNumeric, item.rule, item.hasLiveData, item.hideMarker, item.markerColor);
        }
    }

    // ── Sync user input back to entries before Refresh overwrites ──

    private void SyncEntryFromRow(TrackRuleEntry entry, int entryIndex, string trackId)
    {
        string wantKey = FindFieldKey(entry.fieldKey) ?? entry.fieldKey;
        RowView matched = null;
        int rowOffset = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null || row.root == null || !row.root.gameObject.activeSelf)
                continue;

            bool sameTrack = row.boundTrackId == trackId || row.root.name.StartsWith($"Row_{trackId}_");
            if (!sameTrack)
                continue;

            string rowKey = FindFieldKey(row.boundFieldKey) ?? row.boundFieldKey;
            if (!string.IsNullOrEmpty(rowKey) && rowKey == wantKey)
            {
                matched = row;
                break;
            }
            if (rowOffset == entryIndex)
                matched = row;
            rowOffset++;
        }

        if (matched == null)
            return;

        if (matched.varText != null)
        {
            var varInput = matched.varText.GetComponent<InputField>();
            if (varInput != null)
            {
                string matchedKey = FindFieldKey(varInput.text);
                if (matchedKey != null)
                    entry.fieldKey = matchedKey;
            }
        }
        if (matched.ruleInput != null)
            entry.rule = matched.ruleInput.text;
        if (matched.colorSwatch != null)
            entry.markerColor = matched.colorSwatch.color;
        if (matched.showButtonLabel != null)
            entry.hideMarker = matched.showButtonLabel.text == "隐";
    }

    private enum DisplayType { GroupHeader, DataRow }
    private class DisplayItem
    {
        public DisplayType type;
        public string trackId;
        public int rank;
        public string fieldKey;
        public string displayName;
        public string strValue;
        public float? numValue;
        public bool isNumeric;
        public string rule;
        public bool hasLiveData;
        public bool hideMarker;
        public Color markerColor;
    }

    // ── Row configuration ───────────────────────────────────────

    private void ConfigureGroupRow(RowView row, string trackId)
    {
        if (row == null) return;
        var img = row.root.GetComponent<Image>(); if (img != null) img.color = groupBackground;
        var le = row.root.GetComponent<LayoutElement>(); if (le != null) le.preferredHeight = groupHeaderHeight;
        if (row.rankText != null) row.rankText.text = "";
        if (row.trackIdText != null)
        {
            row.trackIdText.text = trackId; row.trackIdText.color = groupTextColor;
            row.trackIdText.fontStyle = FontStyle.Bold; row.trackIdText.fontSize = groupFontSize;
        }
        if (row.varText != null) row.varText.text = "";
        if (row.valueText != null) row.valueText.text = "";
        if (row.ruleInput != null) { row.ruleInput.text = ""; row.ruleInput.interactable = false; }
        if (row.statusText != null) row.statusText.text = "";
        if (row.colorButton != null) row.colorButton.gameObject.SetActive(false);
        if (row.showButton != null) row.showButton.gameObject.SetActive(false);
    }

    private void ConfigureDataRow(RowView row, int rank, string trackId, string fieldKey, string varName, string strValue, float? numValue, bool isNumeric, string rule, bool hasLiveData, bool hideMarker, Color markerColor)
    {
        if (row == null) return;
        row.boundTrackId = trackId;
        row.boundFieldKey = fieldKey;
        int vi = row.root.GetSiblingIndex();
        var img = row.root.GetComponent<Image>(); if (img != null) img.color = vi % 2 == 0 ? oddRowBackground : evenRowBackground;
        var le = row.root.GetComponent<LayoutElement>(); if (le != null) le.preferredHeight = rowHeight;

        if (row.trackIdText != null)
        {
            row.trackIdText.fontSize = rowFontSize; row.trackIdText.fontStyle = FontStyle.Normal; row.trackIdText.color = textColor;
            row.trackIdText.text = IsAllPlanesMode ? "" : trackId;
        }
        if (row.rankText != null) row.rankText.text = rank.ToString();
        if (row.varText != null)
        {
            // Only update if text actually changed (don't wipe user input while editing)
            var input = row.varText.GetComponent<InputField>();
            if (input != null)
            {
                // Don't overwrite if InputField is currently focused (user is typing)
                if (!input.isFocused && input.text != varName)
                    input.SetTextWithoutNotify(varName);
                input.interactable = !isReadOnly;
            }
            else
            {
                if (row.varText.text != varName)
                    row.varText.text = varName;
            }
        }
        if (row.valueText != null) row.valueText.text = strValue;

        if (row.ruleInput != null)
        {
            // The on-screen rule is the source of truth after pause-time edits.
            if (!string.IsNullOrEmpty(row.ruleInput.text))
                rule = row.ruleInput.text;
            else if (!row.ruleInput.isFocused)
                row.ruleInput.SetTextWithoutNotify(rule ?? "");
            row.ruleInput.interactable = !isReadOnly;
        }
        if (row.colorButton != null)
        {
            row.colorButton.gameObject.SetActive(true);
            if (row.colorSwatch != null)
                row.colorSwatch.color = EnsureMarkerColor(markerColor, fieldKey);
        }
        if (row.showButton != null)
        {
            row.showButton.gameObject.SetActive(true);
            if (row.showButtonLabel != null)
                row.showButtonLabel.text = hideMarker ? "隐" : "显";
        }
        var progress = GetProgressController();
        if (progress != null)
            progress.SetMarkerGroupVisible(ViolationKey(trackId, fieldKey), !hideMarker);
        if (row.statusText != null)
        {
            if (!isNumeric)
            {
                row.statusText.text = hasLiveData ? "正常" : "待数据";
                row.statusText.color = hasLiveData ? normalColor : warningColor;
            }
            else if (!hasLiveData || !numValue.HasValue)
            {
                row.statusText.text = "待数据";
                row.statusText.color = warningColor;
            }
            else
            {
                var status = EvaluateRule(numValue.Value, rule);
                switch (status)
                {
                    case RuleStatus.Empty:
                        row.statusText.text = "无规则";
                        row.statusText.color = mutedTextColor;
                        break;
                    case RuleStatus.Invalid:
                        row.statusText.text = "规则无效";
                        row.statusText.color = warningColor;
                        break;
                    case RuleStatus.Fail:
                        row.statusText.text = "超限";
                        row.statusText.color = violationColor;
                        if (isReadOnly && !hideMarker)
                            NotifyViolation(trackId, fieldKey, varName, markerColor);
                        break;
                    default:
                        row.statusText.text = "正常";
                        row.statusText.color = normalColor;
                        activeViolations.Remove(ViolationKey(trackId, fieldKey));
                        break;
                }
            }
        }
        row.root.name = $"Row_{trackId}_{fieldKey}";
    }

    private static string ViolationKey(string trackId, string fieldKey)
    {
        string key = FindFieldKey(fieldKey) ?? (fieldKey ?? string.Empty).Trim();
        return $"{trackId}|{key}";
    }

    private void NotifyViolation(string trackId, string fieldKey, string varName, Color color)
    {
        var progressController = GetProgressController();
        if (progressController != null)
            progressController.AddViolationMarker($"{trackId} {varName}超限", color, ViolationKey(trackId, fieldKey));
    }

    public void ClearViolationState()
    {
        activeViolations.Clear();
    }

    private FlightProgressController GetProgressController()
    {
        if (cachedProgressController != null) return cachedProgressController;
        cachedProgressController = FindAnyObjectByType<FlightProgressController>();
        if (cachedProgressController == null)
        {
            var all = Resources.FindObjectsOfTypeAll<FlightProgressController>();
            if (all.Length > 0) cachedProgressController = all[0];
        }
        return cachedProgressController;
    }

    // ── Add entry via InputField ────────────────────────────────

    private void OnAddEntryClicked()
    {
        // Determine target trackId: use selected plane, or first available from receiver, or from trackEntries
        string trackId = selectedTrackId != AllPlanesOption ? selectedTrackId : "";

        // Try to get a real trackId from receiver
        if (string.IsNullOrEmpty(trackId))
        {
            var receiver = FlightDataStreamReceiver.Instance;
            if (receiver != null)
            {
                var positions = receiver.SnapshotPositions;
                if (positions != null && positions.Count > 0)
                {
                    foreach (var p in positions)
                    {
                        if (p != null && !string.IsNullOrWhiteSpace(p.trackId))
                        {
                            trackId = p.trackId;
                            break;
                        }
                    }
                }
            }
        }

        // If still no trackId, try from trackEntries
        if (string.IsNullOrEmpty(trackId) && trackEntries.Count > 0)
        {
            foreach (var kv in trackEntries)
            {
                if (kv.Key != AllPlanesOption)
                {
                    trackId = kv.Key;
                    break;
                }
            }
        }

        // Last resort: use a default
        if (string.IsNullOrEmpty(trackId))
        {
            trackId = "默认飞机";
            if (!dropdownTrackIds.Contains(trackId))
                dropdownTrackIds.Add(trackId);
        }

        var entries = GetEntries(trackId);
        entries.Add(new TrackRuleEntry { fieldKey = "", rule = "" });
        ShowHint("已新建行，请在行中输入变量名和规则", normalColor);
        SyncDictToSerialized();
        Refresh();
    }

    private void ShowHint(string msg, Color color)
    {
        if (addVarHint == null) return;
        addVarHint.text = msg; addVarHint.color = color;
        addVarHint.gameObject.SetActive(true);
        hintClearTime = Time.unscaledTime + 3f;
    }

    // ── Plane dropdown ─────────────────────────────────────────

    private void OnPlaneDropdownChanged(int index)
    {
        if (index >= 0 && index < dropdownTrackIds.Count)
        {
            selectedTrackId = dropdownTrackIds[index];
            // Update header visibility
            if (headerRow != null && headerRow.trackIdText != null)
                headerRow.trackIdText.gameObject.SetActive(!IsAllPlanesMode);
            Refresh();
        }
    }

    private void UpdatePlaneDropdown(List<FlightDataStreamReceiver.TrackPosition> positions, List<FlightDataStreamReceiver.TrackTimetableState> states)
    {
        if (planeDropdown == null) return;
        var tids = new List<string> { AllPlanesOption };
        if (positions != null) foreach (var p in positions) { string id = p?.trackId; if (!string.IsNullOrWhiteSpace(id) && !tids.Contains(id)) tids.Add(id); }
        if (states != null) foreach (var s in states) { string id = s?.trackId; if (!string.IsNullOrWhiteSpace(id) && !tids.Contains(id)) tids.Add(id); }
        // Also include trackIds from configured entries
        foreach (var kv in trackEntries)
            if (!tids.Contains(kv.Key)) tids.Add(kv.Key);

        bool changed = tids.Count != dropdownTrackIds.Count;
        if (!changed) for (int i = 0; i < tids.Count; i++) if (tids[i] != dropdownTrackIds[i]) { changed = true; break; }
        if (!changed) return;

        dropdownTrackIds.Clear(); dropdownTrackIds.AddRange(tids);
        var opts = new List<Dropdown.OptionData>();
        foreach (string id in dropdownTrackIds) opts.Add(new Dropdown.OptionData(id == AllPlanesOption ? "全部飞机" : id));
        planeDropdown.ClearOptions(); planeDropdown.AddOptions(opts);
        int idx = dropdownTrackIds.IndexOf(selectedTrackId);
        if (idx < 0) { idx = 0; selectedTrackId = AllPlanesOption; }
        planeDropdown.SetValueWithoutNotify(idx); planeDropdown.RefreshShownValue();
    }

    // ── Layout creation ────────────────────────────────────────

    private void EnsureLayoutObjects()
    {
        RectTransform panel = GetComponent<RectTransform>();
        if (panel == null) return;

        RectTransform container = FindOrCreateRect(panel, "DataRuleConfigRoot");
        if (container.GetComponent<Image>() == null) container.gameObject.AddComponent<Image>();
        container.GetComponent<Image>().color = panelBackground;
        bool firstCreate = container.childCount == 0;
        if (firstCreate) StretchFull(container);

        selectorRoot = FindOrCreateRect(container, "SelectorRoot");
        StretchTop(selectorRoot, selectorHeight, 0f);
        planeDropdown = EnsureDropdown(selectorRoot);

        headerRoot = FindOrCreateRect(container, "HeaderRoot");
        StretchTop(headerRoot, headerHeight, selectorHeight);
        headerRoot.offsetMax = new Vector2(-scrollbarWidth - 2f, headerRoot.offsetMax.y);

        RectTransform scrollRoot = FindOrCreateRect(container, "Scroll View");
        StretchFillBelowWithFooter(scrollRoot, selectorHeight + headerHeight, footerHeight);
        EnsureScrollArea(scrollRoot);

        // Input bar + Add button at bottom
        inputBarRoot = FindOrCreateRect(container, "InputBarRoot");
        StretchBottom(inputBarRoot, footerHeight);
        EnsureInputBar(inputBarRoot);

        selectorRoot.SetAsLastSibling();
        container.SetAsLastSibling();
    }

    private void EnsureInputBar(RectTransform root)
    {
        var hlg = root.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.spacing = 6f; hlg.padding = new RectOffset(8, 8, 2, 2);

        // Add button (find or create)
        RectTransform btnRect = FindOrCreateRect(root, "AddBtn");
        var ble = btnRect.GetComponent<LayoutElement>();
        if (ble == null) ble = btnRect.gameObject.AddComponent<LayoutElement>();
        ble.minWidth = 100f; ble.preferredWidth = 100f; ble.flexibleWidth = 0f;
        var bImg = btnRect.GetComponent<Image>();
        if (bImg == null) bImg = btnRect.gameObject.AddComponent<Image>();
        bImg.color = addButtonColor;
        var btn = btnRect.GetComponent<Button>();
        if (btn == null) btn = btnRect.gameObject.AddComponent<Button>();
        btn.targetGraphic = bImg;
        btn.onClick.RemoveListener(OnAddEntryClicked);
        btn.onClick.AddListener(OnAddEntryClicked);

        RectTransform bTextRect = FindOrCreateRect(btnRect, "Label");
        bTextRect.anchorMin = Vector2.zero; bTextRect.anchorMax = Vector2.one;
        bTextRect.offsetMin = Vector2.zero; bTextRect.offsetMax = Vector2.zero;
        var bt = bTextRect.GetComponent<Text>();
        if (bt == null) bt = bTextRect.gameObject.AddComponent<Text>();
        bt.font = font; bt.fontSize = rowFontSize; bt.color = Color.white; bt.alignment = TextAnchor.MiddleCenter;
        bt.text = "+ 添加行";

        // Hint text (find or create)
        RectTransform hintRect = FindOrCreateRect(root, "Hint");
        var hle = hintRect.GetComponent<LayoutElement>();
        if (hle == null) hle = hintRect.gameObject.AddComponent<LayoutElement>();
        hle.flexibleWidth = 1f; hle.minWidth = 50f;
        addVarHint = hintRect.GetComponent<Text>();
        if (addVarHint == null) addVarHint = hintRect.gameObject.AddComponent<Text>();
        addVarHint.font = font; addVarHint.fontSize = rowFontSize; addVarHint.color = normalColor;
        addVarHint.alignment = TextAnchor.MiddleLeft; addVarHint.horizontalOverflow = HorizontalWrapMode.Overflow;
        addVarHint.gameObject.SetActive(false);
    }

    private Dropdown EnsureDropdown(RectTransform root)
    {
        RectTransform dd = FindOrCreateRect(root, "PlaneSelector");
        StretchFull(dd);
        var img = dd.GetComponent<Image>();
        if (img == null) img = dd.gameObject.AddComponent<Image>();
        img.color = new Color(0.1f, 0.13f, 0.16f, 0.95f);
        var dropdown = dd.GetComponent<Dropdown>();
        if (dropdown == null) dropdown = dd.gameObject.AddComponent<Dropdown>();

        Text label = EnsureText(dd, "Label", textColor, TextAnchor.MiddleLeft);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(8f, 0f); label.rectTransform.offsetMax = new Vector2(-28f, 0f);
        label.text = "全部飞机";

        Text arrow = EnsureText(dd, "Arrow", mutedTextColor, TextAnchor.MiddleCenter);
        arrow.rectTransform.anchorMin = new Vector2(1f, 0f); arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
        arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
        arrow.rectTransform.offsetMin = new Vector2(-26f, 0f); arrow.rectTransform.offsetMax = Vector2.zero;
        arrow.text = "v";

        RectTransform template = EnsureDropdownTemplate(dd);
        dropdown.targetGraphic = img; dropdown.captionText = label;
        dropdown.template = template;
        dropdown.itemText = template.Find("Viewport/Content/Item/Item Label")?.GetComponent<Text>();
        return dropdown;
    }

    private RectTransform EnsureDropdownTemplate(RectTransform dd)
    {
        RectTransform t = FindOrCreateRect(dd, "Template");
        t.anchorMin = new Vector2(0f, 0f); t.anchorMax = new Vector2(1f, 0f);
        t.pivot = new Vector2(0.5f, 1f);
        t.offsetMin = new Vector2(0f, -Mathf.Max(120f, rowHeight * 5f));
        t.offsetMax = Vector2.zero; t.SetAsLastSibling(); t.gameObject.SetActive(false);

        var tc = t.GetComponent<Canvas>();
        if (tc == null) tc = t.gameObject.AddComponent<Canvas>();
        tc.overrideSorting = true; tc.sortingOrder = 100;
        if (t.GetComponent<GraphicRaycaster>() == null) t.gameObject.AddComponent<GraphicRaycaster>();

        var ti = t.GetComponent<Image>();
        if (ti == null) ti = t.gameObject.AddComponent<Image>();
        ti.color = new Color(0.08f, 0.1f, 0.13f, 0.98f);

        var sr = t.GetComponent<ScrollRect>();
        if (sr == null) sr = t.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 24f;

        RectTransform vp = FindOrCreateRect(t, "Viewport");
        StretchFull(vp); vp.offsetMax = new Vector2(-scrollbarWidth - 2f, 0f);
        var vpi = vp.GetComponent<Image>();
        if (vpi == null) vpi = vp.gameObject.AddComponent<Image>();
        vpi.color = new Color(1f, 1f, 1f, 0.01f);
        var mask = vp.GetComponent<Mask>();
        if (mask == null) mask = vp.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform content = FindOrCreateRect(vp, "Content");
        StretchContentTop(content); EnsureVerticalLayout(content, true);

        RectTransform item = FindOrCreateRect(content, "Item");
        item.anchorMin = new Vector2(0f, 1f); item.anchorMax = new Vector2(1f, 1f);
        item.pivot = new Vector2(0.5f, 1f); item.anchoredPosition = Vector2.zero;
        item.sizeDelta = new Vector2(0f, rowHeight);
        var toggle = item.GetComponent<Toggle>();
        if (toggle == null) toggle = item.gameObject.AddComponent<Toggle>();
        var ii = item.GetComponent<Image>();
        if (ii == null) ii = item.gameObject.AddComponent<Image>();
        ii.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        toggle.targetGraphic = ii;

        Text il = EnsureText(item, "Item Label", textColor, TextAnchor.MiddleLeft);
        il.rectTransform.anchorMin = Vector2.zero; il.rectTransform.anchorMax = Vector2.one;
        il.rectTransform.offsetMin = new Vector2(8f, 0f); il.rectTransform.offsetMax = new Vector2(-8f, 0f);
        toggle.graphic = null;

        var ilm = item.GetComponent<LayoutElement>();
        if (ilm == null) ilm = item.gameObject.AddComponent<LayoutElement>();
        ilm.minHeight = rowHeight; ilm.preferredHeight = rowHeight;

        sr.viewport = vp; sr.content = content;
        sr.verticalScrollbar = EnsureVerticalScrollbar(t);
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        sr.verticalScrollbarSpacing = 2f;
        return t;
    }

    private Text EnsureText(RectTransform parent, string name, Color color, TextAnchor alignment)
    {
        RectTransform r = FindOrCreateRect(parent, name);
        var t = r.GetComponent<Text>();
        if (t == null) t = r.gameObject.AddComponent<Text>();
        t.font = font; t.fontSize = rowFontSize; t.color = color; t.alignment = alignment;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    private void EnsureScrollArea(RectTransform sr)
    {
        var si = sr.GetComponent<Image>();
        if (si == null) si = sr.gameObject.AddComponent<Image>();
        si.color = new Color(0f, 0f, 0f, 0f); si.raycastTarget = true;

        RectTransform vp = FindOrCreateRect(sr, "Viewport");
        StretchViewport(vp);
        var vpi = vp.GetComponent<Image>();
        if (vpi == null) vpi = vp.gameObject.AddComponent<Image>();
        vpi.color = new Color(1f, 1f, 1f, 0.01f); vpi.raycastTarget = true;
        var mask = vp.GetComponent<Mask>();
        if (mask == null) mask = vp.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        contentRoot = FindOrCreateRect(vp, "ContentRoot");
        StretchContentTop(contentRoot);

        var s = sr.GetComponent<ScrollRect>();
        if (s == null) s = sr.gameObject.AddComponent<ScrollRect>();
        s.viewport = vp; s.content = contentRoot; s.horizontal = false; s.vertical = true;
        s.movementType = ScrollRect.MovementType.Clamped; s.scrollSensitivity = 24f;
        s.verticalScrollbar = EnsureVerticalScrollbar(sr);
        s.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        s.verticalScrollbarSpacing = 2f;
    }

    private Scrollbar EnsureVerticalScrollbar(RectTransform sr)
    {
        RectTransform sb = FindOrCreateRect(sr, "Vertical Scrollbar");
        sb.anchorMin = new Vector2(1f, 0f); sb.anchorMax = new Vector2(1f, 1f);
        sb.pivot = new Vector2(1f, 0.5f);
        sb.offsetMin = new Vector2(-scrollbarWidth, 0f); sb.offsetMax = Vector2.zero;
        var bg = sb.GetComponent<Image>();
        if (bg == null) bg = sb.gameObject.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.05f, 0.07f, 0.65f);

        RectTransform sa = FindOrCreateRect(sb, "Sliding Area");
        StretchFull(sa); sa.offsetMin = new Vector2(2f, 2f); sa.offsetMax = new Vector2(-2f, -2f);
        RectTransform h = FindOrCreateRect(sa, "Handle");
        StretchFull(h);
        var hi = h.GetComponent<Image>();
        if (hi == null) hi = h.gameObject.AddComponent<Image>();
        hi.color = new Color(0.43f, 0.82f, 1f, 0.9f);

        var s = sb.GetComponent<Scrollbar>();
        if (s == null) s = sb.gameObject.AddComponent<Scrollbar>();
        s.direction = Scrollbar.Direction.BottomToTop; s.handleRect = h;
        s.targetGraphic = hi; s.transition = Selectable.Transition.ColorTint;
        s.size = 1f; s.value = 1f;
        return s;
    }

    // ── Layout helpers ──────────────────────────────────────────

    private void StretchViewport(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero; r.sizeDelta = Vector2.zero;
        r.offsetMin = Vector2.zero; r.offsetMax = new Vector2(-scrollbarWidth - 2f, 0f);
    }
    private static void StretchTop(RectTransform r, float h, float off)
    {
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(0f, -off - h); r.offsetMax = new Vector2(0f, -off);
    }
    private static void StretchFillBelowWithFooter(RectTransform r, float topOff, float footerH)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(0f, footerH); r.offsetMax = new Vector2(0f, -topOff);
    }
    private static void StretchBottom(RectTransform r, float h)
    {
        r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(0.5f, 0f);
        r.offsetMin = new Vector2(0f, 0f); r.offsetMax = new Vector2(0f, h);
    }
    private static void StretchFull(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }
    private static void StretchContentTop(RectTransform r)
    {
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = Vector2.zero; r.sizeDelta = Vector2.zero;
    }
    private void EnsureVerticalLayout(RectTransform r, bool fit)
    {
        if (r == null) return;
        var v = r.GetComponent<VerticalLayoutGroup>();
        if (v == null) v = r.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperCenter; v.childControlWidth = true; v.childControlHeight = false;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false; v.spacing = 2f;
        var f = r.GetComponent<ContentSizeFitter>();
        if (f == null) f = r.gameObject.AddComponent<ContentSizeFitter>();
        f.verticalFit = fit ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
    }

    // ── Row pooling ─────────────────────────────────────────────

    private void EnsureRowCount(int count)
    {
        while (rows.Count < count)
        {
            int i = rows.Count;
            rows.Add(CreateRow(contentRoot, $"Row_{i + 1}", i % 2 == 0 ? oddRowBackground : evenRowBackground, rowFontSize));
        }
    }
    private void SetActiveRowCount(int count)
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].root != null) rows[i].root.gameObject.SetActive(i < count);
    }
    private static RectTransform FindOrCreateRect(RectTransform parent, string name)
    {
        Transform e = parent.Find(name);
        if (e != null && e.TryGetComponent(out RectTransform er)) return er;
        var o = new GameObject(name, typeof(RectTransform));
        o.transform.SetParent(parent, false);
        return o.GetComponent<RectTransform>();
    }

    private RowView FindOrCreateHeaderRow()
    {
        if (headerRoot != null)
        {
            Transform e = headerRoot.Find("Header");
            if (e != null && e.TryGetComponent(out RectTransform er)) return BindRow(er, headerBackground, headerFontSize, true);
        }
        return CreateRow(headerRoot, "Header", headerBackground, headerFontSize, true);
    }

    private RowView CreateRow(RectTransform parent, string name, Color bg, int fontSize, bool header = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent != null ? parent : transform, false);
        return BindRow(go.GetComponent<RectTransform>(), bg, fontSize, header);
    }

    private RowView BindRow(RectTransform rect, Color bg, int fontSize, bool header)
    {
        GameObject go = rect.gameObject;
        if (header) StretchFull(rect);
        else
        {
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f); rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, rowHeight);
        }
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = bg;
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = header; le.minHeight = header ? 0f : rowHeight; le.preferredHeight = header ? 0f : rowHeight;

        var h = go.GetComponent<HorizontalLayoutGroup>();
        if (h == null) h = go.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = true; h.spacing = columnSpacing; h.padding = new RectOffset(8, 8, 2, 2);

        var rv = new RowView { root = rect };
        rv.rankText = FindOrCreateCell(go.transform, "Rank", header ? mutedTextColor : accentTextColor, fontSize, 22f);
        rv.trackIdText = FindOrCreateCell(go.transform, "TrackId", textColor, fontSize, 64f);
        rv.varText = FindOrCreateVarInput(go.transform, "VarName", textColor, fontSize, 52f);
        rv.valueText = FindOrCreateCell(go.transform, "Value", mutedTextColor, fontSize, 58f);
        rv.ruleInput = FindOrCreateRuleInput(go.transform, "Rule", textColor, fontSize, 62f);
        rv.statusText = FindOrCreateCell(go.transform, "Status", textColor, fontSize, 40f);
        BindMarkerControls(rv, go.transform, header);
        return rv;
    }

    private void BindMarkerControls(RowView rv, Transform parent, bool header)
    {
        rv.colorButton = FindOrCreateColorButton(parent, "MarkerColor", 28f);
        rv.colorSwatch = rv.colorButton != null ? rv.colorButton.GetComponent<Image>() : null;
        if (rv.colorButton != null)
        {
            rv.colorButton.onClick.RemoveAllListeners();
            if (!header)
                rv.colorButton.onClick.AddListener(() => CycleMarkerColor(rv));
        }

        rv.showButton = FindOrCreateShowButton(parent, "MarkerShow", 28f, out rv.showButtonLabel);
        if (rv.showButton != null)
        {
            rv.showButton.onClick.RemoveAllListeners();
            if (!header)
                rv.showButton.onClick.AddListener(() => ToggleMarkerVisible(rv));
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
            if (rv.showButton != null) rv.showButton.gameObject.SetActive(true);
            if (rv.showButtonLabel != null) rv.showButtonLabel.text = "显示";
        }
    }

    private void CycleMarkerColor(RowView row)
    {
        if (row == null || row.colorSwatch == null)
            return;
        int best = 0;
        float bestDist = float.MaxValue;
        Color current = row.colorSwatch.color;
        for (int i = 0; i < MarkerPalette.Length; i++)
        {
            Color d = MarkerPalette[i] - current;
            float dist = d.r * d.r + d.g * d.g + d.b * d.b;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }
        row.colorSwatch.color = MarkerPalette[(best + 1) % MarkerPalette.Length];
        var progress = GetProgressController();
        if (progress != null)
            progress.SetMarkerGroupColor(ViolationKey(row.boundTrackId, row.boundFieldKey), row.colorSwatch.color);
    }

    private void ToggleMarkerVisible(RowView row)
    {
        if (row == null || row.showButtonLabel == null)
            return;
        bool hidden = row.showButtonLabel.text == "隐";
        bool show = hidden;
        row.showButtonLabel.text = show ? "显" : "隐";
        var progress = GetProgressController();
        if (progress != null)
            progress.SetMarkerGroupVisible(ViolationKey(row.boundTrackId, row.boundFieldKey), show);
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

    private Text FindOrCreateVarInput(Transform parent, string name, Color color, int fontSize, float width)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            var existingText = existing.GetComponent<Text>();
            if (existingText != null)
            {
                ConfigureCell(existingText, color, fontSize, width);
                var input = existing.GetComponent<InputField>();
                if (input == null)
                {
                    input = existing.gameObject.AddComponent<InputField>();
                    input.targetGraphic = existingText;
                    input.textComponent = existingText;
                }
                // Ensure autocomplete is wired
                input.onValueChanged.RemoveListener(OnVarNameInputChanged);
                input.onValueChanged.AddListener(OnVarNameInputChanged);
                return existingText;
            }
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        ConfigureCell(t, color, fontSize, width);
        var input2 = go.AddComponent<InputField>();
        input2.targetGraphic = t;
        input2.textComponent = t;
        input2.onValueChanged.AddListener(OnVarNameInputChanged);
        return t;
    }

    // ── Autocomplete for VarName InputField ────────────────────

    private Dropdown suggestionDropdown;
    private bool isSelectingSuggestion;
    private InputField activeVarInput; // The InputField that triggered the suggestion

    private void OnVarNameInputChanged(string text)
    {
        // Store which InputField triggered this
        var eventSys = UnityEngine.EventSystems.EventSystem.current;
        if (eventSys != null && eventSys.currentSelectedGameObject != null)
        {
            var input = eventSys.currentSelectedGameObject.GetComponent<InputField>();
            if (input != null)
                activeVarInput = input;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            HideSuggestionPanel();
            return;
        }

        text = text.Trim().ToLowerInvariant();
        var matches = new List<string>();
        foreach (var f in AllFields)
        {
            if (f.key.ToLowerInvariant().Contains(text) || f.displayName.Contains(text))
                matches.Add($"{f.key} ({f.displayName})");
        }

        if (matches.Count == 0)
        {
            HideSuggestionPanel();
            return;
        }

        ShowSuggestionPanel(matches);
    }

    private void ShowSuggestionPanel(List<string> matches)
    {
        RectTransform container = transform.Find("DataRuleConfigRoot") as RectTransform;
        if (container == null) return;

        // Find or create suggestion overlay
        RectTransform overlay = FindOrCreateRect(container, "SuggestionOverlay");
        if (overlay.GetComponent<Image>() == null)
            overlay.gameObject.AddComponent<Image>();
        overlay.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.1f, 0.98f);
        overlay.anchorMin = new Vector2(0f, 0f);
        overlay.anchorMax = new Vector2(1f, 1f);
        overlay.pivot = new Vector2(0.5f, 0.5f);
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;
        overlay.SetAsLastSibling();

        // Build clickable buttons for each match instead of Dropdown
        // Clear old buttons
        for (int i = overlay.childCount - 1; i >= 0; i--)
        {
            var child = overlay.GetChild(i);
            if (child.name.StartsWith("MatchBtn_"))
                DestroyImmediate(child.gameObject);
        }

        // Remove old Title and Dropdown
        var oldTitle = overlay.Find("Title");
        if (oldTitle != null) DestroyImmediate(oldTitle.gameObject);
        var oldDD = overlay.Find("SuggestionDropdown");
        if (oldDD != null) DestroyImmediate(oldDD.gameObject);
        var oldClose = overlay.Find("CloseBtn");
        if (oldClose != null) DestroyImmediate(oldClose.gameObject);

        // Title
        RectTransform titleRect = FindOrCreateRect(overlay, "Title");
        var titleText = EnsureText(titleRect, "Label", mutedTextColor, TextAnchor.MiddleCenter);
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleText.rectTransform.sizeDelta = new Vector2(0f, 24f);
        titleText.rectTransform.anchoredPosition = Vector2.zero;
        titleText.text = $"候选变量名 ({matches.Count}) —点击选择";

        // Close button
        RectTransform closeRect = FindOrCreateRect(overlay, "CloseBtn");
        closeRect.anchorMin = new Vector2(1f, 1f); closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f); closeRect.sizeDelta = new Vector2(60f, 24f);
        closeRect.offsetMin = new Vector2(-60f, -24f); closeRect.offsetMax = Vector2.zero;
        var cImg = closeRect.GetComponent<Image>();
        if (cImg == null) cImg = closeRect.gameObject.AddComponent<Image>();
        cImg.color = new Color(0.6f, 0.2f, 0.2f, 0.9f);
        var cBtn = closeRect.GetComponent<Button>();
        if (cBtn == null) cBtn = closeRect.gameObject.AddComponent<Button>();
        cBtn.targetGraphic = cImg;
        cBtn.onClick.RemoveAllListeners();
        cBtn.onClick.AddListener(HideSuggestionPanel);
        var cText = EnsureText(closeRect, "Label", Color.white, TextAnchor.MiddleCenter);
        cText.rectTransform.anchorMin = Vector2.zero; cText.rectTransform.anchorMax = Vector2.one;
        cText.rectTransform.offsetMin = Vector2.zero; cText.rectTransform.offsetMax = Vector2.zero;
        cText.text = "关闭";

        // Create clickable buttons for each match
        float btnHeight = rowHeight + 4f;
        float startY = -30f; // below title
        for (int i = 0; i < matches.Count; i++)
        {
            string match = matches[i];
            string key = match.Split(' ')[0];

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
            btn.onClick.AddListener(() => OnSuggestionButtonClicked(key));

            var btnLabel = EnsureText(btnRect, "Label", textColor, TextAnchor.MiddleLeft);
            btnLabel.rectTransform.anchorMin = Vector2.zero; btnLabel.rectTransform.anchorMax = Vector2.one;
            btnLabel.rectTransform.offsetMin = new Vector2(10f, 0f); btnLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);
            btnLabel.text = match;
        }

        overlay.gameObject.SetActive(true);
    }

    private void OnSuggestionButtonClicked(string key)
    {
        if (activeVarInput != null)
        {
            activeVarInput.text = key;

            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null)
            {
                es.SetSelectedGameObject(activeVarInput.gameObject);
                activeVarInput.ActivateInputField();
            }

            Debug.Log($"[DataRuleConfigPanel] Filled '{key}' into InputField");
        }

        activeVarInput = null;
        HideSuggestionPanel();
    }

    private void HideSuggestionPanel()
    {
        RectTransform container = transform.Find("DataRuleConfigRoot") as RectTransform;
        if (container == null) return;
        var overlay = container.Find("SuggestionOverlay");
        if (overlay != null)
            overlay.gameObject.SetActive(false);
    }

    private Text FindOrCreateCell(Transform parent, string name, Color color, int fontSize, float width)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out Text existingText))
        {
            ConfigureCell(existingText, color, fontSize, width);
            return existingText;
        }
        return CreateCell(parent, name, color, fontSize, width);
    }

    private InputField FindOrCreateRuleInput(Transform parent, string name, Color color, int fontSize, float width)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out InputField existingInput))
        {
            var le = existing.GetComponent<LayoutElement>();
            if (le == null) le = existing.gameObject.AddComponent<LayoutElement>();
            le.minWidth = width; le.preferredWidth = width; le.flexibleWidth = 0f;
            return existingInput;
        }
        return CreateRuleInput(parent, name, color, fontSize, width);
    }

    private Text CreateCell(Transform parent, string name, Color color, int fontSize, float width)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        ConfigureCell(t, color, fontSize, width);
        return t;
    }

    private void ConfigureCell(Text t, Color color, int fontSize, float width)
    {
        t.font = font; t.fontSize = fontSize; t.color = color; t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Truncate;
        var le = t.GetComponent<LayoutElement>();
        if (le == null) le = t.gameObject.AddComponent<LayoutElement>();
        le.minWidth = width; le.preferredWidth = width; le.flexibleWidth = 0f;
    }

    private InputField CreateRuleInput(Transform parent, string name, Color color, int fontSize, float width)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minWidth = width; le.preferredWidth = width; le.flexibleWidth = 0f;

        var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(go.transform, false);
        var bgRT = bgGo.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
        bgGo.GetComponent<Image>().color = inputBgColor;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(bgGo.transform, false);
        var textRT = textGo.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(4f, 0f); textRT.offsetMax = new Vector2(-4f, 0f);
        var t = textGo.GetComponent<Text>();
        t.font = font; t.fontSize = fontSize; t.color = color; t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.supportRichText = false;

        var input = go.AddComponent<InputField>();
        input.targetGraphic = bgGo.GetComponent<Image>();
        input.textComponent = t; input.text = "";
        return input;
    }

    private void SetHeaderTexts(RowView row)
    {
        if (row == null) return;
        if (row.rankText != null) row.rankText.text = "#";
        if (row.trackIdText != null) { row.trackIdText.text = "飞机编号"; row.trackIdText.gameObject.SetActive(true); }
        if (row.varText != null)
        {
            var input = row.varText.GetComponent<InputField>();
            if (input != null) { input.interactable = false; input.text = "变量名"; }
            else row.varText.text = "变量名";
        }
        if (row.valueText != null) row.valueText.text = "当前值";
        if (row.ruleInput != null) { row.ruleInput.interactable = false; row.ruleInput.text = "规则"; }
        if (row.statusText != null) row.statusText.text = "状态";
        if (row.showButtonLabel != null) row.showButtonLabel.text = "显示";
    }
}
