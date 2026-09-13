using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PitchLadderInstrument : MonoBehaviour
{
    private const string StructureVersion = "PitchLadder_v2";

    [Header("Data")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private string trackId;
    [SerializeField] private bool usePrimaryTrack = true;
    [SerializeField] private bool useManualInput = false;
    [SerializeField] private float manualPitchDeg;
    [SerializeField] private float manualRollDeg;

    [Header("Layout")]
    [SerializeField] private RectTransform ladderRoot;
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
    [SerializeField] private Vector2 displaySize = new Vector2(240f, 190f);

    [Header("Style")]
    [SerializeField] private Color lineColor = new Color(0.88f, 0.94f, 1f, 0.95f);
    [SerializeField] private Color centerColor = new Color(0.4f, 0.9f, 1f, 1f);
    [SerializeField] private int fontSize = 18;
    [SerializeField] private Font font;

    private readonly List<GameObject> generatedObjects = new List<GameObject>();
    private RectTransform clipRoot;
    private RectTransform markRoot;
    private float pitchDeg;
    private float rollDeg;

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnValidate()
    {
        majorStepDeg = Mathf.Max(1f, majorStepDeg);
        pixelsPerDegree = Mathf.Max(1f, pixelsPerDegree);
        negativeDashWidth = Mathf.Max(2f, negativeDashWidth);
        negativeDashGap = Mathf.Max(0f, negativeDashGap);
    }

    private void Update()
    {
        UpdateData();
        UpdateDisplay();
    }

    public void SetAttitude(float pitchDegrees, float rollDegrees)
    {
        pitchDeg = pitchDegrees;
        rollDeg = rollDegrees;
        UpdateDisplay();
    }

    public void SetTrackId(string selectedTrackId)
    {
        trackId = selectedTrackId;
        usePrimaryTrack = string.IsNullOrEmpty(trackId);
    }

    [ContextMenu("Rebuild Pitch Ladder")]
    public void Rebuild()
    {
        ClearGeneratedObjects();
        EnsureBuilt();
        UpdateDisplay();
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
        {
            receiver = FlightDataStreamReceiver.Instance;
        }

        if (receiver == null)
        {
            return;
        }

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
        if (markRoot == null)
        {
            EnsureBuilt();
        }

        if (markRoot == null)
        {
            return;
        }

        float clampedPitch = Mathf.Clamp(pitchDeg, minPitchDeg, maxPitchDeg);
        markRoot.anchoredPosition = new Vector2(0f, -clampedPitch * pixelsPerDegree);
        markRoot.localEulerAngles = new Vector3(0f, 0f, -rollDeg);
    }

    private void EnsureBuilt()
    {
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        RectTransform root = GetComponent<RectTransform>();
        if (root != null && root.rect.size == Vector2.zero)
        {
            root.sizeDelta = displaySize;
        }

        if (ladderRoot == null)
        {
            Transform existing = transform.Find("PitchLadderRoot");
            if (existing != null)
            {
                ladderRoot = existing as RectTransform;
            }
            else
            {
                ladderRoot = CreateRect("PitchLadderRoot", transform);
                generatedObjects.Add(ladderRoot.gameObject);
            }
        }

        StretchFull(ladderRoot);

        clipRoot = FindOrCreateRect(ladderRoot, "ClipRoot");
        StretchFull(clipRoot);
        SetMask(clipRoot);

        markRoot = FindMarksRoot(clipRoot);
        if (markRoot.childCount > 0 && markRoot.gameObject.name != StructureVersion)
        {
            ClearChildren(markRoot);
        }
        markRoot.gameObject.name = StructureVersion;
        markRoot.anchorMin = new Vector2(0.5f, 0.5f);
        markRoot.anchorMax = new Vector2(0.5f, 0.5f);
        markRoot.pivot = new Vector2(0.5f, 0.5f);
        markRoot.sizeDelta = new Vector2(displaySize.x * 2.4f, (maxPitchDeg - minPitchDeg) * pixelsPerDegree + displaySize.y * 2f);

        if (markRoot.childCount == 0)
        {
            BuildMarks(markRoot);
        }

        RectTransform center = FindOrCreateRect(ladderRoot, "CenterClimbMarker");
        center.anchorMin = new Vector2(0.5f, 0.5f);
        center.anchorMax = new Vector2(0.5f, 0.5f);
        center.pivot = new Vector2(0.5f, 0.5f);
        center.anchoredPosition = Vector2.zero;
        center.sizeDelta = new Vector2(70f, 30f);
        if (center.childCount == 0)
        {
            BuildCenterMarker(center);
        }
    }

    private void BuildMarks(RectTransform parent)
    {
        CreateHorizon(parent, 0f);

        for (float pitch = majorStepDeg; pitch <= maxPitchDeg + 0.01f; pitch += majorStepDeg)
        {
            CreatePitchMark(parent, pitch);
        }

        for (float pitch = -majorStepDeg; pitch >= minPitchDeg - 0.01f; pitch -= majorStepDeg)
        {
            CreatePitchMark(parent, pitch);
        }
    }

    private void CreateHorizon(RectTransform parent, float pitch)
    {
        float y = pitch * pixelsPerDegree;
        CreateSolidLine(parent, "Horizon", new Vector2(-horizonLength * 0.5f, y), horizonLength, lineThickness + 1f);
        CreateText(parent, "HorizonLabel", "地平线", new Vector2(horizonLength * 0.5f + 34f, y + 14f), TextAnchor.MiddleLeft, fontSize - 4);
    }

    private void CreatePitchMark(RectTransform parent, float pitch)
    {
        float y = pitch * pixelsPerDegree;
        bool negative = pitch < 0f;
        float halfGap = centerGap * 0.5f;
        float outerHalf = lineGap * 0.5f;
        float lineLength = Mathf.Max(4f, Mathf.Min(attitudeLineLength, outerHalf - halfGap));
        string label = Mathf.Abs(pitch).ToString("0");
        if (negative)
        {
            label = "-" + label;
        }

        if (negative)
        {
            CreateDashedLine(parent, "LeftNegLine", -outerHalf, -outerHalf + lineLength, y);
            CreateDashedLine(parent, "RightNegLine", outerHalf - lineLength, outerHalf, y);
        }
        else
        {
            CreateSolidLine(parent, "LeftPosLine", new Vector2(-outerHalf + lineLength * 0.5f, y), lineLength, lineThickness);
            CreateSolidLine(parent, "RightPosLine", new Vector2(outerHalf - lineLength * 0.5f, y), lineLength, lineThickness);
        }

        CreateTail(parent, "LeftTail", new Vector2(-outerHalf, y), pitch);
        CreateTail(parent, "RightTail", new Vector2(outerHalf, y), pitch);
        CreateText(parent, "LeftNumber", label, new Vector2(-outerHalf - numberOffset, y), TextAnchor.MiddleRight, fontSize);
        CreateText(parent, "RightNumber", label, new Vector2(outerHalf + numberOffset, y), TextAnchor.MiddleLeft, fontSize);
    }

    private void BuildCenterMarker(RectTransform parent)
    {
        CreateSolidLine(parent, "CenterLeft", new Vector2(-34f, 0f), 26f, lineThickness + 1f, centerColor);
        CreateSolidLine(parent, "CenterRight", new Vector2(8f, 0f), 26f, lineThickness + 1f, centerColor);
        CreateSolidLine(parent, "CenterStem", new Vector2(0f, -10f), 20f, lineThickness + 1f, centerColor, 90f);
        CreateArcApproximation(parent, centerColor);
    }

    private void CreateArcApproximation(RectTransform parent, Color color)
    {
        const int segments = 8;
        float radius = 13f;
        Vector2 previous = new Vector2(-radius, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float t0 = Mathf.PI - (Mathf.PI * i / segments);
            Vector2 current = new Vector2(Mathf.Cos(t0) * radius, Mathf.Sin(t0) * radius);
            Vector2 mid = (previous + current) * 0.5f;
            float length = Vector2.Distance(previous, current);
            float angle = Mathf.Atan2(current.y - previous.y, current.x - previous.x) * Mathf.Rad2Deg;
            CreateSolidLine(parent, "CenterArc", mid, length, lineThickness + 1f, color, angle);
            previous = current;
        }
    }

    private void CreateTail(RectTransform parent, string name, Vector2 lineEnd, float pitch)
    {
        float directionToHorizon = pitch > 0f ? -1f : 1f;
        float tailLength = 12f;
        Vector2 center = lineEnd + new Vector2(0f, directionToHorizon * tailLength * 0.5f);
        CreateSolidLine(parent, name, center, tailLength, lineThickness, lineColor, 90f);
    }

    private void CreateDashedLine(RectTransform parent, string name, float startX, float endX, float y)
    {
        float direction = Mathf.Sign(endX - startX);
        float x = startX;
        while ((direction > 0f && x < endX) || (direction < 0f && x > endX))
        {
            float next = x + direction * negativeDashWidth;
            if ((direction > 0f && next > endX) || (direction < 0f && next < endX))
            {
                next = endX;
            }

            float length = Mathf.Abs(next - x);
            CreateSolidLine(parent, name, new Vector2((x + next) * 0.5f, y), length, lineThickness);
            x = next + direction * negativeDashGap;
        }
    }

    private void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private RectTransform CreateSolidLine(RectTransform parent, string name, Vector2 center, float length, float thickness)
    {
        return CreateSolidLine(parent, name, center, length, thickness, lineColor, 0f);
    }

    private RectTransform CreateSolidLine(RectTransform parent, string name, Vector2 center, float length, float thickness, Color color)
    {
        return CreateSolidLine(parent, name, center, length, thickness, color, 0f);
    }

    private RectTransform CreateSolidLine(RectTransform parent, string name, Vector2 center, float length, float thickness, Color color, float angle)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = center;
        rect.sizeDelta = new Vector2(length, thickness);
        rect.localEulerAngles = new Vector3(0f, 0f, angle);

        Image image = rect.GetComponent<Image>();
        if (image == null)
        {
            image = rect.gameObject.AddComponent<Image>();
        }
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private Text CreateText(RectTransform parent, string name, string value, Vector2 position, TextAnchor anchor, int size)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(52f, 24f);

        Text text = rect.GetComponent<Text>();
        if (text == null)
        {
            text = rect.gameObject.AddComponent<Text>();
        }
        text.font = font;
        text.text = value;
        text.fontSize = Mathf.Max(10, size);
        text.color = lineColor;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private RectTransform FindMarksRoot(RectTransform parent)
    {
        Transform versioned = parent.Find(StructureVersion);
        if (versioned != null && versioned.TryGetComponent(out RectTransform versionedRect))
        {
            return versionedRect;
        }

        Transform legacy = parent.Find("Marks");
        if (legacy != null && legacy.TryGetComponent(out RectTransform legacyRect))
        {
            return legacyRect;
        }

        RectTransform rect = CreateRect("Marks", parent);
        generatedObjects.Add(rect.gameObject);
        return rect;
    }

    private RectTransform FindOrCreateRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
        {
            return existingRect;
        }

        RectTransform rect = CreateRect(name, parent);
        generatedObjects.Add(rect.gameObject);
        return rect;
    }

    private RectTransform CreateRect(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    private void SetMask(RectTransform rect)
    {
        Image image = rect.GetComponent<Image>();
        if (image == null)
        {
            image = rect.gameObject.AddComponent<Image>();
        }
        image.color = new Color(0f, 0f, 0f, 0.01f);
        image.raycastTarget = false;

        Mask mask = rect.GetComponent<Mask>();
        if (mask == null)
        {
            mask = rect.gameObject.AddComponent<Mask>();
        }
        mask.showMaskGraphic = false;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ClearGeneratedObjects()
    {
        for (int i = generatedObjects.Count - 1; i >= 0; i--)
        {
            if (generatedObjects[i] != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedObjects[i]);
                }
                else
                {
                    DestroyImmediate(generatedObjects[i]);
                }
            }
        }
        generatedObjects.Clear();
        ladderRoot = null;
        clipRoot = null;
        markRoot = null;
    }
}
