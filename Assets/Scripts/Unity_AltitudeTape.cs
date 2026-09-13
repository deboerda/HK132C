using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Unity_AltitudeTape : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private string trackId;
    [SerializeField] private bool usePrimaryTrack = true;
    [SerializeField] private bool useManualInput = false;
    [SerializeField] private float manualAltitude;
    [SerializeField] private float commandAltitude;

    [Header("Layout")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;
    [SerializeField] private Vector2 displaySize = new Vector2(80f, 200f);
    [SerializeField] private Vector2 screenPosition = new Vector2(540f, 130f);
    [SerializeField] private float scaleFactor = 1f;
    [SerializeField] private float pixelsPerUnit = 0.2f;
    [SerializeField] private float minorStep = 10f;
    [SerializeField] private float majorStep = 100f;
    [SerializeField] private float tickWidth = 12f;
    [SerializeField] private float tickMajorH = 10f;
    [SerializeField] private float tickMinorH = 5f;
    [SerializeField] private float tickThickness = 2f;
    [SerializeField] private int fontSize = 14;
    [SerializeField] private float pointerSize = 12f;
    [SerializeField] private float readoutHeight = 24f;

    [Header("Style")]
    [SerializeField] private Color tickColor = new Color(0.78f, 0.82f, 0.86f, 0.9f);
    [SerializeField] private Color minorTickColor = new Color(0.43f, 0.47f, 0.51f, 0.8f);
    [SerializeField] private Color numberColor = new Color(0.78f, 0.82f, 0.86f, 0.95f);
    [SerializeField] private Color pointerColor = new Color(1f, 0.78f, 0f, 1f);
    [SerializeField] private Color readoutColor = new Color(1f, 0.78f, 0f, 1f);
    [SerializeField] private Color idColor = new Color(0.7f, 0.75f, 0.78f, 1f);

    private VisualElement root;
    private VisualElement tapeRoot;
    private VisualElement clipWindow;
    private VisualElement scaleTape;
    private VisualElement pointer;
    private Label readoutLabel;
    private Label idLabel;

    private float currentValue;

    private float S(float v) => v * scaleFactor;

    // Tape spans -1000..+15000m, center at 7000
    private const float TapeRange = 16000f;
    private const float TapeCenter = 8000f;

    private void OnEnable() => EnsureRoot();

    private void EnsureRoot()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) { Debug.LogError("[AltitudeTape] No UIDocument."); return; }
        root = uiDocument.rootVisualElement;
        if (root != null) { ApplyStyleSheet(); QueryElements(); ApplyLayout(); BuildTicks(); UpdateDisplay(); }
    }

    private void OnValidate()
    {
        pixelsPerUnit = Mathf.Max(0.001f, pixelsPerUnit);
        scaleFactor = Mathf.Max(0.1f, scaleFactor);
#if UNITY_EDITOR
        if (!Application.isPlaying && uiDocument != null && root == null) root = uiDocument.rootVisualElement;
        if (root != null) { QueryElements(); ApplyLayout(); BuildTicks(); UpdateDisplay(); }
#endif
    }

    private void Update()
    {
        if (root == null) EnsureRoot();
        if (root == null || scaleTape == null) return;
        UpdateData();
        UpdateDisplay();
    }

    public void SetAltitude(float alt) { currentValue = alt; if (scaleTape != null) UpdateDisplay(); }
    public void SetCommandAltitude(float a) { commandAltitude = a; }

    public void SetTrackId(string id) { trackId = id; usePrimaryTrack = string.IsNullOrEmpty(id); }

    [ContextMenu("Rebuild")]
    public void Rebuild() => EnsureRoot();

    private void ApplyStyleSheet()
    {
        if (styleSheet == null)
        {
#if UNITY_EDITOR
            styleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI Toolkit/VerticalTape.uss");
#endif
        }
        if (styleSheet != null && root != null && !root.styleSheets.Contains(styleSheet)) root.styleSheets.Add(styleSheet);
    }

    private void QueryElements()
    {
        tapeRoot = root.Q<VisualElement>("TapeRoot");
        clipWindow = root.Q<VisualElement>("ClipWindow");
        scaleTape = root.Q<VisualElement>("ScaleTape");
        pointer = root.Q<VisualElement>("Pointer");
        readoutLabel = root.Q<Label>("Readout");
        idLabel = root.Q<Label>("Identifier");
    }

    private void ApplyLayout()
    {
        if (tapeRoot == null) return;
        float w = displaySize.x, h = displaySize.y;
        tapeRoot.style.position = Position.Absolute;
        tapeRoot.style.left = screenPosition.x;
        tapeRoot.style.top = screenPosition.y;
        tapeRoot.style.width = w; tapeRoot.style.height = h;
        tapeRoot.style.overflow = Overflow.Hidden;

        if (clipWindow != null)
        {
            clipWindow.style.position = Position.Absolute;
            clipWindow.style.left = 0; clipWindow.style.top = 0;
            clipWindow.style.width = w; clipWindow.style.height = h;
            clipWindow.style.overflow = Overflow.Hidden;
        }
        if (scaleTape != null)
        {
            float tapeH = TapeRange * S(pixelsPerUnit);
            scaleTape.style.position = Position.Absolute;
            scaleTape.style.left = 0; scaleTape.style.top = 0;
            scaleTape.style.width = w;
            scaleTape.style.height = tapeH;
        }
        if (pointer != null)
        {
            // Triangle pointing right (toward scale on right side)
            pointer.style.position = Position.Absolute;
            pointer.style.right = 0;
            pointer.style.top = h * 0.5f - S(pointerSize) * 0.5f;
            pointer.style.width = S(pointerSize);
            pointer.style.height = S(pointerSize);
            pointer.style.borderBottomWidth = S(pointerSize) * 0.5f;
            pointer.style.borderTopWidth = S(pointerSize) * 0.5f;
            pointer.style.borderLeftWidth = S(pointerSize);
            pointer.style.borderBottomColor = new Color(0, 0, 0, 0);
            pointer.style.borderTopColor = new Color(0, 0, 0, 0);
            pointer.style.borderLeftColor = pointerColor;
            pointer.style.backgroundColor = new Color(0, 0, 0, 0);
        }
        if (readoutLabel != null)
        {
            readoutLabel.style.position = Position.Absolute;
            readoutLabel.style.right = S(pointerSize) + 2f;
            readoutLabel.style.top = h * 0.5f - S(readoutHeight) * 0.5f;
            readoutLabel.style.width = w - S(pointerSize) - 4f;
            readoutLabel.style.height = S(readoutHeight);
            readoutLabel.style.fontSize = Mathf.RoundToInt(S(fontSize));
            readoutLabel.style.color = readoutColor;
            readoutLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            readoutLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            readoutLabel.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            ClearPaddingMargin(readoutLabel);
        }
        if (idLabel != null)
        {
            idLabel.style.position = Position.Absolute;
            idLabel.style.left = 0; idLabel.style.bottom = 0;
            idLabel.style.width = w; idLabel.style.height = 16f;
            idLabel.style.fontSize = Mathf.RoundToInt(S(fontSize - 3));
            idLabel.style.color = idColor;
            idLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            idLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            ClearPaddingMargin(idLabel);
        }
    }

    private void UpdateData()
    {
        if (useManualInput) { currentValue = manualAltitude; return; }
        if (receiver == null) receiver = FlightDataStreamReceiver.Instance;
        if (receiver == null) return;
        if (usePrimaryTrack || string.IsNullOrEmpty(trackId))
        {
            currentValue = receiver.PrimaryAltitudeMeters;
            return;
        }
        var positions = receiver.SnapshotPositions;
        var sel = positions.Find(p => p != null && p.trackId == trackId);
        if (sel != null) currentValue = sel.alt;
    }

    private void UpdateDisplay()
    {
        if (scaleTape == null) return;
        float h = displaySize.y;
        float tapeH = TapeRange * S(pixelsPerUnit);
        // Tape center (TapeCenter) should be at display center when alt=TapeCenter
        // When alt increases, tape moves down (positive Y in UITK)
        float posY = h * 0.5f - tapeH * 0.5f + (currentValue - TapeCenter) * S(pixelsPerUnit);
        scaleTape.style.translate = new Translate(0f, posY);

        if (readoutLabel != null)
            readoutLabel.text = Mathf.RoundToInt(currentValue).ToString("D3");

        if (idLabel != null)
            idLabel.text = "B";
    }

    private void BuildTicks()
    {
        if (scaleTape == null) return;
        scaleTape.Clear();

        float w = displaySize.x;
        float tapeH = TapeRange * S(pixelsPerUnit);
        float tapeCenterY = tapeH * 0.5f; // center of tape = TapeCenter altitude
        float tickX = 0f; // ticks on left side

        for (float val = 0; val <= 15000; val += minorStep)
        {
            bool isMajor = Mathf.RoundToInt(val / majorStep) * majorStep == Mathf.RoundToInt(val);
            float y = tapeCenterY - (val - TapeCenter) * S(pixelsPerUnit);
            float th = isMajor ? S(tickMajorH) : S(tickMinorH);
            var tick = new VisualElement();
            tick.style.position = Position.Absolute;
            tick.style.left = tickX; tick.style.top = y - th * 0.5f;
            tick.style.width = S(tickThickness); tick.style.height = th;
            tick.style.backgroundColor = isMajor ? tickColor : minorTickColor;
            scaleTape.Add(tick);

            if (isMajor)
            {
                var lbl = new Label(val.ToString("F0"));
                lbl.style.position = Position.Absolute;
                lbl.style.left = tickX + S(tickWidth) + 2f;
                lbl.style.top = y - S(fontSize) * 0.5f;
                lbl.style.width = 40f; lbl.style.height = S(fontSize) + 4f;
                lbl.style.fontSize = Mathf.RoundToInt(S(fontSize));
                lbl.style.color = numberColor;
                lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
                ClearPaddingMargin(lbl);
                scaleTape.Add(lbl);
            }
        }
    }

    private void ClearPaddingMargin(VisualElement el)
    {
        el.style.paddingTop = 0; el.style.paddingBottom = 0;
        el.style.paddingLeft = 0; el.style.paddingRight = 0;
        el.style.marginTop = 0; el.style.marginBottom = 0;
        el.style.marginLeft = 0; el.style.marginRight = 0;
    }
}
