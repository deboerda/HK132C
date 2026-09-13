using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AlarmPanel : MonoBehaviour
{
    [Serializable]
    private class RowView
    {
        public RectTransform root;
        public Text rankText;
        public Text idText;
        public Text alarmText;
        public Text stateText;
        public Text batteryText;
    }

    private class AlarmEntry
    {
        public string key;
        public string flightTime;
        public string trackId;
        public string alarm;
        public string state;
        public float battery;
    }

    [Header("Data")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private bool autoFindReceiver = true;
    [SerializeField] private float refreshInterval = 0.1f;

    [Header("Layout")]
    [SerializeField] private Dropdown planeDropdown;
    [SerializeField] private RectTransform selectorRoot;
    [SerializeField] private RectTransform headerRoot;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private bool autoCreateLayout = true;
    [SerializeField] private float selectorHeight = 34f;
    [SerializeField] private float headerHeight = 32f;
    [SerializeField] private float rowHeight = 30f;
    [SerializeField] private float scrollbarWidth = 14f;
    [SerializeField] private float columnSpacing = 8f;
    [SerializeField] private int maxRows = 32;
    [SerializeField] private int maxHistoryEntries = 256;

    [Header("Style")]
    [SerializeField] private Font font;
    [SerializeField] private int headerFontSize = 13;
    [SerializeField] private int rowFontSize = 13;
    [SerializeField] private Color panelBackground = new Color(0.06f, 0.08f, 0.1f, 0.7f);
    [SerializeField] private Color headerBackground = new Color(0.08f, 0.1f, 0.13f, 0.95f);
    [SerializeField] private Color oddRowBackground = new Color(0.12f, 0.14f, 0.18f, 0.82f);
    [SerializeField] private Color evenRowBackground = new Color(0.16f, 0.18f, 0.22f, 0.82f);
    [SerializeField] private Color textColor = new Color(0.92f, 0.96f, 1f, 1f);
    [SerializeField] private Color mutedTextColor = new Color(0.66f, 0.74f, 0.82f, 1f);
    [SerializeField] private Color accentTextColor = new Color(0.43f, 0.82f, 1f, 1f);
    [SerializeField] private Color alarmNormalColor = new Color(0.4f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color alarmWarningColor = new Color(1f, 0.78f, 0f, 1f);
    [SerializeField] private Color alarmDangerColor = new Color(1f, 0.3f, 0.3f, 1f);

    private readonly List<RowView> rows = new List<RowView>();
    private readonly List<AlarmEntry> alarmHistory = new List<AlarmEntry>();
    private readonly HashSet<string> alarmKeys = new HashSet<string>();
    private readonly List<string> dropdownTrackIds = new List<string>();
    private const string AllPlanesOption = "__ALL__";
    private RowView headerRow;
    private float nextRefreshTime;
    private string selectedTrackId = AllPlanesOption;

    private void Awake()
    {
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
            SetRowTexts(headerRow, "#", "飞机编号", "告警信息", "状态", "电量");
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);
        Refresh();
    }

    public void Refresh()
    {
        if (receiver == null && autoFindReceiver)
            receiver = FlightDataStreamReceiver.Instance;

        if (receiver == null)
        {
            SetActiveRowCount(0);
            return;
        }

        var states = receiver.SnapshotStates;
        states.Sort((a, b) => string.Compare(a?.trackId, b?.trackId, StringComparison.Ordinal));
        AppendAlarmEntries(states);
        UpdateDropdownOptions(states);

        var visibleEntries = new List<AlarmEntry>();
        foreach (var entry in alarmHistory)
        {
            if (entry == null)
            {
                continue;
            }

            if (selectedTrackId == AllPlanesOption || entry.trackId == selectedTrackId)
            {
                visibleEntries.Add(entry);
            }
        }

        int count = Mathf.Min(Mathf.Max(0, maxRows), visibleEntries.Count);
        EnsureRowCount(count);
        SetActiveRowCount(count);

        for (int i = 0; i < count; i++)
        {
            var entry = visibleEntries[i];
            string id = string.IsNullOrWhiteSpace(entry.trackId) ? "unknown" : entry.trackId;
            string alarm = string.IsNullOrWhiteSpace(entry.alarm) ? "正常" : entry.alarm;
            string stateStr = string.IsNullOrWhiteSpace(entry.state) ? "-" : entry.state;
            string battery = entry.battery > 0 ? $"{entry.battery:F1}%" : "-";

            SetRowTexts(rows[i], (i + 1).ToString(), id, alarm, stateStr, battery);

            if (rows[i].alarmText != null)
            {
                rows[i].alarmText.color = GetAlarmColor(alarm);
            }
        }
    }

    private void AppendAlarmEntries(List<FlightDataStreamReceiver.TrackTimetableState> states)
    {
        if (states == null)
        {
            return;
        }

        var newEntries = new List<AlarmEntry>();
        foreach (var state in states)
        {
            if (state == null)
            {
                continue;
            }

            string trackId = string.IsNullOrWhiteSpace(state.trackId) ? "unknown" : state.trackId;
            string alarm = string.IsNullOrWhiteSpace(state.alarm) ? "正常" : state.alarm;
            string stateText = string.IsNullOrWhiteSpace(state.state) ? "-" : state.state;
            string flightTime = string.IsNullOrWhiteSpace(state.flightTime) ? Time.frameCount.ToString() : state.flightTime;
            string key = $"{flightTime}|{trackId}|{alarm}|{stateText}";

            if (alarmKeys.Contains(key))
            {
                continue;
            }

            alarmKeys.Add(key);
            newEntries.Add(new AlarmEntry
            {
                key = key,
                flightTime = flightTime,
                trackId = trackId,
                alarm = alarm,
                state = stateText,
                battery = state.battery,
            });
        }

        newEntries.Sort((a, b) =>
        {
            int timeCompare = string.Compare(b.flightTime, a.flightTime, StringComparison.Ordinal);
            if (timeCompare != 0) return timeCompare;
            int severityCompare = GetAlarmSeverity(b.alarm).CompareTo(GetAlarmSeverity(a.alarm));
            if (severityCompare != 0) return severityCompare;
            return string.Compare(a.trackId, b.trackId, StringComparison.Ordinal);
        });

        for (int i = newEntries.Count - 1; i >= 0; i--)
        {
            alarmHistory.Insert(0, newEntries[i]);
        }

        int historyLimit = Mathf.Max(maxRows, maxHistoryEntries);
        while (alarmHistory.Count > historyLimit)
        {
            var tail = alarmHistory[alarmHistory.Count - 1];
            if (tail != null)
            {
                alarmKeys.Remove(tail.key);
            }
            alarmHistory.RemoveAt(alarmHistory.Count - 1);
        }
    }

    private int GetAlarmSeverity(string alarm)
    {
        if (string.IsNullOrWhiteSpace(alarm) || alarm == "正常")
        {
            return 0;
        }

        string value = alarm.ToLowerInvariant();
        if (alarm.Contains("被锁定") ||
            value.Contains("locked") ||
            value.Contains("danger") ||
            alarm.Contains("严重") ||
            alarm.Contains("紧急") ||
            value.Contains("critical"))
        {
            return 3;
        }

        if (alarm.Contains("左侧引擎故障") ||
            alarm.Contains("右侧引擎故障") ||
            alarm.Contains("引擎故障") ||
            alarm.Contains("雷达故障") ||
            value.Contains("engine") ||
            value.Contains("radar") ||
            value.Contains("fault") ||
            value.Contains("failure"))
        {
            return 2;
        }

        return 1;
    }

    private Color GetAlarmColor(string alarm)
    {
        int severity = GetAlarmSeverity(alarm);
        if (severity >= 3)
        {
            return alarmDangerColor;
        }

        if (severity >= 1)
        {
            return alarmWarningColor;
        }

        return alarmNormalColor;
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
        if (planeDropdown == null)
        {
            return;
        }

        var trackIds = new List<string> { AllPlanesOption };
        foreach (var state in states)
        {
            string trackId = state?.trackId;
            if (!string.IsNullOrWhiteSpace(trackId) && !trackIds.Contains(trackId))
            {
                trackIds.Add(trackId);
            }
        }

        bool changed = trackIds.Count != dropdownTrackIds.Count;
        if (!changed)
        {
            for (int i = 0; i < trackIds.Count; i++)
            {
                if (trackIds[i] != dropdownTrackIds[i])
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            return;
        }

        dropdownTrackIds.Clear();
        dropdownTrackIds.AddRange(trackIds);

        var options = new List<Dropdown.OptionData>();
        foreach (string trackId in dropdownTrackIds)
        {
            options.Add(new Dropdown.OptionData(trackId == AllPlanesOption ? "全部飞机" : trackId));
        }

        planeDropdown.ClearOptions();
        planeDropdown.AddOptions(options);

        int selectedIndex = dropdownTrackIds.IndexOf(selectedTrackId);
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
            selectedTrackId = AllPlanesOption;
        }

        planeDropdown.SetValueWithoutNotify(selectedIndex);
        planeDropdown.RefreshShownValue();
    }

    // ── Layout creation (mirrors PlaneDifferenceRankingPanel) ──

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

        headerRoot = FindOrCreateRect(panel, "HeaderRoot");
        StretchTop(headerRoot, headerHeight, selectorHeight);
        headerRoot.offsetMax = new Vector2(-scrollbarWidth - 2f, headerRoot.offsetMax.y);

        RectTransform scrollRoot = FindOrCreateRect(panel, "Scroll View");
        StretchFillBelow(scrollRoot, selectorHeight + headerHeight);
        EnsureScrollArea(scrollRoot);

        selectorRoot.SetAsLastSibling();
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
            rows.Add(CreateRow(contentRoot, $"AlarmRow_{index + 1}", bg, rowFontSize));
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
        if (!preserveExistingHeaderCells)
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

        RemoveCellIfExists(rowObject.transform, "Time");

        return new RowView
        {
            root = rect,
            rankText = FindOrCreateCell(rowObject.transform, "Rank", header ? mutedTextColor : accentTextColor, fontSize, 42f, 0f, preserveExistingHeaderCells),
            idText = FindOrCreateCell(rowObject.transform, "PlaneId", textColor, fontSize, 112f, 0f, preserveExistingHeaderCells),
            alarmText = FindOrCreateCell(rowObject.transform, "Alarm", textColor, fontSize, 120f, 0f, preserveExistingHeaderCells),
            stateText = FindOrCreateCell(rowObject.transform, "State", mutedTextColor, fontSize, 86f, 0f, preserveExistingHeaderCells),
            batteryText = FindOrCreateCell(rowObject.transform, "Battery", mutedTextColor, fontSize, 60f, 0f, preserveExistingHeaderCells),
        };
    }

    private static void RemoveCellIfExists(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing == null)
            return;

        if (Application.isPlaying)
            Destroy(existing.gameObject);
        else
            DestroyImmediate(existing.gameObject);
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

    private void SetRowTexts(RowView row, string rank, string planeId, string alarm, string state, string battery)
    {
        if (row == null) return;
        if (row.rankText != null) row.rankText.text = rank;
        if (row.idText != null) row.idText.text = planeId;
        if (row.alarmText != null) row.alarmText.text = alarm;
        if (row.stateText != null) row.stateText.text = state;
        if (row.batteryText != null) row.batteryText.text = battery;
    }
}
