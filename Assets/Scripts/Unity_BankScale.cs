using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 横滚角指示器（参考技术文档图 35 倾斜刻度 1）
/// ∪ 形圆弧，圆心在上方，刻度向内指（朝圆心），数字在刻度内侧。
/// </summary>
public class Unity_BankScale : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;

    [Header("Data Source")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private string trackId;
    [SerializeField] private bool usePrimaryTrack = true;
    [SerializeField] private bool useManualInput = false;
    [SerializeField, Range(-180, 180)] private float manualRollAngle = 0f;

    [Header("Layout")]
    [SerializeField] private Vector2 displaySize = new Vector2(300f, 110f);
    [SerializeField] private Vector2 screenPosition = new Vector2(1520f, 5f);
    [SerializeField] private float scaleFactor = 1.2f;

    [Header("Design Parameters (spec: mr, 1mr = 4px)")]
    [SerializeField] private float mrToPixels = 4f;
    [SerializeField] private float tickMinorLengthMr = 3f;
    [SerializeField] private float tickMajorLengthMr = 5f;
    [SerializeField] private float pointerSizeMr = 4f;
    [SerializeField] private float arcRadiusMr = 24f;
    [SerializeField] private float tickWidth = 2f;
    [SerializeField] private int fontSize = 11;

    [Header("Style")]
    [SerializeField] private Color tickColor = new Color(0.85f, 0.88f, 0.92f, 1f);
    [SerializeField] private Color minorTickColor = new Color(0.5f, 0.54f, 0.58f, 1f);
    [SerializeField] private Color numberColor = new Color(0.85f, 0.88f, 0.92f, 1f);
    [SerializeField] private Color pointerColor = new Color(1f, 0.78f, 0f, 1f);
    [SerializeField] private Color readoutColor = new Color(1f, 0.78f, 0f, 1f);

    private VisualElement root;
    private VisualElement bankScale;
    private VisualElement scaleArc;
    private VisualElement rollPointer;
    private Label rollReadout;
    private float currentRollAngle;

    private float _cx, _cy, _R;
    private float _tickMajorH, _tickMinorH, _pointerSide, _pointerHeight;
    private float _fs;

    private static readonly int[] MajorAngles = { -60, -45, -30, -20, -10, 0, 10, 20, 30, 45, 60 };
    private static readonly int[] MinorAngles = { -55, -50, -40, -35, -25, -15, -5, 5, 15, 25, 35, 40, 50, 55 };

    // Only label 30/45/60 — enough spacing to avoid overlap
    private static readonly int[] LabelAngles = { -60, -45, -30, 30, 45, 60 };

    private void OnEnable() { EnsureRoot(); }

    private void OnValidate()
    {
        arcRadiusMr = Mathf.Max(5f, arcRadiusMr);
        scaleFactor = Mathf.Max(0.5f, scaleFactor);
#if UNITY_EDITOR
        if (!Application.isPlaying && uiDocument != null && root == null)
            root = uiDocument.rootVisualElement;
        if (root != null) { QueryElements(); ApplyLayout(); BuildTicks(); SetupPointer(); UpdateDisplay(); }
#endif
    }

    private void Update()
    {
        if (root == null) EnsureRoot();
        if (root == null || scaleArc == null) return;
        UpdateData();
        UpdateDisplay();
    }

    public void SetRoll(float rollDegrees) { currentRollAngle = rollDegrees; if (root != null) UpdateDisplay(); }
    public void SetTrackId(string id) { trackId = id; usePrimaryTrack = string.IsNullOrEmpty(id); }
    [ContextMenu("Rebuild")] public void Rebuild() { EnsureRoot(); }

    // ── Init ────────────────────────────────────────────────────

    private void EnsureRoot()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) { Debug.LogError("[Unity_BankScale] No UIDocument."); return; }
        root = uiDocument.rootVisualElement;
        if (root == null) return;
        ApplyStyleSheet();
        QueryElements();
        ApplyLayout();
        BuildTicks();
        SetupPointer();
        UpdateDisplay();
    }

    private void ApplyStyleSheet()
    {
        if (styleSheet == null)
        {
#if UNITY_EDITOR
            styleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI Toolkit/BankScale.uss");
#endif
        }
        if (styleSheet != null && root != null && !root.styleSheets.Contains(styleSheet))
            root.styleSheets.Add(styleSheet);
    }

    private void QueryElements()
    {
        bankScale = root.Q<VisualElement>("BankScale");
        scaleArc = root.Q<VisualElement>("ScaleArc");
        rollPointer = root.Q<VisualElement>("RollPointer");
        rollReadout = root.Q<Label>("RollReadout");
    }

    // ── Layout ─────────────────────────────────────────────────
    //
    // ∪ arc: centre (_cx, _cy) is ABOVE the display.
    //   arc point: x = cx + R·sin(θ),  y = cy + R·cos(θ)
    //   θ=0  → bottom centre (lowest point)
    //   θ=±60 → upper sides
    // Ticks point INWARD (upward toward centre).
    // Labels are inside the arc (above ticks, closer to centre).
    // ───────────────────────────────────────────────────────────

    private void ApplyLayout()
    {
        if (bankScale == null) return;

        float w = displaySize.x;
        float h = displaySize.y;

        _R = arcRadiusMr * mrToPixels * scaleFactor;
        _cx = w * 0.5f;
        _tickMajorH = tickMajorLengthMr * mrToPixels * scaleFactor;
        _tickMinorH = tickMinorLengthMr * mrToPixels * scaleFactor;
        _pointerSide = pointerSizeMr * mrToPixels * scaleFactor;
        _pointerHeight = _pointerSide * Mathf.Sqrt(3f) / 2f;
        _fs = Mathf.Max(8f, fontSize * scaleFactor);

        _cy = -2f;

        bankScale.style.position = Position.Absolute;
        bankScale.style.left = screenPosition.x;
        bankScale.style.top = screenPosition.y;
        bankScale.style.width = w;
        bankScale.style.height = h;
        bankScale.style.overflow = Overflow.Hidden;

        if (scaleArc != null)
        {
            scaleArc.style.position = Position.Absolute;
            scaleArc.style.left = 0f;
            scaleArc.style.top = 0f;
            scaleArc.style.width = w;
            scaleArc.style.height = h;
            scaleArc.style.overflow = Overflow.Hidden;
        }

        if (rollReadout != null)
        {
            rollReadout.style.position = Position.Absolute;
            rollReadout.style.right = 4f;
            rollReadout.style.bottom = 2f;
            rollReadout.style.fontSize = _fs;
            rollReadout.style.color = readoutColor;
            rollReadout.style.unityFontStyleAndWeight = FontStyle.Bold;
            rollReadout.style.unityTextAlign = TextAnchor.UpperRight;
            ClearPaddingMargin(rollReadout);
        }
    }

    // ── Ticks ───────────────────────────────────────────────────

    private void BuildTicks()
    {
        if (scaleArc == null) return;
        scaleArc.Clear();

        foreach (int a in MinorAngles) CreateTick(a, false);
        foreach (int a in MajorAngles) CreateTick(a, true);
        foreach (int a in LabelAngles) CreateLabel(a);
    }

    private void CreateTick(int angle, bool isMajor)
    {
        float rad = angle * Mathf.Deg2Rad;
        float x = _cx + _R * Mathf.Sin(rad);
        float y = _cy + _R * Mathf.Cos(rad);

        float tickH = isMajor ? _tickMajorH : _tickMinorH;
        float tw = tickWidth * scaleFactor;

        var tick = new VisualElement();
        tick.style.position = Position.Absolute;
        tick.style.left = x - tw * 0.5f;
        tick.style.top = y - tickH;
        tick.style.width = tw;
        tick.style.height = tickH;
        tick.style.backgroundColor = isMajor ? tickColor : minorTickColor;

        // Rotate around bottom-centre (the arc point) so tick points toward centre.
        // UI Toolkit positive = clockwise; for θ>0 (right side) we need CCW → -angle.
        tick.style.transformOrigin = new TransformOrigin(
            new Length(50, LengthUnit.Percent),
            new Length(100, LengthUnit.Percent));
        tick.style.rotate = new Rotate(-angle);
        scaleArc.Add(tick);
    }

    private void CreateLabel(int angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        float labelOffset = _tickMajorH + 8f * scaleFactor;
        float labelR = _R - labelOffset;
        float x = _cx + labelR * Mathf.Sin(rad);
        float y = _cy + labelR * Mathf.Cos(rad);
        float lw = 24f * scaleFactor;
        float lh = 16f * scaleFactor;

        var label = new Label(Mathf.Abs(angle).ToString());
        label.style.position = Position.Absolute;
        label.style.left = x - lw * 0.5f;
        label.style.top = y - lh * 0.5f;
        label.style.width = lw;
        label.style.height = lh;
        label.style.fontSize = _fs;
        label.style.color = numberColor;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        ClearPaddingMargin(label);
        scaleArc.Add(label);
    }

    // ── Pointer ─────────────────────────────────────────────────

    private void SetupPointer()
    {
        if (rollPointer == null) return;
        float halfBase = _pointerSide * 0.5f;

        rollPointer.style.position = Position.Absolute;
        rollPointer.style.width = 0;
        rollPointer.style.height = 0;
        rollPointer.style.borderLeftWidth = halfBase;
        rollPointer.style.borderRightWidth = halfBase;
        rollPointer.style.borderTopWidth = _pointerHeight;
        rollPointer.style.borderBottomWidth = 0;
        rollPointer.style.borderLeftColor = new Color(0, 0, 0, 0);
        rollPointer.style.borderRightColor = new Color(0, 0, 0, 0);
        rollPointer.style.borderTopColor = pointerColor;
        rollPointer.style.borderBottomColor = new Color(0, 0, 0, 0);
        // Rotate around tip (bottom-centre of the triangle)
        rollPointer.style.transformOrigin = new TransformOrigin(
            new Length(50, LengthUnit.Percent),
            new Length(100, LengthUnit.Percent));
    }

    // ── Data & Display ──────────────────────────────────────────

    private void UpdateData()
    {
        if (useManualInput) { currentRollAngle = manualRollAngle; return; }
        if (receiver == null) receiver = FlightDataStreamReceiver.Instance;
        if (receiver == null) return;
        if (usePrimaryTrack || string.IsNullOrEmpty(trackId))
        { currentRollAngle = receiver.PrimaryRollDeg; return; }
        var positions = receiver.SnapshotPositions;
        var sel = positions.Find(p => p != null && p.trackId == trackId);
        if (sel != null) currentRollAngle = sel.roll;
    }

    private void UpdateDisplay()
    {
        if (rollPointer == null) return;
        float displayAngle = Mathf.Clamp(currentRollAngle, -60f, 60f);
        float rad = displayAngle * Mathf.Deg2Rad;
        float x = _cx + _R * Mathf.Sin(rad);
        float y = _cy + _R * Mathf.Cos(rad);

        rollPointer.style.left = x - _pointerSide * 0.5f;
        rollPointer.style.top = y - _pointerHeight;
        // Rotate triangle so tip always points toward arc centre (radially inward)
        rollPointer.style.rotate = new Rotate(180f - displayAngle);

        if (rollReadout != null)
        {
            int rollInt = Mathf.RoundToInt(currentRollAngle);
            string sign = rollInt >= 0 ? "+" : "";
            rollReadout.text = $"R:{sign}{rollInt}\u00b0";
        }
    }

    // ── Utility ────────────────────────────────────────────────

    private static void ClearPaddingMargin(VisualElement el)
    {
        el.style.paddingTop = 0; el.style.paddingBottom = 0;
        el.style.paddingLeft = 0; el.style.paddingRight = 0;
        el.style.marginTop = 0; el.style.marginBottom = 0;
        el.style.marginLeft = 0; el.style.marginRight = 0;
    }
}
