using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Unity_HUD : MonoBehaviour
{
    [Header("HUD Motion")]
    public float pitchSensitivity = 5.0f;
    public float rollSensitivity = 1.0f;

    [Header("Flight Data")]
    [SerializeField] private bool useLocalInputForTesting = false;
    [SerializeField] private float pitchDegrees;
    [SerializeField] private float rollDegrees;

    [Header("Data Source")]
    [SerializeField] private bool useManualInput = false;
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private string trackId;
    [SerializeField] private bool usePrimaryTrack = true;

    private RectTransform mainScale;
    private RectTransform leftScale;
    private RectTransform rightScale;
    private RectTransform centerBar;

    private readonly List<GameObject> uiElements = new List<GameObject>();

    private void Start()
    {
        Transform t = transform;
        mainScale = t.Find("MainScale")?.GetComponent<RectTransform>();
        leftScale = t.Find("LeftScale")?.GetComponent<RectTransform>();
        rightScale = t.Find("RightScale")?.GetComponent<RectTransform>();
        centerBar = t.Find("CenterBar")?.GetComponent<RectTransform>();

        CreateHUDElements();
    }

    private void OnDestroy()
    {
        foreach (GameObject element in uiElements)
        {
            if (element != null)
            {
                Destroy(element);
            }
        }

        uiElements.Clear();
    }

    public void SetAttitude(float pitch, float roll)
    {
        pitchDegrees = pitch;
        rollDegrees = roll;
        UpdateAttitudeDisplay();
    }

    public void SetTrackId(string id)
    {
        trackId = id;
        usePrimaryTrack = string.IsNullOrEmpty(id);
    }

    private void Update()
    {
        if (!useManualInput && !useLocalInputForTesting)
            UpdateData();

        if (useLocalInputForTesting || useManualInput || receiver != null || FlightDataStreamReceiver.Instance != null)
            UpdateAttitudeDisplay();
    }

    private void UpdateData()
    {
        if (receiver == null)
            receiver = FlightDataStreamReceiver.Instance;

        if (receiver == null)
            return;

        if (usePrimaryTrack || string.IsNullOrEmpty(trackId))
        {
            pitchDegrees = receiver.PrimaryPitchDeg;
            rollDegrees = receiver.PrimaryRollDeg;
            return;
        }

        var positions = receiver.SnapshotPositions;
        var sel = positions.Find(p => p != null && p.trackId == trackId);
        if (sel != null)
        {
            pitchDegrees = sel.pitch;
            rollDegrees = sel.roll;
        }
    }

    private void UpdateAttitudeDisplay()
    {
        if (mainScale == null || leftScale == null || rightScale == null || centerBar == null)
        {
            return;
        }

        float pitchOffset = pitchDegrees * pitchSensitivity;
        mainScale.localPosition = new Vector3(mainScale.localPosition.x, pitchOffset, mainScale.localPosition.z);
        leftScale.localPosition = new Vector3(leftScale.localPosition.x, mainScale.localPosition.y, leftScale.localPosition.z);
        rightScale.localPosition = new Vector3(rightScale.localPosition.x, mainScale.localPosition.y, rightScale.localPosition.z);
        centerBar.localEulerAngles = new Vector3(0, 0, -rollDegrees * rollSensitivity);
    }

    private void CreateHUDElements()
    {
        CreateVerticalScale(mainScale, Color.cyan, "主刻度");
        CreateVerticalScale(leftScale, new Color(1, 0.69f, 0.125f), "左侧刻度");
        CreateVerticalScale(rightScale, Color.cyan, "右侧刻度");
        CreateHorizontalBar(centerBar);
        CreateCenterAnchor();
    }

    private void CreateVerticalScale(RectTransform parent, Color color, string name)
    {
        if (parent == null) return;

        GameObject scaleContainer = new GameObject(name);
        scaleContainer.transform.SetParent(parent, false);
        uiElements.Add(scaleContainer);

        for (int i = 0; i <= 10; i++)
        {
            float y = -300 + (i * 60);
            CreateLine(scaleContainer.transform, new Vector2(0, y), new Vector2(0, y + 10), color, 1.5f);
        }

        for (int i = 0; i <= 20; i++)
        {
            if (i % 2 != 0)
            {
                float y = -300 + (i * 30);
                CreateLine(scaleContainer.transform, new Vector2(0, y), new Vector2(0, y + 5), color, 1.0f);
            }
        }
    }

    private void CreateHorizontalBar(RectTransform parent)
    {
        if (parent == null) return;

        CreateLine(parent.transform, new Vector2(-200, 0), new Vector2(200, 0), Color.cyan, 3.0f);
        CreateCircle(parent.transform, new Vector2(-120, 0), 6, Color.cyan, 2.0f);
        CreateCircle(parent.transform, new Vector2(120, 0), 6, Color.cyan, 2.0f);
    }

    private void CreateCenterAnchor()
    {
        GameObject anchorContainer = new GameObject("CenterAnchor");
        anchorContainer.transform.SetParent(transform, false);
        uiElements.Add(anchorContainer);

        CreateCircle(anchorContainer.transform, Vector2.zero, 12, Color.cyan, 2.0f);
        CreateCircle(anchorContainer.transform, Vector2.zero, 4, Color.cyan, 0.0f, true);
    }

    private void CreateLine(Transform parent, Vector2 start, Vector2 end, Color color, float width)
    {
        GameObject line = new GameObject("Line");
        line.transform.SetParent(parent, false);
        uiElements.Add(line);

        RectTransform rectTransform = line.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(400, 600);
        rectTransform.anchoredPosition = Vector2.zero;

        Image image = line.AddComponent<Image>();
        image.color = color;

        rectTransform.anchoredPosition = (start + end) / 2;
        rectTransform.sizeDelta = new Vector2(Vector2.Distance(start, end), width);
        rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg);
    }

    private void CreateCircle(Transform parent, Vector2 position, float radius, Color color, float borderWidth, bool fill = false)
    {
        GameObject circle = new GameObject("Circle");
        circle.transform.SetParent(parent, false);
        uiElements.Add(circle);

        RectTransform rectTransform = circle.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(radius * 2, radius * 2);
        rectTransform.anchoredPosition = position;

        Image image = circle.AddComponent<Image>();
        image.color = color;

        Texture2D texture = new Texture2D(128, 128);
        for (int x = 0; x < 128; x++)
        {
            for (int y = 0; y < 128; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(64, 64));
                if (fill)
                {
                    texture.SetPixel(x, y, distance <= 64 ? color : Color.clear);
                }
                else
                {
                    texture.SetPixel(x, y, distance >= 64 - borderWidth && distance <= 64 ? color : Color.clear);
                }
            }
        }
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
    }
}