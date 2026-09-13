using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RadarDetectionSystem : MonoBehaviour
{
    private struct DetectionKey : IEquatable<DetectionKey>
    {
        public string sourceTrackId;
        public string targetTrackId;

        public bool Equals(DetectionKey other) => sourceTrackId == other.sourceTrackId && targetTrackId == other.targetTrackId;
        public override bool Equals(object obj) => obj is DetectionKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((sourceTrackId != null ? sourceTrackId.GetHashCode() : 0) * 397) ^ (targetTrackId != null ? targetTrackId.GetHashCode() : 0); }
        }
    }

    // ── Public detection result (for UI consumers) ─────────────

    public struct DetectionResult
    {
        public string sourceTrackId;
        public string targetTrackId;
        public string detectorType;
        public string rangeType;
        public float distance;
        public float detectRange;
        public float yaw;
        public float pitch;
        public float coneAngle;
        public string centerPoint;
        public float sourceLat;
        public float sourceLng;
        public float sourceHeading;
        public float targetLat;
        public float targetLng;
    }

    public struct SensorInfo
    {
        public string trackId;
        public string detectorType;
        public string rangeType;
        public float detectRange;
        public float yawAngle;
        public float pitchAngle;
        public string centerPoint;
        public float lat;
        public float lng;
        public float heading;
    }

    public List<DetectionResult> CurrentDetections { get; private set; } = new List<DetectionResult>();
    public List<SensorInfo> ActiveSensors { get; private set; } = new List<SensorInfo>();

    // ── Config ──────────────────────────────────────────────────

    [Header("Data")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private bool autoFindReceiver = true;
    [SerializeField] private float checkInterval = 0.2f;

    [Header("Geometry")]
    [SerializeField] private float defaultYawHalfAngle = 30f;
    [SerializeField] private float defaultPitchHalfAngle = 20f;
    [SerializeField] private float defaultConeHalfAngle = 25f;
    [SerializeField] private float aircraftLengthMeters = 20f;

    [Header("Output")]
    [SerializeField] private bool enableDetectionLog = true;
    [SerializeField] private bool logLostDetection = false;

    // ── UI Visualization ────────────────────────────────────────

    [Header("Detection UI")]
    [SerializeField] private PlaneDisplayController mapController;
    [SerializeField] private bool showDetectionSectors = true;
    [SerializeField] private bool showDetectionBeams = true;
    [SerializeField] private float sectorLineWidth = 1.5f;
    [SerializeField] private float beamLineWidth = 2.5f;
    [SerializeField] private int sectorSortingOrder = 6;
    [SerializeField] private int beamSortingOrder = 7;
    [SerializeField] private Color sectorColor = new Color(0f, 1f, 0.5f, 0.4f);
    [SerializeField] private Color sectorEdgeColor = new Color(0f, 1f, 0.5f, 0.8f);
    [SerializeField] private Color beamColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private int sectorArcSegments = 24;

    // ── Runtime state ───────────────────────────────────────────

    private readonly HashSet<DetectionKey> activeDetections = new HashSet<DetectionKey>();
    private float nextCheckTime;

    // Sector graphics keyed by trackId
    private readonly Dictionary<string, RadarPolylineGraphic> sectorLines = new Dictionary<string, RadarPolylineGraphic>();
    // Beam graphics keyed by "source→target"
    private readonly Dictionary<string, RadarPolylineGraphic> beamLines = new Dictionary<string, RadarPolylineGraphic>();
    private RectTransform radarGraphicRoot;

    private void Start()
    {
        if (mapController == null)
            mapController = FindAnyObjectByType<PlaneDisplayController>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheckTime)
            return;

        nextCheckTime = Time.unscaledTime + Mathf.Max(0.02f, checkInterval);
        EvaluateDetections();

        if (mapController != null)
        {
            if (showDetectionSectors)
                UpdateSectorVisuals();
            if (showDetectionBeams)
                UpdateBeamVisuals();
        }
    }

    // ── Detection logic (unchanged) ─────────────────────────────

    private void EvaluateDetections()
    {
        CurrentDetections.Clear();
        ActiveSensors.Clear();

        if (receiver == null && autoFindReceiver)
            receiver = FlightDataStreamReceiver.Instance;
        if (receiver == null)
            return;

        var positions = receiver.SnapshotPositions;
        var states = receiver.SnapshotStates;
        if (positions == null || states == null || positions.Count < 2 || states.Count == 0)
        {
            ClearDetections();
            return;
        }

        var positionsByTrack = new Dictionary<string, FlightDataStreamReceiver.TrackPosition>();
        for (int i = 0; i < positions.Count; i++)
        {
            var p = positions[i];
            if (p != null && !string.IsNullOrWhiteSpace(p.trackId))
                positionsByTrack[p.trackId] = p;
        }

        // Collect active sensors
        for (int i = 0; i < states.Count; i++)
        {
            var s = states[i];
            if (!IsSensorEnabled(s) || !positionsByTrack.TryGetValue(s.trackId, out var srcPos))
                continue;

            ActiveSensors.Add(new SensorInfo
            {
                trackId = s.trackId,
                detectorType = s.detectorType,
                rangeType = s.rangeType,
                detectRange = s.detectRange,
                yawAngle = GetPositiveOrDefault(s.yawAngle, defaultYawHalfAngle),
                pitchAngle = GetPositiveOrDefault(s.pitchAngle, defaultPitchHalfAngle),
                centerPoint = s.centerPoint,
                lat = srcPos.lat,
                lng = srcPos.lng,
                heading = srcPos.heading,
            });
        }

        // Run detections
        var currentDetections = new HashSet<DetectionKey>();
        for (int i = 0; i < states.Count; i++)
        {
            var sensorState = states[i];
            if (!IsSensorEnabled(sensorState) || !positionsByTrack.TryGetValue(sensorState.trackId, out var sourcePosition))
                continue;

            foreach (var kv in positionsByTrack)
            {
                string targetTrackId = kv.Key;
                if (targetTrackId == sensorState.trackId)
                    continue;

                var targetPosition = kv.Value;
                if (TryDetect(sourcePosition, sensorState, targetPosition, out float distance, out float yaw, out float pitch, out float coneAngle))
                {
                    var key = new DetectionKey { sourceTrackId = sensorState.trackId, targetTrackId = targetTrackId };
                    currentDetections.Add(key);

                    CurrentDetections.Add(new DetectionResult
                    {
                        sourceTrackId = sensorState.trackId,
                        targetTrackId = targetTrackId,
                        detectorType = sensorState.detectorType,
                        rangeType = sensorState.rangeType,
                        distance = distance,
                        detectRange = sensorState.detectRange,
                        yaw = yaw,
                        pitch = pitch,
                        coneAngle = coneAngle,
                        centerPoint = sensorState.centerPoint,
                        sourceLat = sourcePosition.lat,
                        sourceLng = sourcePosition.lng,
                        sourceHeading = sourcePosition.heading,
                        targetLat = targetPosition.lat,
                        targetLng = targetPosition.lng,
                    });

                    if (!activeDetections.Contains(key))
                        LogDetection(sensorState, targetTrackId, distance, yaw, pitch, coneAngle);
                }
            }
        }

        if (logLostDetection)
        {
            foreach (var old in activeDetections)
            {
                if (!currentDetections.Contains(old))
                    Debug.Log($"[RadarDetectionSystem] LOST source={old.sourceTrackId} target={old.targetTrackId}");
            }
        }

        activeDetections.Clear();
        foreach (var d in currentDetections)
            activeDetections.Add(d);
    }

    private void ClearDetections()
    {
        if (logLostDetection && activeDetections.Count > 0)
        {
            foreach (var old in activeDetections)
                Debug.Log($"[RadarDetectionSystem] LOST source={old.sourceTrackId} target={old.targetTrackId}");
        }
        activeDetections.Clear();
    }

    // ── UI: Detection sectors ───────────────────────────────────
    //
    // Draws a sector (fan shape) on the 2D map for each active sensor.
    // For 扇形: arc spanning ±yawAngle around heading
    // For 锥形: full circle (cone projected to 2D)
    // ─────────────────────────────────────────────────────────────

    private void UpdateSectorVisuals()
    {
        var activeKeys = new HashSet<string>();

        foreach (var sensor in ActiveSensors)
        {
            if (sensor.detectRange <= 0f)
                continue;

            string key = sensor.trackId;
            activeKeys.Add(key);

            // Get or create sector LineRenderer
            if (!sectorLines.TryGetValue(key, out var lr))
            {
                lr = CreateLineRenderer($"RadarSector_{key}", sectorSortingOrder, sectorLineWidth);
                lr.startColor = sectorEdgeColor;
                lr.endColor = sectorEdgeColor;

                sectorLines[key] = lr;
            }

            // Convert sensor position to UI coordinates
            Vector2 center = mapController.ConvertToAnchoredPosition(sensor.lng, sensor.lat);

            // Convert detectRange (meters) to UI pixel radius
            // by measuring pixel distance for a small geo offset, then scaling.
            // Use a small offset (1 metre) to stay within the mapped range and avoid clamping.
            float metersPerDegreeLat = 110540f;
            float smallOffsetDeg = 1f / metersPerDegreeLat; // 1 metre in degrees
            Vector2 p0 = mapController.ConvertToAnchoredPosition(sensor.lng, sensor.lat);
            Vector2 p1 = mapController.ConvertToAnchoredPosition(sensor.lng, sensor.lat + smallOffsetDeg);
            float pixelsPerMeter = Vector2.Distance(p0, p1); // pixels per 1 metre
            float radiusPx = sensor.detectRange * pixelsPerMeter;

            if (radiusPx < 5f)
                radiusPx = 5f;

            // Determine sector angles
            string rangeType = NormalizeText(sensor.rangeType);
            bool isCone = rangeType.Contains("锥") || rangeType.Contains("cone");
            float halfAngle = isCone ? 180f : Mathf.Clamp(sensor.yawAngle, 5f, 180f);

            // Heading: 0=N, 90=E. On UI: 0=up, rotate clockwise.
            float headingUi = sensor.heading;

            // Build sector outline: center →left edge →arc →right edge →center
            var bounds = GetMapBounds();
            var points = new List<Vector3>();
            points.Add(new Vector3(center.x, center.y, 0));

            float startAngle = headingUi - halfAngle;
            float endAngle = headingUi + halfAngle;

            for (int i = 0; i <= sectorArcSegments; i++)
            {
                float t = (float)i / sectorArcSegments;
                float angle = startAngle + (endAngle - startAngle) * t;
                float rad = angle * Mathf.Deg2Rad;
                // angle 0 = north = up in UI (positive Y)
                float x = center.x + radiusPx * Mathf.Sin(rad);
                float y = center.y + radiusPx * Mathf.Cos(rad);
                points.Add(new Vector3(x, y, 0));
            }

            points.Add(new Vector3(center.x, center.y, 0));

            for (int i = 0; i < points.Count; i++)
                points[i] = ClampToMap(points[i], bounds);
            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
            lr.gameObject.SetActive(true);

        }

        // Hide stale sectors
        foreach (var kv in sectorLines)
        {
            if (!activeKeys.Contains(kv.Key) && kv.Value != null)
                kv.Value.gameObject.SetActive(false);
        }
    }

    // ── UI: Detection beams ─────────────────────────────────────
    //
    // Draws a line from source to target for each active detection.
    // ─────────────────────────────────────────────────────────────

    private void UpdateBeamVisuals()
    {
        if (mapController != null && mapController.IsSuppressingRealtimeTrails)
        {
            foreach (var kv in beamLines)
                if (kv.Value != null)
                    kv.Value.gameObject.SetActive(false);
            return;
        }

        var activeKeys = new HashSet<string>();

        foreach (var det in CurrentDetections)
        {
            string key = $"{det.sourceTrackId}→{det.targetTrackId}";
            activeKeys.Add(key);

            if (!beamLines.TryGetValue(key, out var lr))
            {
                lr = CreateLineRenderer($"RadarBeam_{key}", beamSortingOrder, beamLineWidth);
                lr.startColor = beamColor;
                lr.endColor = beamColor;
                beamLines[key] = lr;
            }

            Vector2 src = mapController.ConvertToAnchoredPosition(det.sourceLng, det.sourceLat);
            Vector2 tgt = mapController.ConvertToAnchoredPosition(det.targetLng, det.targetLat);

            var bounds = GetMapBounds();
            Vector3 p0 = ClampToMap(new Vector3(src.x, src.y, 0), bounds);
            Vector3 p1 = ClampToMap(new Vector3(tgt.x, tgt.y, 0), bounds);
            lr.positionCount = 2;
            lr.SetPosition(0, p0);
            lr.SetPosition(1, p1);
            lr.gameObject.SetActive(true);
        }

        // Hide stale beams
        foreach (var kv in beamLines)
        {
            if (!activeKeys.Contains(kv.Key) && kv.Value != null)
                kv.Value.gameObject.SetActive(false);
        }
    }

    private void EnsureRadarGraphicRoot(RectTransform mapRoot)
    {
        if (mapController != null)
        {
            var overlay = mapController.MapOverlayRoot;
            if (overlay != null)
            {
                radarGraphicRoot = overlay;
                return;
            }
        }

        if (radarGraphicRoot != null || mapRoot == null)
            return;

        var existing = mapRoot.Find("MapOverlay");
        if (existing == null)
            existing = mapRoot.Find("RadarGraphics");
        var go = existing != null ? existing.gameObject : new GameObject("MapOverlay", typeof(RectTransform));
        radarGraphicRoot = go.GetComponent<RectTransform>();
        radarGraphicRoot.SetParent(mapRoot, false);
        radarGraphicRoot.anchorMin = Vector2.zero;
        radarGraphicRoot.anchorMax = Vector2.one;
        radarGraphicRoot.offsetMin = Vector2.zero;
        radarGraphicRoot.offsetMax = Vector2.zero;
        radarGraphicRoot.pivot = new Vector2(0.5f, 0.5f);
        var pos = radarGraphicRoot.localPosition;
        pos.z = 0f;
        radarGraphicRoot.localPosition = pos;
        radarGraphicRoot.SetAsLastSibling();
    }

    private RadarPolylineGraphic CreateLineRenderer(string name, int sortingOrder, float width)
    {
        var mapRoot = GetMapRoot();
        EnsureRadarGraphicRoot(mapRoot);
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RadarPolylineGraphic));
        go.transform.SetParent(radarGraphicRoot != null ? radarGraphicRoot : mapRoot != null ? mapRoot : transform, false);
        go.layer = mapRoot != null ? mapRoot.gameObject.layer : 0;

        var graphic = go.GetComponent<RadarPolylineGraphic>();
        graphic.startWidth = width;
        graphic.endWidth = width;
        graphic.sortingOrder = sortingOrder;
        graphic.raycastTarget = false;
        return graphic;
    }

    private RectTransform GetMapRoot()
    {
        if (mapController == null)
            return null;
        if (mapController.MapRoot != null)
            return mapController.MapRoot;
        var field = typeof(PlaneDisplayController).GetField("mapRoot",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(mapController) as RectTransform;
    }

    private Rect GetMapBounds()
    {
        var rt = GetMapRoot();
        if (rt == null)
            return new Rect(-10000, -10000, 20000, 20000);
        var rect = rt.rect;
        return rect;
    }

    private static Vector3 ClampToMap(Vector3 p, Rect bounds)
    {
        float halfW = bounds.width * 0.5f;
        float halfH = bounds.height * 0.5f;
        p.x = Mathf.Clamp(p.x, -halfW, halfW);
        p.y = Mathf.Clamp(p.y, -halfH, halfH);
        return p;
    }

    // ── Detection math (unchanged) ──────────────────────────────

    private bool TryDetect(
        FlightDataStreamReceiver.TrackPosition source,
        FlightDataStreamReceiver.TrackTimetableState sensor,
        FlightDataStreamReceiver.TrackPosition target,
        out float distance, out float yaw, out float pitch, out float coneAngle)
    {
        distance = 0f; yaw = 0f; pitch = 0f; coneAngle = 0f;

        Vector3 sourceMeters = GeoToLocalMeters(source, source.lat, source.lng);
        Vector3 targetMeters = GeoToLocalMeters(target, source.lat, source.lng);
        Vector3 forward = GetForwardVector(source.heading, source.pitch).normalized;
        Vector3 sensorOrigin = sourceMeters + forward * GetCenterOffset(sensor.centerPoint);
        Vector3 toTarget = targetMeters - sensorOrigin;
        distance = toTarget.magnitude;

        float maxRange = Mathf.Max(0f, sensor.detectRange);
        if (maxRange <= 0f || distance <= 0.001f || distance > maxRange)
            return false;

        Vector3 local = WorldToAircraftLocal(toTarget, source.heading, source.pitch);
        yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        pitch = Mathf.Atan2(local.y, Mathf.Sqrt(local.x * local.x + local.z * local.z)) * Mathf.Rad2Deg;
        coneAngle = Vector3.Angle(forward, toTarget.normalized);

        string rangeType = NormalizeText(sensor.rangeType);
        if (rangeType.Contains("锥") || rangeType.Contains("cone"))
        {
            float coneHalfAngle = Mathf.Max(
                GetPositiveOrDefault(sensor.yawAngle, defaultConeHalfAngle),
                GetPositiveOrDefault(sensor.pitchAngle, defaultConeHalfAngle));
            return coneAngle <= coneHalfAngle;
        }

        float yawHalfAngle = GetPositiveOrDefault(sensor.yawAngle, defaultYawHalfAngle);
        float pitchHalfAngle = GetPositiveOrDefault(sensor.pitchAngle, defaultPitchHalfAngle);
        return Mathf.Abs(yaw) <= yawHalfAngle && Mathf.Abs(pitch) <= pitchHalfAngle;
    }

    private static Vector3 GeoToLocalMeters(FlightDataStreamReceiver.TrackPosition pos, float originLat, float originLng)
    {
        float latRad = originLat * Mathf.Deg2Rad;
        float metersPerDegLon = 111320f * Mathf.Cos(latRad);
        const float metersPerDegLat = 110540f;
        return new Vector3((pos.lng - originLng) * metersPerDegLon, pos.alt, (pos.lat - originLat) * metersPerDegLat);
    }

    private static Vector3 GetForwardVector(float headingDeg, float pitchDeg)
    {
        float h = headingDeg * Mathf.Deg2Rad;
        float p = pitchDeg * Mathf.Deg2Rad;
        float horiz = Mathf.Cos(p);
        return new Vector3(Mathf.Sin(h) * horiz, Mathf.Sin(p), Mathf.Cos(h) * horiz);
    }

    private static Vector3 WorldToAircraftLocal(Vector3 world, float headingDeg, float pitchDeg)
    {
        Vector3 fwd = GetForwardVector(headingDeg, pitchDeg).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        Vector3 up = Vector3.Cross(fwd, right).normalized;
        return new Vector3(Vector3.Dot(world, right), Vector3.Dot(world, up), Vector3.Dot(world, fwd));
    }

    private float GetCenterOffset(string centerPoint)
    {
        string c = NormalizeText(centerPoint);
        if (c.Contains("机头") || c.Contains("nose")) return aircraftLengthMeters * 0.5f;
        if (c.Contains("机尾") || c.Contains("tail")) return -aircraftLengthMeters * 0.5f;
        return 0f;
    }

    private static float GetPositiveOrDefault(float value, float fallback) => value > 0.001f ? value : fallback;

    private static bool IsSensorEnabled(FlightDataStreamReceiver.TrackTimetableState s)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.trackId)) return false;
        string status = NormalizeText(s.radarStatus);
        if (status.Contains("无效") || status.Contains("invalid") || status.Contains("off") || status.Contains("disabled")) return false;
        string d = NormalizeText(s.detectorType);
        return d.Contains("雷达") || d.Contains("电测") || d.Contains("光测") ||
               d.Contains("radar") || d.Contains("elect") || d.Contains("optic");
    }

    private static string NormalizeText(string v) => string.IsNullOrWhiteSpace(v) ? string.Empty : v.Trim().ToLowerInvariant();

    private void LogDetection(FlightDataStreamReceiver.TrackTimetableState sensor, string target, float distance, float yaw, float pitch, float coneAngle)
    {
        if (!enableDetectionLog) return;
        Debug.Log($"[RadarDetectionSystem] DETECTED detector={sensor.detectorType} source={sensor.trackId} target={target} " +
                  $"rangeType={sensor.rangeType} distance={distance:F1}m maxRange={sensor.detectRange:F1}m " +
                  $"yaw={yaw:+0.0;-0.0;0.0}deg pitch={pitch:+0.0;-0.0;0.0}deg cone={coneAngle:F1}deg center={sensor.centerPoint}");
    }
}
