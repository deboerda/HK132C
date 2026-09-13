using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FlightProgressController : MonoBehaviour
{
    [Header("UI组件")]
    public Slider progressSlider;
    public Text progressText;

    [Header("进度配置")]
    public float minFlightTime = 0f;
    public float maxFlightTime = 100f;

    [Header("异常标记")]
    [SerializeField] private Color markerColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float markerSize = 1f;

    [Header("Data Source")]
    [SerializeField] private bool autoFollowReceiver = true;

    private float currentFlightTime = 0f;
    private float nextRefreshTime;
    private const float refreshInterval = 0.2f;
    private FlightPlaybackPanel cachedPlaybackPanel;

    // 异常标记记录
    [System.Serializable]
    public class ViolationMarker
    {
        public float normalizedPos; // 0-1 on the slider
        public string info; // e.g. "UAV-001 alt超限"
        public string key;  // trackId|fieldKey
        public Color color;
        public bool visible;
    }

    private readonly List<ViolationMarker> markers = new List<ViolationMarker>();
    private readonly List<GameObject> markerObjects = new List<GameObject>();

    void Start()
    {
        minFlightTime = 0f;
        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
        }
        UpdateProgressUI();
    }

    void Update()
    {
        if (!autoFollowReceiver) return;
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + refreshInterval;
        UpdateFromReceiver();
    }

    private void UpdateFromReceiver()
    {
        var receiver = FlightDataStreamReceiver.Instance;
        if (receiver == null) return;

        var taskInfo = receiver.LatestTaskTimeInfo;
        if (taskInfo != null && taskInfo.durationSeconds > 0.01f)
            maxFlightTime = taskInfo.durationSeconds;

        if (cachedPlaybackPanel == null)
        {
            cachedPlaybackPanel = FindAnyObjectByType<FlightPlaybackPanel>();
            if (cachedPlaybackPanel == null)
            {
                var all = Resources.FindObjectsOfTypeAll<FlightPlaybackPanel>();
                if (all.Length > 0) cachedPlaybackPanel = all[0];
            }
        }

        if (cachedPlaybackPanel != null)
        {
            currentFlightTime = cachedPlaybackPanel.CurrentFlightTime;
            minFlightTime = cachedPlaybackPanel.MinFlightTime;
            if (cachedPlaybackPanel.MaxFlightTime > minFlightTime)
                maxFlightTime = cachedPlaybackPanel.MaxFlightTime;
            UpdateProgress();
        }
    }

    private void UpdateProgress()
    {
        // Do NOT write to progressSlider —FlightPlaybackPanel owns the slider
        // and writing from both controllers causes the handle to jump back and forth.
        UpdateMarkerPositions();
    }

    private void UpdateProgressUI()
    {
        // Only write to explicitly assigned progressText —do NOT fall back to
        // GetComponentInChildren<Text>(), because that Text is typically owned
        // by FlightPlaybackPanel and writing to it causes flickering.
        if (progressText == null) return;

        float progressPercentage = 0f;
        if (maxFlightTime > minFlightTime)
            progressPercentage = Mathf.Clamp01((currentFlightTime - minFlightTime) / (maxFlightTime - minFlightTime)) * 100f;

        progressText.text = string.Format("飞行进度: {0:F1}%", progressPercentage);
    }

    // ── 异常标记 API ──────────────────────────────────────────
    public void AddViolationMarker(string info)
    {
        AddViolationMarker(info, markerColor, info);
    }

    public void AddViolationMarker(string info, Color color)
    {
        AddViolationMarker(info, color, info);
    }

    public void AddViolationMarker(string info, Color color, string key)
    {
        if (progressSlider == null)
            return;

        SyncLiveFlightTime();
        float normalized = Mathf.Clamp01(progressSlider.normalizedValue);
        if (color.a < 0.01f)
            color = markerColor;
        if (string.IsNullOrEmpty(key))
            key = info;

        float trackWidth = 400f;
        var track = GetMarkerTrack();
        if (track != null && track.rect.width > 1f)
            trackWidth = track.rect.width;
        float minGap = 2.5f / trackWidth;

        for (int i = 0; i < markers.Count; i++)
        {
            if (markers[i].key == key && Mathf.Abs(markers[i].normalizedPos - normalized) < minGap)
                return;
        }

        var marker = new ViolationMarker
        {
            normalizedPos = normalized,
            info = info,
            key = key,
            color = color,
            visible = true,
        };
        markers.Add(marker);
        CreateMarkerObject(marker);
    }

    public void SetMarkerGroupVisible(string key, bool visible)
    {
        if (string.IsNullOrEmpty(key))
            return;
        for (int i = 0; i < markers.Count && i < markerObjects.Count; i++)
        {
            if (markers[i].key != key)
                continue;
            markers[i].visible = visible;
            if (markerObjects[i] != null)
                markerObjects[i].SetActive(visible);
        }
    }

    public void SetMarkerGroupColor(string key, Color color)
    {
        if (string.IsNullOrEmpty(key) || color.a < 0.01f)
            return;
        for (int i = 0; i < markers.Count && i < markerObjects.Count; i++)
        {
            if (markers[i].key != key)
                continue;
            markers[i].color = color;
            if (markerObjects[i] != null)
            {
                var img = markerObjects[i].GetComponent<Image>();
                if (img != null)
                    img.color = color;
            }
        }
    }

    private void SyncLiveFlightTime()
    {
        if (cachedPlaybackPanel == null)
        {
            cachedPlaybackPanel = FindAnyObjectByType<FlightPlaybackPanel>();
            if (cachedPlaybackPanel == null)
            {
                var all = Resources.FindObjectsOfTypeAll<FlightPlaybackPanel>();
                if (all.Length > 0) cachedPlaybackPanel = all[0];
            }
        }

        if (cachedPlaybackPanel != null)
        {
            currentFlightTime = cachedPlaybackPanel.CurrentFlightTime;
            minFlightTime = cachedPlaybackPanel.MinFlightTime;
            if (cachedPlaybackPanel.MaxFlightTime > minFlightTime)
                maxFlightTime = cachedPlaybackPanel.MaxFlightTime;
        }

        var receiver = FlightDataStreamReceiver.Instance;
        var taskInfo = receiver != null ? receiver.LatestTaskTimeInfo : null;
        if (taskInfo != null && taskInfo.durationSeconds > 0.01f)
            maxFlightTime = taskInfo.durationSeconds;
    }

    private RectTransform GetMarkerTrack()
    {
        if (progressSlider == null)
            return null;
        // Same parent as the handle so X matches the knob exactly.
        if (progressSlider.handleRect != null && progressSlider.handleRect.parent is RectTransform handleParent)
            return handleParent;
        if (progressSlider.fillRect != null && progressSlider.fillRect.parent is RectTransform fillArea)
            return fillArea;
        return progressSlider.fillRect;
    }

    private void LayoutMarker(RectTransform rt, float normalizedPos)
    {
        float width = Mathf.Max(1f, markerSize);
        width = Mathf.Min(width, 1.5f);
        float height = 8f;
        if (progressSlider != null && progressSlider.fillRect != null)
            height = Mathf.Max(2f, progressSlider.fillRect.rect.height - 2f);

        rt.anchorMin = new Vector2(normalizedPos, 0.5f);
        rt.anchorMax = new Vector2(normalizedPos, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = Vector2.zero;
    }

    private void CreateMarkerObject(ViolationMarker marker)
    {
        var track = GetMarkerTrack();
        if (track == null)
            return;

        var markerGO = new GameObject($"ViolationMarker_{markers.Count}");
        markerGO.transform.SetParent(track, false);
        markerGO.layer = progressSlider.gameObject.layer;

        var rt = markerGO.AddComponent<RectTransform>();
        LayoutMarker(rt, marker.normalizedPos);

        var img = markerGO.AddComponent<Image>();
        img.color = marker.color.a > 0.01f ? marker.color : markerColor;
        img.raycastTarget = false;
        markerGO.SetActive(marker.visible);

        markerObjects.Add(markerGO);
    }

    private void UpdateMarkerPositions()
    {
        for (int i = 0; i < markerObjects.Count && i < markers.Count; i++)
        {
            if (markerObjects[i] == null)
                continue;
            var rt = markerObjects[i].GetComponent<RectTransform>();
            if (rt == null)
                continue;
            LayoutMarker(rt, markers[i].normalizedPos);
        }
    }

    public void ClearViolationMarkers()
    {
        markers.Clear();
        foreach (var go in markerObjects)
        {
            if (go != null) Destroy(go);
        }
        markerObjects.Clear();
    }

    // ── Public API ─────────────────────────────────────────────

    public void SetFlightTime(float time)
    {
        currentFlightTime = time;
        UpdateProgress();
    }

    public void SetNormalizedProgress(float progress)
    {
        currentFlightTime = Mathf.Lerp(minFlightTime, maxFlightTime, Mathf.Clamp01(progress));
        UpdateProgress();
    }

    public void ResetProgress()
    {
        currentFlightTime = 0f;
        ClearViolationMarkers();
        UpdateProgress();
    }

    public float GetCurrentFlightTime() => currentFlightTime;
    public float GetMaxFlightTime() => maxFlightTime;
    public float GetNormalizedProgress() => Mathf.Clamp01((currentFlightTime - minFlightTime) / Mathf.Max(0.001f, maxFlightTime - minFlightTime));
}
