using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlaneInfoGrid : MonoBehaviour
{
    [Serializable]
    private class RowView
    {
        public RectTransform root;
        public Text rankText;
        public Text idText;
        public Text longitudeText;
        public Text latitudeText;
        public Text altitudeText;
    }

    [Header("Data")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private bool autoFindReceiver = true;
    [SerializeField] private float refreshInterval = 0.1f;

    [Header("Layout")]
    [SerializeField] private RectTransform headerRoot;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private bool autoCreateScrollLayout = false;
    [SerializeField] private float headerHeight = 34f;
    [SerializeField] private float rowHeight = 30f;
    [SerializeField] private float scrollbarWidth = 14f;
    [SerializeField] private float columnSpacing = 8f;
    [SerializeField] private int maxRows = 32;
    [SerializeField] private bool showHeader = true;

    [Header("Style")]
    [SerializeField] private Font font;
    [SerializeField] private int headerFontSize = 14;
    [SerializeField] private int rowFontSize = 13;
    [SerializeField] private Color panelBackground = new Color(0.06f, 0.08f, 0.1f, 0.7f);
    [SerializeField] private Color headerBackground = new Color(0.08f, 0.1f, 0.13f, 0.95f);
    [SerializeField] private Color oddRowBackground = new Color(0.12f, 0.14f, 0.18f, 0.82f);
    [SerializeField] private Color evenRowBackground = new Color(0.16f, 0.18f, 0.22f, 0.82f);
    [SerializeField] private Color textColor = new Color(0.92f, 0.96f, 1f, 1f);
    [SerializeField] private Color mutedTextColor = new Color(0.66f, 0.74f, 0.82f, 1f);
    [SerializeField] private Color accentTextColor = new Color(0.43f, 0.82f, 1f, 1f);

    private readonly List<RowView> rows = new List<RowView>();
    private RowView headerRow;
    private float nextRefreshTime;

    private void Awake()
    {
        if (contentRoot == null)
        {
            contentRoot = GetComponent<RectTransform>();
        }

        if (autoCreateScrollLayout && (headerRoot == null || contentRoot == null || contentRoot == GetComponent<RectTransform>()))
        {
            EnsureScrollLayoutObjects();
        }

        if (headerRoot == null)
        {
            headerRoot = contentRoot;
        }

        ApplyHeaderViewportInset();

        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        EnsurePanelBackground();

        EnsureLayout(headerRoot, false);
        EnsureLayout(contentRoot, true);
        if (showHeader)
        {
            headerRow = FindOrCreateHeaderRow();
            SetRowTexts(headerRow, "#", "飞机编号", "经度", "纬度", "高度");
        }
    }

    private void EnsureScrollLayoutObjects()
    {
        RectTransform panel = GetComponent<RectTransform>();
        if (panel == null)
        {
            return;
        }

        headerRoot = FindOrCreateRect(panel, "HeaderRoot");
        StretchTop(headerRoot, headerHeight, 0f);
        headerRoot.offsetMax = new Vector2(-scrollbarWidth - 2f, 0f);

        RectTransform scrollRoot = FindOrCreateRect(panel, "Scroll View");
        StretchFillBelow(scrollRoot, headerHeight);

        var scrollImage = scrollRoot.GetComponent<Image>();
        if (scrollImage == null)
        {
            scrollImage = scrollRoot.gameObject.AddComponent<Image>();
        }
        scrollImage.color = new Color(0f, 0f, 0f, 0f);
        scrollImage.raycastTarget = true;

        RectTransform viewport = FindOrCreateRect(scrollRoot, "Viewport");
        StretchViewport(viewport);

        var viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null)
        {
            viewportImage = viewport.gameObject.AddComponent<Image>();
        }
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;

        var mask = viewport.GetComponent<Mask>();
        if (mask == null)
        {
            mask = viewport.gameObject.AddComponent<Mask>();
        }
        mask.showMaskGraphic = false;

        contentRoot = FindOrCreateRect(viewport, "ContentRoot");
        StretchContentTop(contentRoot);

        var scrollRect = scrollRoot.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        }
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

        var background = scrollbarRoot.GetComponent<Image>();
        if (background == null)
        {
            background = scrollbarRoot.gameObject.AddComponent<Image>();
        }
        background.color = new Color(0.04f, 0.05f, 0.07f, 0.65f);
        background.raycastTarget = true;

        RectTransform slidingArea = FindOrCreateRect(scrollbarRoot, "Sliding Area");
        StretchFull(slidingArea);
        slidingArea.offsetMin = new Vector2(2f, 2f);
        slidingArea.offsetMax = new Vector2(-2f, -2f);

        RectTransform handle = FindOrCreateRect(slidingArea, "Handle");
        StretchFull(handle);

        var handleImage = handle.GetComponent<Image>();
        if (handleImage == null)
        {
            handleImage = handle.gameObject.AddComponent<Image>();
        }
        handleImage.color = new Color(0.43f, 0.82f, 1f, 0.9f);
        handleImage.raycastTarget = true;

        var scrollbar = scrollbarRoot.GetComponent<Scrollbar>();
        if (scrollbar == null)
        {
            scrollbar = scrollbarRoot.gameObject.AddComponent<Scrollbar>();
        }
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.transition = Selectable.Transition.ColorTint;
        scrollbar.size = 1f;
        scrollbar.value = 1f;

        return scrollbar;
    }

    private void EnsurePanelBackground()
    {
        var background = GetComponent<Image>();
        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
        }

        background.color = panelBackground;
    }

    private void ApplyHeaderViewportInset()
    {
        if (headerRoot == null || headerRoot == contentRoot)
        {
            return;
        }

        headerRoot.offsetMax = new Vector2(-scrollbarWidth - 2f, headerRoot.offsetMax.y);
    }

