#pragma warning disable CS0414
using System;
using System.Collections.Generic;
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
        public Text rangeTypeText;
        public Text rangeText;
        public Text yawText;
        public Text pitchText;
        public Text centerText;
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

    private readonly List<RowView> rows = new List<RowView>();
    private readonly List<RectTransform> groupHeaders = new List<RectTransform>();
    private readonly List<string> dropdownTrackIds = new List<string>();
    private const string AllPlanesOption = "__ALL__";
    


























































    private RowView headerRow;
    private float nextRefreshTime;
    private string selectedTrackId = AllPlanesOption;
    private bool layoutInitialized;

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
            SetRowTexts(headerRow, "#", "飞机编号", "探测器类型", "范围类型", "探测范围", "左右偏转角", "上下偏转角", "中心点");
        }

        layoutInitialized = true;
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
            SetRowTexts(headerRow, "#", "飞机编号", "探测器类型", "范围类型", "探测范围", "左右偏转角", "上下偏转角", "中心点");
        }

        layoutInitialized = true;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;
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
        UpdateDropdownOptions(states);

        // Group by trackId
        var grouped = new Dictionary<string, List<FlightDataStreamReceiver.TrackTimetableState>>();
        if (states != null)
        {
            foreach (var s in states)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.trackId))
                    continue;
                if (selectedTrackId != AllPlanesOption && s.trackId != selectedTrackId)
                    continue;
                if (!grouped.ContainsKey(s.trackId))
                    grouped[s.trackId] = new List<FlightDataStreamReceiver.TrackTimetableState>();
                grouped[s.trackId].Add(s);
            }
        }

        // Build display list: group header rows + sensor rows
        int totalRows = 0;
        var displayItems = new List<DisplayItem>();
        var sortedKeys = new List<string>(grouped.Keys);
        sortedKeys.Sort(StringComparer.Ordinal);

        foreach (string trackId in sortedKeys)
        {
            displayItems.Add(new DisplayItem { type = DisplayType.GroupHeader, trackId = trackId });
            totalRows++;
            int sensorIndex = 1;
            foreach (var s in grouped[trackId])
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
                string range = s.detectRange > 0 ? $"{s.detectRange:F0}m" : "-";
                string yaw = s.yawAngle != 0 ? $"{s.yawAngle:F1}°" : "-";
                string pitch = s.pitchAngle != 0 ? $"{s.pitchAngle:F1}°" : "-";
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
    }

    private void ConfigureSensorRow(RowView row, string rank, string planeId, string detector, string rangeType, string range, string yaw, string pitch, string center)
    {
        if (row == null) return;

        // Restore normal row styling
        int visualIndex = row.root.GetSiblingIndex();
        var img = row.root.GetComponent<Image>();
        if (img != null) img.color = visualIndex % 2 == 0 ? oddRowBackground : evenRowBackground;

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
        if (row.rangeText != null) row.rangeText.text = range;
        if (row.yawText != null) row.yawText.text = yaw;
        if (row.pitchText != null) row.pitchText.text = pitch;
        if (row.centerText != null) row.centerText.text = center;
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
            rows.Add(CreateRow(contentRoot, $"SensorRow_{index + 1}", bg, rowFontSize));
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

        return new RowView
        {
            root = rect,
            rankText = FindOrCreateCell(rowObject.transform, "Rank", header ? mutedTextColor : accentTextColor, fontSize, 30f, 0f, preserveExistingHeaderCells),
            idText = FindOrCreateCell(rowObject.transform, "PlaneId", textColor, fontSize, 90f, 0f, preserveExistingHeaderCells),
            detectorText = FindOrCreateCell(rowObject.transform, "Detector", textColor, fontSize, 70f, 0f, preserveExistingHeaderCells),
            rangeTypeText = FindOrCreateCell(rowObject.transform, "RangeType", mutedTextColor, fontSize, 50f, 0f, preserveExistingHeaderCells),
            rangeText = FindOrCreateCell(rowObject.transform, "Range", mutedTextColor, fontSize, 70f, 0f, preserveExistingHeaderCells),
            yawText = FindOrCreateCell(rowObject.transform, "Yaw", mutedTextColor, fontSize, 50f, 0f, preserveExistingHeaderCells),
            pitchText = FindOrCreateCell(rowObject.transform, "Pitch", mutedTextColor, fontSize, 50f, 0f, preserveExistingHeaderCells),
            centerText = FindOrCreateCell(rowObject.transform, "Center", mutedTextColor, fontSize, 40f, 0f, preserveExistingHeaderCells),
        };
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
