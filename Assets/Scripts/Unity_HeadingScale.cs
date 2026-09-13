using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Unity_HeadingScale : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private string trackId;
    [SerializeField] private bool usePrimaryTrack = true;
    [SerializeField] private bool useManualInput = false;
    [SerializeField] private float manualHeadingDeg;
    [SerializeField] private float commandHeadingDeg;

    [Header("Layout")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;
    [SerializeField] private float pixelsPerDegree = 5f;
    [SerializeField] private Vector2 displaySize = new Vector2(400f, 60f);
    [SerializeField] private Vector2 screenPosition = new Vector2(20f, 60f);
    [SerializeField] private float tickMajorRatio = 0.35f;
    [SerializeField] private float tickMinorRatio = 0.18f;
    [SerializeField] private float tickMajorWidth = 2f;
    [SerializeField] private float tickMinorWidth = 1f;
    [SerializeField] private int fontSize = 14;

    [Header("Style")]
    [SerializeField] private Color tickColor = new Color(0.78f, 0.82f, 0.86f, 1f);
    [SerializeField] private Color minorTickColor = new Color(0.43f, 0.47f, 0.51f, 1f);
    [SerializeField] private Color numberColor = new Color(0.78f, 0.82f, 0.86f, 1f);
    [SerializeField] private Color commandColor = new Color(0f, 1f, 0.39f, 1f);
    [SerializeField] private Color centerColor = new Color(1f, 0.78f, 0f, 1f);
    [SerializeField] private Color readoutColor = new Color(1f, 0.78f, 0f, 1f);
    [SerializeField] private bool useTrueHeading = true;

    private VisualElement root;
    private VisualElement headingScale;
    private VisualElement clipWindow;
    private VisualElement scaleTape;
    private VisualElement centerIndex;
    private VisualElement centerTab;
    private VisualElement commandMarker;
    private Label tmLabel;
    private Label headingReadout;

    private float headingDeg;

    // Cached layout values for BuildTicks / UpdateDisplay
    private float _tickMajorH = 12f;
    private float _tickMinorH = 6f;
    private float _numTop = 1f;
    private float _numHeight = 37f;
    private float _autoFontSize = 14f;

    private void OnEnable()
    {
        EnsureRoot();
    }

    private void EnsureRoot()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("[Unity_HeadingScale] No UIDocument found.");
            return;
        }

        root = uiDocument.rootVisualElement;
        if (root != null)
        {
            ApplyStyleSheet();
            QueryElements();
            ApplyLayout();
            BuildTicks();
            UpdateDisplay();
        }
    }

    private void OnValidate()
    {
        pixelsPerDegree = Mathf.Max(1f, pixelsPerDegree);
        tickMajorRatio = Mathf.Clamp(tickMajorRatio, 0.05f, 0.8f);
        tickMinorRatio = Mathf.Clamp(tickMinorRatio, 0.05f, tickMajorRatio);

#if UNITY_EDITOR
        if (!Application.isPlaying && uiDocument != null && root == null)
            root = uiDocument.rootVisualElement;

        if (root != null)
        {
            QueryElements();
            ApplyLayout();
            BuildTicks();
            UpdateDisplay();
        }
#endif
    }

    private void Update()
    {
        if (root == null)
            EnsureRoot();

        if (root == null || scaleTape == null)
            return;

        UpdateData();
        UpdateDisplay();
    }

    public void SetHeading(float headingDegrees)
    {
        headingDeg = headingDegrees;
        if (root != null && scaleTape != null)
            UpdateDisplay();
    }

    public void SetCommandHeading(float commandHeadingDegrees)
    {
        commandHeadingDeg = commandHeadingDegrees;
    }

    public void SetTrackId(string selectedTrackId)
    {
        trackId = selectedTrackId;
        usePrimaryTrack = string.IsNullOrEmpty(trackId);
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        EnsureRoot();
    }

    private void ApplyStyleSheet()
    {
        if (styleSheet == null)
        {
#if UNITY_EDITOR
            styleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/UI Toolkit/HeadingScale.uss");
#endif
        }

        if (styleSheet != null && root != null)
        {
            if (!root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);
        }
    }

    private void QueryElements()
    {
        headingScale = root.Q<VisualElement>("HeadingScale");
        clipWindow = root.Q<VisualElement>("ClipWindow");
        scaleTape = root.Q<VisualElement>("ScaleTape");
        centerIndex = root.Q<VisualElement>("CenterIndex");
        centerTab = root.Q<VisualElement>("CenterIndexTab");
        commandMarker = root.Q<VisualElement>("CommandMarker");
        tmLabel = root.Q<Label>("TMLabel");
        headingReadout = root.Q<Label>("HeadingReadout");
    }

    private void ApplyLayout()
    {
        if (headingScale == null)
            return;

        float w = displaySize.x;
        float h = displaySize.y;

        // Space allocation: numbers at top, ticks at bottom
        float tickMajorH = Mathf.Max(4f, h * tickMajorRatio);
        float tickMinorH = Mathf.Max(2f, h * tickMinorRatio);

        float numTop = 1f;
        float numAreaH = h - tickMajorH - numTop - 1f;
        if (numAreaH < 6f)
        {
            tickMajorH = Mathf.Max(2f, h * 0.3f);
            tickMinorH = tickMajorH * 0.5f;
            numAreaH = Mathf.Max(6f, h - tickMajorH - numTop - 1f);
        }
        float autoFontSize = Mathf.Max(7f, Mathf.Min(fontSize, numAreaH * 0.85f));

        float tabWidth = Mathf.Max(8f, h * 0.16f);
        float tabHeight = Mathf.Max(3f, h * 0.07f);
        float cmdFontSize = Mathf.Max(8f, numAreaH * 0.65f);
        float labelFontSize = Mathf.Max(8f, numAreaH * 0.45f);
        float readoutFontSize = Mathf.Max(8f, numAreaH * 0.55f);

        headingScale.style.position = Position.Absolute;
        headingScale.style.left = screenPosition.x;
        headingScale.style.top = screenPosition.y;
        headingScale.style.width = w;
        headingScale.style.height = h;
        headingScale.style.overflow = Overflow.Hidden;

        if (clipWindow != null)
        {
            clipWindow.style.position = Position.Absolute;
            clipWindow.style.left = 0f;
            clipWindow.style.top = 0f;
            clipWindow.style.width = w;
            clipWindow.style.height = h;
            clipWindow.style.overflow = Overflow.Hidden;
        }

        if (scaleTape != null)
        {
            scaleTape.style.position = Position.Absolute;
            scaleTape.style.left = 0f;
            scaleTape.style.top = 0f;
            scaleTape.style.width = 540f * pixelsPerDegree;
            scaleTape.style.height = h;
        }

        if (centerIndex != null)
        {
            centerIndex.style.position = Position.Absolute;
            centerIndex.style.left = w * 0.5f;
            centerIndex.style.top = 0f;
            centerIndex.style.width = 2f;
            centerIndex.style.height = h;
            centerIndex.style.marginLeft = -1f;
            centerIndex.style.backgroundColor = centerColor;
        }

        if (centerTab != null)
        {
            centerTab.style.position = Position.Absolute;
            centerTab.style.left = w * 0.5f;
            centerTab.style.top = 0f;
            centerTab.style.width = tabWidth;
            centerTab.style.height = tabHeight;
            centerTab.style.marginLeft = -tabWidth * 0.5f;
            centerTab.style.backgroundColor = centerColor;
        }

        if (commandMarker != null)
        {
            commandMarker.style.position = Position.Absolute;
            commandMarker.style.top = 0f;
            commandMarker.style.fontSize = cmdFontSize;
            commandMarker.style.color = commandColor;
            commandMarker.style.unityFontStyleAndWeight = FontStyle.Bold;
            commandMarker.style.unityTextAlign = TextAnchor.UpperCenter;
            commandMarker.style.width = 20f;
            commandMarker.style.marginLeft = -10f;
            commandMarker.style.paddingTop = 0f;
            commandMarker.style.paddingBottom = 0f;
            commandMarker.style.marginTop = 0f;
            commandMarker.style.marginBottom = 0f;
        }

        if (tmLabel != null)
        {
            tmLabel.style.position = Position.Absolute;
            tmLabel.style.left = 4f;
            tmLabel.style.bottom = 2f;
            tmLabel.style.fontSize = labelFontSize;
            tmLabel.style.color = new Color(0.7f, 0.75f, 0.78f, 1f);
            tmLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            tmLabel.style.paddingTop = 0f;
            tmLabel.style.paddingBottom = 0f;
            tmLabel.style.paddingLeft = 0f;
            tmLabel.style.paddingRight = 0f;
            tmLabel.style.marginTop = 0f;
            tmLabel.style.marginBottom = 0f;
            tmLabel.style.marginLeft = 0f;
            tmLabel.style.marginRight = 0f;
        }

        if (headingReadout != null)
        {
            headingReadout.style.position = Position.Absolute;
            headingReadout.style.right = 4f;
            headingReadout.style.bottom = 2f;
            headingReadout.style.fontSize = readoutFontSize;
            headingReadout.style.color = readoutColor;
            headingReadout.style.unityFontStyleAndWeight = FontStyle.Bold;
            headingReadout.style.unityTextAlign = TextAnchor.UpperRight;
            headingReadout.style.paddingTop = 0f;
            headingReadout.style.paddingBottom = 0f;
            headingReadout.style.paddingLeft = 0f;
            headingReadout.style.paddingRight = 0f;
            headingReadout.style.marginTop = 0f;
            headingReadout.style.marginBottom = 0f;
            headingReadout.style.marginLeft = 0f;
            headingReadout.style.marginRight = 0f;
        }

        _tickMajorH = tickMajorH;
        _tickMinorH = tickMinorH;
        _numTop = numTop;
        _numHeight = numAreaH;
        _autoFontSize = autoFontSize;
    }

    private void UpdateData()
    {
        if (useManualInput)
        {
            headingDeg = manualHeadingDeg;
            return;
        }

        if (receiver == null)
            receiver = FlightDataStreamReceiver.Instance;

        if (receiver == null)
            return;

        if (usePrimaryTrack || string.IsNullOrEmpty(trackId))
        {
            var pos = receiver.PrimaryPosition;
            headingDeg = pos != null ? pos.heading : 0f;
            return;
        }

        List<FlightDataStreamReceiver.TrackPosition> positions = receiver.SnapshotPositions;
        FlightDataStreamReceiver.TrackPosition selected = positions.Find(p => p != null && p.trackId == trackId);
        if (selected != null)
            headingDeg = selected.heading;
    }

    private void UpdateDisplay()
    {
        if (scaleTape == null)
            return;

        // Always use displaySize.x —layout.width may be stale after size changes
        float clipWidth = displaySize.x;

        float normalizedHeading = ((headingDeg % 360f) + 360f) % 360f;
        float tapeX = clipWidth * 0.5f - (normalizedHeading + 90f) * pixelsPerDegree;
        scaleTape.style.translate = new Translate(tapeX, 0f);

        if (commandMarker != null)
        {
            float cmdDelta = Mathf.DeltaAngle(headingDeg, commandHeadingDeg);
            float cmdX = clipWidth * 0.5f + cmdDelta * pixelsPerDegree;
            commandMarker.style.left = cmdX;
            bool inView = Mathf.Abs(cmdDelta) * pixelsPerDegree < clipWidth * 0.5f;
            commandMarker.style.display = inView ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (headingReadout != null)
        {
            int headingInt = Mathf.RoundToInt(normalizedHeading) % 360;
            headingReadout.text = headingInt.ToString("D3") + "\u00b0";
        }

        if (tmLabel != null)
            tmLabel.text = useTrueHeading ? "T" : "M";
    }

    private void BuildTicks()
    {
        if (scaleTape == null)
            return;

        scaleTape.Clear();

        float h = displaySize.y;

        for (int deg = -90; deg <= 450; deg += 5)
        {
            int displayDeg = ((deg % 360) + 360) % 360;
            bool isMajor = displayDeg % 10 == 0;
            float xPos = (deg + 90) * pixelsPerDegree;

            var tick = new VisualElement();
            tick.style.position = Position.Absolute;
            tick.style.bottom = 0f;
            tick.style.left = xPos;
            if (isMajor)
            {
                tick.style.height = _tickMajorH;
                tick.style.width = tickMajorWidth;
                tick.style.backgroundColor = tickColor;
            }
            else
            {
                tick.style.height = _tickMinorH;
                tick.style.width = tickMinorWidth;
                tick.style.backgroundColor = minorTickColor;
            }
            scaleTape.Add(tick);

            if (isMajor)
            {
                string label = (displayDeg / 10).ToString("00");
                var numLabel = new Label(label);
                numLabel.style.position = Position.Absolute;
                numLabel.style.top = _numTop;
                numLabel.style.left = xPos - 15f;
                numLabel.style.width = 30f;
                numLabel.style.height = _numHeight;
                numLabel.style.fontSize = _autoFontSize;
                numLabel.style.color = numberColor;
                numLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                numLabel.style.unityTextAlign = TextAnchor.LowerCenter;
                // Clear Unity runtime theme defaults that cause clipping
                numLabel.style.paddingTop = 0f;
                numLabel.style.paddingBottom = 0f;
                numLabel.style.paddingLeft = 0f;
                numLabel.style.paddingRight = 0f;
                numLabel.style.marginTop = 0f;
                numLabel.style.marginBottom = 0f;
                numLabel.style.marginLeft = 0f;
                numLabel.style.marginRight = 0f;
                scaleTape.Add(numLabel);
            }
        }
    }
}
