using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Unity_PitchLadder : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private string trackId;
    [SerializeField] private bool usePrimaryTrack = true;
    [SerializeField] private bool useManualInput = false;
    [SerializeField] private float manualPitchDeg;
    [SerializeField] private float manualRollDeg;

    [Header("Layout")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;
    [SerializeField] private Vector2 displaySize = new Vector2(240f, 190f);
    [SerializeField] private Vector2 screenPosition = new Vector2(20f, 130f);
    [SerializeField] private float scaleFactor = 1f;
    [SerializeField] private float pixelsPerDegree = 8f;
    [SerializeField] private float minPitchDeg = -85f;
    [SerializeField] private float maxPitchDeg = 85f;
    [SerializeField] private float majorStepDeg = 5f;
    [SerializeField] private float lineGap = 116f;
    [SerializeField] private float horizonLength = 168f;
    [SerializeField] private float attitudeLineLength = 35f;
    [SerializeField] private float centerGap = 46f;
    [SerializeField] private float lineThickness = 2f;
    [SerializeField] private float negativeDashWidth = 12f;
    [SerializeField] private float negativeDashGap = 8f;
    [SerializeField] private float numberOffset = 24f;
    [SerializeField] private int fontSize = 18;

    [Header("Center Marker")]
    [SerializeField] private float centerWingLeftCenter = -34f;
    [SerializeField] private float centerWingRightCenter = 8f;
    [SerializeField] private float centerWingLength = 26f;
    [SerializeField] private float centerStemOffset = -10f;
    [SerializeField] private float centerStemLength = 20f;
    [SerializeField] private float centerArcRadius = 13f;
    [SerializeField] private int centerArcSegments = 8;
    [SerializeField] private Vector2 centerOffset = Vector2.zero;
    [SerializeField] private float centerArcOffsetY = 0f;

    [Header("Style")]
    [SerializeField] private Color lineColor = new Color(0.88f, 0.94f, 1f, 0.95f);
    [SerializeField] private Color centerColor = new Color(0.4f, 0.9f, 1f, 1f);

    private VisualElement root;
    private VisualElement pitchLadder;
    private VisualElement clipWindow;
    private VisualElement marksRoot;
    private VisualElement centerMarker;

    private float pitchDeg;
    private float rollDeg;

    private float S(float v) => v * scaleFactor;

    // Always compute from formula —never read resolvedStyle (may be NaN before layout)
    private float MarksHeight => (maxPitchDeg - minPitchDeg) * S(pixelsPerDegree) + displaySize.y * 2f;

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
            Debug.LogError("[Unity_PitchLadder] No UIDocument found.");
            return;
        }

        root = uiDocument.rootVisualElement;
        if (root != null)
        {
            ApplyStyleSheet();
            QueryElements();
            ApplyLayout();
            BuildMarks();
            BuildCenterMarker();
            UpdateDisplay();
        }
    }

    private void OnValidate()
    {
        pixelsPerDegree = Mathf.Max(1f, pixelsPerDegree);
        majorStepDeg = Mathf.Max(1f, majorStepDeg);
        negativeDashWidth = Mathf.Max(2f, negativeDashWidth);
        negativeDashGap = Mathf.Max(0f, negativeDashGap);
        scaleFactor = Mathf.Max(0.1f, scaleFactor);

#if UNITY_EDITOR
        if (!Application.isPlaying && uiDocument != null && root == null)
            root = uiDocument.rootVisualElement;

        if (root != null)
        {
            QueryElements();
            ApplyLayout();
            BuildMarks();
            BuildCenterMarker();
            UpdateDisplay();
        }
#endif
    }

    private void Update()
    {
        if (root == null)
            EnsureRoot();

        if (root == null || marksRoot == null)
            return;

        UpdateData();
        UpdateDisplay();
    }

    public void SetAttitude(float pitchDegrees, float rollDegrees)
    {
        pitchDeg = pitchDegrees;
        rollDeg = rollDegrees;
        if (root != null && marksRoot != null)
            UpdateDisplay();
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
                "Assets/UI Toolkit/PitchLadder.uss");
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
        pitchLadder = root.Q<VisualElement>("PitchLadder");
        clipWindow = root.Q<VisualElement>("ClipWindow");
        marksRoot = root.Q<VisualElement>("Marks");
        centerMarker = root.Q<VisualElement>("CenterMarker");
    }

    private void ApplyLayout()
    {
        if (pitchLadder == null)
            return;

        float w = displaySize.x;
        float h = displaySize.y;

        pitchLadder.style.position = Position.Absolute;
        pitchLadder.style.left = screenPosition.x;
        pitchLadder.style.top = screenPosition.y;
        pitchLadder.style.width = w;
        pitchLadder.style.height = h;
        pitchLadder.style.overflow = Overflow.Hidden;

        if (clipWindow != null)
        {
            clipWindow.style.position = Position.Absolute;
            clipWindow.style.left = 0f;
            clipWindow.style.top = 0f;
            clipWindow.style.width = w;
            clipWindow.style.height = h;
            clipWindow.style.overflow = Overflow.Hidden;
        }

        if (marksRoot != null)
        {
            marksRoot.style.position = Position.Absolute;
            marksRoot.style.left = 0f;
            marksRoot.style.top = 0f;
            marksRoot.style.width = w;
            marksRoot.style.height = MarksHeight;
        }

        if (centerMarker != null)
        {
            centerMarker.style.position = Position.Absolute;
            centerMarker.style.left = 0f;
            centerMarker.style.top = 0f;
            centerMarker.style.width = w;
            centerMarker.style.height = h;
        }
    }

    private void UpdateData()
    {
        if (useManualInput)
        {
            pitchDeg = manualPitchDeg;
            rollDeg = manualRollDeg;
            return;
        }

        if (receiver == null)
            receiver = FlightDataStreamReceiver.Instance;

        if (receiver == null)
            return;

        if (usePrimaryTrack || string.IsNullOrEmpty(trackId))
        {
            pitchDeg = receiver.PrimaryPitchDeg;
            rollDeg = receiver.PrimaryRollDeg;
            return;
        }

        List<FlightDataStreamReceiver.TrackPosition> positions = receiver.SnapshotPositions;
        FlightDataStreamReceiver.TrackPosition selected = positions.Find(p => p != null && p.trackId == trackId);
        if (selected != null)
        {
            pitchDeg = selected.pitch;
            rollDeg = selected.roll;
        }
    }

    private void UpdateDisplay()
    {
        if (marksRoot == null)
            return;

        float clampedPitch = Mathf.Clamp(pitchDeg, minPitchDeg, maxPitchDeg);
        float h = displaySize.y;
        float marksH = MarksHeight;

        // marksRoot center is at marksH/2 (pitch=0).
        // Positive pitch →marksRoot moves down →horizon drops below center (correct)
        float posY = h * 0.5f - marksH * 0.5f + clampedPitch * S(pixelsPerDegree);
        marksRoot.style.translate = new Translate(0f, posY);

        // Rotation pivot = center marker position in marksRoot's local space.
        // Center marker is at display center + centerOffset; marksRoot is offset by posY.
        float pivotX = displaySize.x * 0.5f + S(centerOffset.x);
        float pivotY = marksH * 0.5f - clampedPitch * S(pixelsPerDegree) + S(centerOffset.y);
        marksRoot.style.transformOrigin = new TransformOrigin(
            new Length(pivotX, LengthUnit.Pixel),
            new Length(pivotY, LengthUnit.Pixel)
        );
        marksRoot.style.rotate = new Rotate(-rollDeg);

        // Per spec: dive/climb marker does NOT rotate
    }

    // ── Mark building ──

    private void BuildMarks()
    {
        if (marksRoot == null)
            return;

        marksRoot.Clear();

        float cx = displaySize.x * 0.5f;
        float marksH = MarksHeight;
        float cy = marksH * 0.5f; // pitch=0 position in marksRoot

        // Horizon line at pitch=0 (thick solid, with center gap)
        float halfH = S(horizonLength) * 0.5f;
        float halfGap = S(centerGap) * 0.5f;
        CreateLine(marksRoot, "HorizonL", cx - halfH, cy, halfH - halfGap, S(lineThickness) + 1f, "pl-line-horizon", lineColor);
        CreateLine(marksRoot, "HorizonR", cx + halfGap, cy, halfH - halfGap, S(lineThickness) + 1f, "pl-line-horizon", lineColor);
        CreateLabel(marksRoot, "HorizonLabel", "地平线", cx + halfH + S(6f), cy - S(fontSize) * 0.5f, "pl-horizon-label", lineColor, Mathf.RoundToInt(S(fontSize - 4)));

        for (float pitch = majorStepDeg; pitch <= maxPitchDeg + 0.01f; pitch += majorStepDeg)
            CreatePitchMark(pitch, cx, cy);

        for (float pitch = -majorStepDeg; pitch >= minPitchDeg - 0.01f; pitch -= majorStepDeg)
            CreatePitchMark(pitch, cx, cy);
    }

    private void CreatePitchMark(float pitch, float cx, float cy)
    {
        float y = cy - pitch * S(pixelsPerDegree);
        bool negative = pitch < 0f;
        float halfGap = S(centerGap) * 0.5f;
        float outerHalf = S(lineGap) * 0.5f;
        float lineLength = Mathf.Max(4f, Mathf.Min(S(attitudeLineLength), outerHalf - halfGap));
        string label = Mathf.Abs(pitch).ToString("0");
        if (negative)
            label = "-" + label;

        float leftStart = cx - outerHalf;
        float leftEnd = cx - halfGap;
        float rightStart = cx + halfGap;
        float rightEnd = cx + outerHalf;

        if (negative)
        {
            CreateDashedLine(marksRoot, leftStart, leftEnd, y);
            CreateDashedLine(marksRoot, rightStart, rightEnd, y);
        }
        else
        {
            float leftCenter = (leftStart + leftEnd) * 0.5f;
            float rightCenter = (rightStart + rightEnd) * 0.5f;
            CreateLine(marksRoot, "LeftPos", leftCenter - lineLength * 0.5f, y - S(lineThickness) * 0.5f, lineLength, S(lineThickness), "pl-line", lineColor);
            CreateLine(marksRoot, "RightPos", rightCenter - lineLength * 0.5f, y - S(lineThickness) * 0.5f, lineLength, S(lineThickness), "pl-line", lineColor);
        }

        // Tails —always point toward horizon (positive pitch →tail down, negative →tail up)
        float tailDir = pitch > 0f ? 1f : -1f;
        float tailLen = S(12f);
        CreateLine(marksRoot, "LeftTail", leftStart - S(lineThickness) * 0.5f, y + tailDir * tailLen * 0.5f - tailLen * 0.5f, S(lineThickness), tailLen, "pl-tail", lineColor);
        CreateLine(marksRoot, "RightTail", rightEnd - S(lineThickness) * 0.5f, y + tailDir * tailLen * 0.5f - tailLen * 0.5f, S(lineThickness), tailLen, "pl-tail", lineColor);

        // Numbers —on outside of tail lines
        float numW = S(52f);
        float numH = Mathf.Max(20f, S(fontSize) + 4f);
        CreateLabel(marksRoot, "LeftNum", label, leftStart - S(numberOffset) - numW * 0.5f, y - numH * 0.5f, "pl-number", lineColor, Mathf.RoundToInt(S(fontSize)), TextAnchor.MiddleRight, numW, numH);
        CreateLabel(marksRoot, "RightNum", label, rightEnd + S(numberOffset) - numW * 0.5f, y - numH * 0.5f, "pl-number", lineColor, Mathf.RoundToInt(S(fontSize)), TextAnchor.MiddleLeft, numW, numH);
    }

    private void BuildCenterMarker()
    {
        if (centerMarker == null)
            return;

        centerMarker.Clear();

        float cx = displaySize.x * 0.5f + S(centerOffset.x);
        float cy = displaySize.y * 0.5f + S(centerOffset.y);
        float t = S(lineThickness) + 1f;

        // Left wing
        CreateLine(centerMarker, "CenterLeft", cx + S(centerWingLeftCenter) - S(centerWingLength) * 0.5f, cy - t * 0.5f, S(centerWingLength), t, "pl-line-center", centerColor);
        // Right wing
        CreateLine(centerMarker, "CenterRight", cx + S(centerWingRightCenter) - S(centerWingLength) * 0.5f, cy - t * 0.5f, S(centerWingLength), t, "pl-line-center", centerColor);
        // Stem (UITK: negative Y = up)
        CreateLine(centerMarker, "CenterStem", cx - t * 0.5f, cy + S(centerStemOffset) - S(centerStemLength), t, S(centerStemLength), "pl-line-center", centerColor);

        // Arc: dome above center (UITK: negative Y = up), with independent Y offset
        float arcBaseY = cy + S(centerArcOffsetY);
        float arcR = S(centerArcRadius);
        Vector2 prev = new Vector2(cx - arcR, arcBaseY);
        for (int i = 1; i <= centerArcSegments; i++)
        {
            float angle = Mathf.PI - Mathf.PI * i / centerArcSegments;
            Vector2 cur = new Vector2(cx + Mathf.Cos(angle) * arcR, arcBaseY - Mathf.Sin(angle) * arcR);
            float midX = (prev.x + cur.x) * 0.5f;
            float midY = (prev.y + cur.y) * 0.5f;
            float len = Vector2.Distance(prev, cur);
            float rot = Mathf.Atan2(cur.y - prev.y, cur.x - prev.x) * Mathf.Rad2Deg;
            CreateLineRotated(centerMarker, midX, midY, len, t, "pl-arc", centerColor, rot);
            prev = cur;
        }
    }

    // ── Primitive helpers ──

    private void CreateLine(VisualElement parent, string name, float x, float y, float w, float h, string ussClass, Color color)
    {
        var el = new VisualElement();
        el.name = name;
        el.AddToClassList(ussClass);
        el.style.position = Position.Absolute;
        el.style.left = x;
        el.style.top = y;
        el.style.width = w;
        el.style.height = h;
        el.style.backgroundColor = color;
        parent.Add(el);
    }

    private void CreateLineRotated(VisualElement parent, float x, float y, float w, float h, string ussClass, Color color, float angleDeg)
    {
        var el = new VisualElement();
        el.name = "Arc";
        el.AddToClassList(ussClass);
        el.style.position = Position.Absolute;
        el.style.left = x - w * 0.5f;
        el.style.top = y - h * 0.5f;
        el.style.width = w;
        el.style.height = h;
        el.style.backgroundColor = color;
        el.style.rotate = new Rotate(angleDeg);
        parent.Add(el);
    }

    private void CreateDashedLine(VisualElement parent, float startX, float endX, float y)
    {
        float dashW = S(negativeDashWidth);
        float dashGap = S(negativeDashGap);
        float lt = S(lineThickness);
        float dir = Mathf.Sign(endX - startX);
        float x = startX;
        while ((dir > 0f && x < endX) || (dir < 0f && x > endX))
        {
            float next = x + dir * dashW;
            if ((dir > 0f && next > endX) || (dir < 0f && next < endX))
                next = endX;

            float len = Mathf.Abs(next - x);
            float mid = (x + next) * 0.5f;
            CreateLine(parent, "Dash", mid - len * 0.5f, y - lt * 0.5f, len, lt, "pl-dash", lineColor);
            x = next + dir * dashGap;
        }
    }

    private void CreateLabel(VisualElement parent, string name, string text, float x, float y, string ussClass, Color color, int size, TextAnchor anchor = TextAnchor.MiddleCenter, float w = 52f, float h = 24f)
    {
        var lbl = new Label(text);
        lbl.name = name;
        lbl.AddToClassList(ussClass);
        lbl.style.position = Position.Absolute;
        lbl.style.left = x;
        lbl.style.top = y;
        lbl.style.width = w;
        lbl.style.height = h;
        lbl.style.fontSize = Mathf.Max(4, size);
        lbl.style.color = color;
        lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        lbl.style.unityTextAlign = anchor;
        lbl.style.paddingTop = 0f;
        lbl.style.paddingBottom = 0f;
        lbl.style.paddingLeft = 0f;
        lbl.style.paddingRight = 0f;
        lbl.style.marginTop = 0f;
        lbl.style.marginBottom = 0f;
        lbl.style.marginLeft = 0f;
        lbl.style.marginRight = 0f;
        parent.Add(lbl);
    }
}