#if UNITY_EDITOR
    [ContextMenu("Create Fixed Header Scroll Layout")]
    private void CreateFixedHeaderScrollLayoutInEditor()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Create Plane Info Grid Layout");

        EnsureScrollLayoutObjects();

        EditorUtility.SetDirty(this);
        if (headerRoot != null)
        {
            EditorUtility.SetDirty(headerRoot.gameObject);
        }
        if (contentRoot != null)
        {
            EditorUtility.SetDirty(contentRoot.gameObject);
        }

        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
    }
#endif

    private static RectTransform FindOrCreateRect(RectTransform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
        {
            return existingRect;
        }

        var obj = new GameObject(childName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(obj, $"Create {childName}");
#endif
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
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
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
        rect.sizeDelta = new Vector2(0f, 0f);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);
        Refresh();
    }

    public void Refresh()
    {
        if (receiver == null && autoFindReceiver)
        {
            receiver = FlightDataStreamReceiver.Instance;
        }

        if (receiver == null)
        {
            SetActiveRowCount(0);
            return;
        }

        List<FlightDataStreamReceiver.TrackPosition> positions = receiver.SnapshotPositions;
        positions.Sort((a, b) => string.Compare(a?.trackId, b?.trackId, StringComparison.Ordinal));

        int count = Mathf.Min(maxRows, positions.Count);
        EnsureRowCount(count);
        SetActiveRowCount(count);

        for (int i = 0; i < count; i++)
        {
            var pos = positions[i];
            string trackId = string.IsNullOrWhiteSpace(pos.trackId) ? "unknown" : pos.trackId;
            SetRowTexts(
                rows[i],
                (i + 1).ToString(),
                trackId,
                pos.lng.ToString("F6"),
                pos.lat.ToString("F6"),
                pos.alt.ToString("F1"));
        }
    }

    private void EnsureLayout(RectTransform root, bool fitHeight)
    {
        if (root == null)
        {
            return;
        }

        var vertical = root.GetComponent<VerticalLayoutGroup>();
        if (vertical == null)
        {
            vertical = root.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = false;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.spacing = 2f;

        var fitter = root.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.verticalFit = fitHeight ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
    }

    private void EnsureRowCount(int count)
    {
        while (rows.Count < count)
        {
            int index = rows.Count;
            Color bg = index % 2 == 0 ? oddRowBackground : evenRowBackground;
            rows.Add(CreateRow(contentRoot, $"PlaneInfoRow_{index + 1}", bg, rowFontSize, false));
        }
    }

    private RowView FindOrCreateHeaderRow()
    {
        if (headerRoot != null)
        {
            Transform existing = headerRoot.Find("Header");
            if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
            {
                return BindRow(existingRect, headerBackground, headerFontSize, true, true);
            }
        }

        return CreateRow(headerRoot, "Header", headerBackground, headerFontSize, true);
    }

    private void SetActiveRowCount(int count)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].root != null)
            {
                rows[i].root.gameObject.SetActive(i < count);
            }
        }
    }

    private RowView CreateRow(RectTransform parentRoot, string rowName, Color background, int fontSize, bool header)
    {
        var rowObject = new GameObject(rowName, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObject.transform.SetParent(parentRoot != null ? parentRoot : transform, false);

        return BindRow(rowObject.GetComponent<RectTransform>(), background, fontSize, header, false);
    }

    private RowView BindRow(RectTransform rect, Color background, int fontSize, bool header, bool preserveExistingHeaderCells)
    {
        GameObject rowObject = rect.gameObject;

        if (header)
        {
            StretchFull(rect);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, rowHeight);
            rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
            rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
        }

        var image = rowObject.GetComponent<Image>();
        if (image == null)
        {
            image = rowObject.AddComponent<Image>();
        }
        image.color = background;

        var layoutElement = rowObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = rowObject.AddComponent<LayoutElement>();
        }
        layoutElement.ignoreLayout = header;
        layoutElement.minHeight = header ? 0f : rowHeight;
        layoutElement.preferredHeight = header ? 0f : rowHeight;

        var horizontal = rowObject.GetComponent<HorizontalLayoutGroup>();
        if (preserveExistingHeaderCells)
        {
            if (horizontal != null)
            {
                horizontal.enabled = false;
            }
        }
        else
        {
            if (horizontal == null)
            {
                horizontal = rowObject.AddComponent<HorizontalLayoutGroup>();
            }
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
            rankText = FindOrCreateCell(rowObject.transform, "Rank", header ? mutedTextColor : accentTextColor, fontSize, 42f, preserveExistingHeaderCells),
            idText = FindOrCreateCell(rowObject.transform, "PlaneId", textColor, fontSize, 112f, preserveExistingHeaderCells),
            longitudeText = FindOrCreateCell(rowObject.transform, "Longitude", textColor, fontSize, 86f, preserveExistingHeaderCells),
            latitudeText = FindOrCreateCell(rowObject.transform, "Latitude", textColor, fontSize, 86f, preserveExistingHeaderCells),
            altitudeText = FindOrCreateCell(rowObject.transform, "Altitude", textColor, fontSize, 78f, preserveExistingHeaderCells),
        };
    }

    private Text FindOrCreateCell(Transform parent, string name, Color color, int fontSize, float width, bool preserveExisting)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out Text existingText))
        {
            if (!preserveExisting)
            {
                ConfigureCell(existingText, name, color, fontSize, width);
            }
            return existingText;
        }

        return CreateCell(parent, name, color, fontSize, width);
    }

    private Text CreateCell(Transform parent, string name, Color color, int fontSize, float width)
    {
        var cellObject = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        cellObject.transform.SetParent(parent, false);

        var text = cellObject.GetComponent<Text>();
        ConfigureCell(text, name, color, fontSize, width);
        return text;
    }

    private void ConfigureCell(Text text, string name, Color color, int fontSize, float width)
    {
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        var layoutElement = text.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = text.gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.flexibleWidth = 0f;
    }

    private void SetRowTexts(RowView row, string rank, string planeId, string longitude, string latitude, string altitude)
    {
        if (row == null)
        {
            return;
        }

        if (row.rankText != null) row.rankText.text = rank;
        if (row.idText != null) row.idText.text = planeId;
        if (row.longitudeText != null) row.longitudeText.text = longitude;
        if (row.latitudeText != null) row.latitudeText.text = latitude;
        if (row.altitudeText != null) row.altitudeText.text = altitude;
    }
}
