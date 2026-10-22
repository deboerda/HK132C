using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 飞行视图：用二维同一套经纬度、姿态、航迹和扫描锥。
/// 雷达是立体锥，只有目标真的在锥内才画扫描线。
/// </summary>
public class FlightSceneView : MonoBehaviour
{
    private const int FlightLayer = 3;

    private Camera flightCamera;
    private RawImage viewImage;
    private Transform root;
    private bool userHasControl;
    private bool flying;
    private bool panning;
    private bool orbiting;
    private bool zoomDragging;
    private float flySpeed = 8f;
    private float orbitDistance = 80f;
    private Vector3 orbitPivot;
    private float yaw;
    private float pitch;
    private Material skyMaterial;
    private GameObject modelTemplate;
    private bool hasOrigin;
    private float originLat;
    private float originLng;
    private float nextRefresh;
    private readonly Dictionary<string, Transform> planes = new Dictionary<string, Transform>();
    private readonly Dictionary<string, LineRenderer> tracks = new Dictionary<string, LineRenderer>();
    private readonly Dictionary<string, MeshFilter> volumes = new Dictionary<string, MeshFilter>();
    private readonly Dictionary<string, LineRenderer> hits = new Dictionary<string, LineRenderer>();
    private readonly Dictionary<string, LineRenderer> volumeEdges = new Dictionary<string, LineRenderer>();
    private readonly Dictionary<string, List<Vector3>> liveTrails = new Dictionary<string, List<Vector3>>();
    private readonly Dictionary<string, float> stableAltitude = new Dictionary<string, float>();
    private readonly Dictionary<string, float> altitudeTime = new Dictionary<string, float>();
    private readonly Dictionary<string, Vector3> planeCenters = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, TextMesh> labels = new Dictionary<string, TextMesh>();
    private Vector3 unitsPerMeter = Vector3.one;
    private static bool labelsVisible = true;

    public static FlightSceneView Active { get; private set; }
    public float RadarEdgePixels = 1.15f;
    public float TrailWidthMeters = 12f;
    public float ModelLength { get; private set; } = 18f;
    private float appliedModelLength = 18f;

    public bool LabelsVisible => labelsVisible;

    public void SetModelLength(float length)
    {
        length = Mathf.Clamp(length, 0.2f, 80f);
        float ratio = length / Mathf.Max(0.01f, appliedModelLength);
        ModelLength = length;
        appliedModelLength = length;
        if (Mathf.Abs(ratio - 1f) < 0.001f)
            return;
        foreach (var kv in planes)
        {
            if (kv.Value != null)
                kv.Value.localScale *= ratio;
        }
    }

    public void SetLabelsVisible(bool visible)
    {
        labelsVisible = visible;
        foreach (var kv in labels)
        {
            if (kv.Value != null)
                kv.Value.gameObject.SetActive(visible);
        }
    }

    public void ToggleLabels()
    {
        SetLabelsVisible(!labelsVisible);
    }

    public void Bind(Camera camera, RawImage image = null, bool reframe = false)
    {
        Active = this;
        flightCamera = camera;
        var saved = PanelMemory.Load();
        if (saved.planeIcon != null && saved.planeIcon.saved && saved.planeIcon.radarLine3D > 0.05f)
            RadarEdgePixels = saved.planeIcon.radarLine3D;
        if (saved.planeIcon != null && saved.planeIcon.saved && saved.planeIcon.modelLength > 0.15f)
            SetModelLength(saved.planeIcon.modelLength);
        if (saved.planeIcon != null && saved.planeIcon.saved && saved.planeIcon.trailWidth3D > 0.2f)
            TrailWidthMeters = saved.planeIcon.trailWidth3D;
        if (image != null)
            viewImage = image;
        if (reframe)
            FrameCamera(lastFocus, lastHeading, Mathf.Max(40f, lastSpan), true);
        else
            ApplyDefaultCameraIfNeeded();
        EnsureRoot();
        EnsureSky();
        if (flightCamera != null)
        {
            flightCamera.cullingMask = 1 << FlightLayer;
            flightCamera.clearFlags = CameraClearFlags.Skybox;
            var sky = flightCamera.GetComponent<Skybox>();
            if (sky == null)
                sky = flightCamera.gameObject.AddComponent<Skybox>();
            sky.material = skyMaterial;
            Vector3 euler = flightCamera.transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }
    }

    private void Update()
    {
        if (flightCamera == null || !flightCamera.enabled)
            return;
        Navigate();
        FaceLabels();
        if (Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + 0.2f;
        Refresh();
    }

    private void EnsureRoot()
    {
        if (root != null)
            return;
        var existing = GameObject.Find("FlightSceneRoot");
        root = existing != null ? existing.transform : new GameObject("FlightSceneRoot").transform;
        SetLayer(root.gameObject);
        var placeholder = GameObject.Find("FlightViewPlaceholder");
        if (placeholder != null)
            placeholder.SetActive(false);
    }

    private void EnsureSky()
    {
        if (skyMaterial != null)
            return;
        var shader = Shader.Find("Skybox/Procedural");
        if (shader == null)
            return;
        skyMaterial = new Material(shader);
        skyMaterial.SetFloat("_SunSize", 0.02f);
        skyMaterial.SetFloat("_AtmosphereThickness", 0.7f);
        skyMaterial.SetColor("_SkyTint", new Color(0.45f, 0.62f, 0.85f));
        skyMaterial.SetColor("_GroundColor", new Color(0.45f, 0.62f, 0.85f));
    }

    private void Refresh()
    {
        EnsureRoot();
        var receiver = FlightDataStreamReceiver.Instance;
        var radar = FindAnyObjectByType<RadarDetectionSystem>();
        if (receiver == null)
        {
            FrameCamera(Vector3.zero, 0f, 40f);
            return;
        }

        var positions = receiver.SnapshotPositions;
        if (positions == null || positions.Count == 0)
        {
            FrameCamera(Vector3.zero, 0f, 40f);
            return;
        }

        if (!hasOrigin)
        {
            originLat = (float)positions[0].lat;
            originLng = (float)positions[0].lng;
            hasOrigin = true;
        }
        RefreshDisplayScale();

        var usedPlanes = new HashSet<string>();
        Vector3 sum = Vector3.zero;
        int n = 0;
        for (int i = 0; i < positions.Count; i++)
        {
            var pos = positions[i];
            if (pos == null || string.IsNullOrWhiteSpace(pos.trackId))
                continue;
            float alt = StableAltitude(pos.trackId, pos.alt);
            Vector3 world = ToVisual(pos.lng, pos.lat, alt);
            var plane = GetPlane(pos.trackId);
            plane.rotation = FlightAttitude(pos.heading, pos.pitch, pos.roll);
            plane.position = world;
            Vector3 center = RendererCenter(plane.gameObject);
            plane.position += world - center;
            planeCenters[pos.trackId] = world;
            AppendLiveTrail(pos.trackId, world);
            usedPlanes.Add(pos.trackId);
            sum += world;
            n++;
        }
        HideUnused(planes, usedPlanes);
        DrawDifferenceLabels(positions);
        HideUnused(labels, usedPlanes);

        DrawTracks(receiver);
        DrawVolumes(radar, positions);
        if (n > 0)
            FrameToMappedWindow(sum / n, MeanHeading(positions), FormationSpan(positions));
    }

    private void RefreshDisplayScale()
    {
        var map = FindAnyObjectByType<PlaneDisplayController>();
        if (map != null && map.TryGetDisplayScale(originLat, out _, out _, out float uniform))
        {
            uniform = Mathf.Max(1e-6f, uniform);
            unitsPerMeter = new Vector3(uniform, uniform, uniform);
        }
    }

    private Vector3 ToVisual(double lng, double lat, float alt)
    {
        return ToVisual(RadarDetectionSystem.GeoToLocalMeters(lng, lat, alt, originLat, originLng));
    }

    private Vector3 ToVisual(Vector3 eastUpNorthMeters)
    {
        return Vector3.Scale(eastUpNorthMeters, unitsPerMeter);
    }

    private Vector3 FromVisual(Vector3 visual)
    {
        return new Vector3(
            visual.x / Mathf.Max(1e-6f, unitsPerMeter.x),
            visual.y / Mathf.Max(1e-6f, unitsPerMeter.y),
            visual.z / Mathf.Max(1e-6f, unitsPerMeter.z));
    }

    private Vector3 AircraftApex(RadarDetectionSystem.SensorInfo sensor, float alt)
    {
        Vector3 body = planeCenters.TryGetValue(sensor.trackId, out var center)
            ? center
            : ToVisual(sensor.lng, sensor.lat, alt);
        // 网格机头在局部 +Y，但姿态四元数之后世界朝向是 -Y，沿 +Y 会把雷达推到机尾。
        Vector3 nose = -(FlightAttitude(sensor.heading, sensor.pitch, sensor.roll) * Vector3.up);
        if (nose.sqrMagnitude < 1e-4f)
            return body;
        float along = RadarStationTuner.Instance != null
            ? RadarStationTuner.Instance.Offset3D(sensor.mountSign)
            : sensor.mountSign * 8f;
        float up = RadarStationTuner.Instance != null
            ? RadarStationTuner.Instance.OffsetUp(sensor.mountSign)
            : 0f;
        return body + nose.normalized * along + Vector3.up * up;
    }

    private void FrameToMappedWindow(Vector3 aircraftFocus, float heading, float aircraftSpan)
    {
        var map = FindAnyObjectByType<PlaneDisplayController>();
        if (map != null && map.TryGetMapWindow(out float centerLng, out float centerLat, out float span))
        {
            Vector3 focus = ToVisual(centerLng, centerLat, 0f);
            FrameCamera(focus, heading, Mathf.Max(span, aircraftSpan));
            return;
        }
        FrameCamera(aircraftFocus, heading, aircraftSpan);
    }

    private static float MeanHeading(List<FlightDataStreamReceiver.TrackPosition> positions)
    {
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < positions.Count; i++)
        {
            var pos = positions[i];
            if (pos == null)
                continue;
            float rad = pos.heading * Mathf.Deg2Rad;
            sum += new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }
        if (sum.sqrMagnitude < 0.001f)
            return 0f;
        return Mathf.Atan2(sum.x, sum.y) * Mathf.Rad2Deg;
    }

    public static Quaternion FlightAttitude(float heading, float pitch, float roll)
    {
        // 航向北偏东、俯仰上正。上一版多转了 180°，机头和雷达锥相反；模型网格机头在 +Y，只用 -90° 放平且机背朝上。
        Quaternion yaw = Quaternion.AngleAxis(heading, Vector3.up);
        Quaternion noseUp = Quaternion.AngleAxis(-pitch, yaw * Vector3.right);
        Quaternion bank = Quaternion.AngleAxis(-roll, noseUp * yaw * Vector3.forward);
        return bank * noseUp * yaw * Quaternion.Euler(-90f, 0f, 0f);
    }

    private static Vector3 RendererCenter(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return go.transform.position;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds.center;
    }

    private float StableAltitude(string trackId, float raw)
    {
        float now = Time.unscaledTime;
        if (!stableAltitude.TryGetValue(trackId, out float prev))
        {
            stableAltitude[trackId] = raw;
            altitudeTime[trackId] = now;
            return raw;
        }
        float dt = Mathf.Max(0.05f, now - altitudeTime[trackId]);
        altitudeTime[trackId] = now;
        // test_data 后段高度每秒跳几十米，但俯仰只有零点几度，不能当真实爬升。
        if (raw <= 1f && prev > 50f)
            return prev;
        if (Mathf.Abs(raw - prev) / dt > 25f)
            return prev;
        stableAltitude[trackId] = raw;
        return raw;
    }

    private PlaneDisplayController trailSettingsSource;

    /// <summary>航迹采样间隔跟二维共用同一个值（米），降采样省性能。</summary>
    private float ResolveTrailSampleMeters()
    {
        if (trailSettingsSource == null)
            trailSettingsSource = FindAnyObjectByType<PlaneDisplayController>();
        return trailSettingsSource != null ? Mathf.Max(1f, trailSettingsSource.TrailSampleMeters) : 50f;
    }

    /// <summary>点数上限也跟二维一致，保证两边覆盖的公里数相同。</summary>
    private int ResolveTrailMaxPoints()
    {
        if (trailSettingsSource == null)
            trailSettingsSource = FindAnyObjectByType<PlaneDisplayController>();
        return trailSettingsSource != null ? (int)trailSettingsSource.TrailMaxPoints : 12000;
    }

    private void AppendLiveTrail(string trackId, Vector3 world)
    {
        if (!liveTrails.TryGetValue(trackId, out var trail))
        {
            trail = new List<Vector3>();
            liveTrails[trackId] = trail;
        }
        float step = Mathf.Max(1e-4f, ResolveTrailSampleMeters() * Mathf.Max(unitsPerMeter.x, unitsPerMeter.z));
        if (trail.Count == 0 || (trail[trail.Count - 1] - world).sqrMagnitude > step * step)
            trail.Add(world);
        // 全程保留，只留一个防爆上限，不再按时间去截断。
        int cap = ResolveTrailMaxPoints();
        if (trail.Count > cap)
            trail.RemoveRange(0, trail.Count - cap);
    }

    public void ClearTrails(string trackId = null)
    {
        bool all = string.IsNullOrWhiteSpace(trackId);
        var drop = new List<string>();
        foreach (var kv in liveTrails)
        {
            if (!all && kv.Key != trackId)
                continue;
            kv.Value.Clear();
            drop.Add(kv.Key);
        }
        for (int i = 0; i < drop.Count; i++)
            liveTrails.Remove(drop[i]);

        foreach (var kv in tracks)
        {
            if (kv.Value == null)
                continue;
            if (!all && kv.Key != trackId && kv.Key != "live_" + trackId)
                continue;
            kv.Value.positionCount = 0;
            kv.Value.gameObject.SetActive(false);
        }
    }

    private Transform GetPlane(string trackId)
    {
        if (planes.TryGetValue(trackId, out var existing) && existing != null)
            return existing;
        var go = Instantiate(GetTemplate());
        go.name = "FlightPlane_" + trackId;
        go.transform.SetParent(root, false);
        foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }
        foreach (var collider in go.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null)
                Destroy(collider);
        }
        NormalizeLength(go, ModelLength);
        appliedModelLength = ModelLength;
        SetLayer(go);
        planes[trackId] = go.transform;
        return go.transform;
    }

    private const float EarthRadiusMeters = 6371000f;

    private void DrawDifferenceLabels(List<FlightDataStreamReceiver.TrackPosition> positions)
    {
        if (!labelsVisible || positions == null)
        {
            foreach (var kv in labels)
            {
                if (kv.Value != null)
                    kv.Value.gameObject.SetActive(false);
            }
            return;
        }

        var live = new List<FlightDataStreamReceiver.TrackPosition>();
        for (int i = 0; i < positions.Count; i++)
        {
            var pos = positions[i];
            if (pos == null || string.IsNullOrWhiteSpace(pos.trackId))
                continue;
            if (!planeCenters.ContainsKey(pos.trackId))
                continue;
            live.Add(pos);
        }
        live.Sort((a, b) => string.Compare(a.trackId, b.trackId, System.StringComparison.Ordinal));

        var tuner = DifferenceLabelTuner.Instance;
        float cycle = tuner != null ? Mathf.Max(1f, tuner.cycleSeconds) : 4f;
        int peerSlot = live.Count > 1
            ? Mathf.FloorToInt(Time.unscaledTime / cycle)
            : 0;
        Color color = tuner != null ? tuner.textColor : new Color(0.92f, 0.96f, 1f, 1f);
        Vector3 camRight = flightCamera != null ? flightCamera.transform.right : Vector3.right;
        Vector3 camForward = flightCamera != null ? flightCamera.transform.forward : Vector3.forward;
        camRight.y = 0f;
        if (camRight.sqrMagnitude > 0.0001f)
            camRight.Normalize();
        camForward.y = 0f;
        if (camForward.sqrMagnitude > 0.0001f)
            camForward.Normalize();
        float right = tuner != null ? tuner.offsetRight : 0f;
        float up = tuner != null ? tuner.offsetUp : 12f;
        float forward = tuner != null ? tuner.offsetForward : 0f;

        for (int i = 0; i < live.Count; i++)
        {
            var pos = live[i];
            Vector3 world = planeCenters[pos.trackId];
            var mesh = GetLabel(pos.trackId);
            mesh.gameObject.SetActive(true);
            mesh.color = color;
            mesh.text = BuildCycledDifferenceLabel(pos, live, peerSlot);
            float scale = tuner != null ? Mathf.Max(0.05f, tuner.textScale) : 1f;
            mesh.transform.localScale = new Vector3(scale, scale, scale);
            mesh.transform.position = world + camRight * right + Vector3.up * up + camForward * forward;
            FaceLabel(mesh);
        }
    }

    private void FaceLabels()
    {
        if (!labelsVisible || flightCamera == null)
            return;
        foreach (var kv in labels)
            FaceLabel(kv.Value);
    }

    private void FaceLabel(TextMesh mesh)
    {
        if (mesh == null || flightCamera == null || !mesh.gameObject.activeInHierarchy)
            return;
        Vector3 toCam = mesh.transform.position - flightCamera.transform.position;
        if (toCam.sqrMagnitude > 0.0001f)
            mesh.transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
    }

    private static string BuildCycledDifferenceLabel(
        FlightDataStreamReceiver.TrackPosition pos,
        List<FlightDataStreamReceiver.TrackPosition> live,
        int peerSlot)
    {
        string id = string.IsNullOrWhiteSpace(pos.trackId) ? "unknown" : pos.trackId;
        if (live == null || live.Count <= 1)
            return id;

        var others = new List<FlightDataStreamReceiver.TrackPosition>(live.Count - 1);
        for (int i = 0; i < live.Count; i++)
        {
            if (live[i].trackId != pos.trackId)
                others.Add(live[i]);
        }
        if (others.Count == 0)
            return id;

        var peer = others[Mathf.Abs(peerSlot) % others.Count];
        string peerId = string.IsNullOrWhiteSpace(peer.trackId) ? "unknown" : peer.trackId;
        float distance = HorizontalDistanceMeters(pos, peer);
        float altitude = peer.alt - pos.alt;
        float azimuth = RelativeAzimuthDeg(pos, peer);
        float pitchDelta = Mathf.DeltaAngle(pos.pitch, peer.pitch);
        return id
            + "\n相对 " + peerId
            + "\n距离差 " + FormatDistance(distance)
            + "\n高度差 " + FormatSigned(altitude, "m")
            + "\n方位角差 " + FormatSigned(azimuth, "deg")
            + "\n俯仰角差 " + FormatSigned(pitchDelta, "deg");
    }

    private static float HorizontalDistanceMeters(FlightDataStreamReceiver.TrackPosition a, FlightDataStreamReceiver.TrackPosition b)
    {
        float lat1 = (float)a.lat * Mathf.Deg2Rad;
        float lat2 = (float)b.lat * Mathf.Deg2Rad;
        float dLat = (float)(b.lat - a.lat) * Mathf.Deg2Rad;
        float dLng = (float)(b.lng - a.lng) * Mathf.Deg2Rad;
        float sinLat = Mathf.Sin(dLat * 0.5f);
        float sinLng = Mathf.Sin(dLng * 0.5f);
        float h = sinLat * sinLat + Mathf.Cos(lat1) * Mathf.Cos(lat2) * sinLng * sinLng;
        float c = 2f * Mathf.Atan2(Mathf.Sqrt(h), Mathf.Sqrt(Mathf.Max(0f, 1f - h)));
        return EarthRadiusMeters * c;
    }

    /// <summary>对方相对本机机头的方位：0 正前，正值右舷，负值左舷。</summary>
    private static float RelativeAzimuthDeg(FlightDataStreamReceiver.TrackPosition from, FlightDataStreamReceiver.TrackPosition to)
    {
        return Mathf.DeltaAngle(from.heading, BearingDeg(from, to));
    }

    private static float BearingDeg(FlightDataStreamReceiver.TrackPosition from, FlightDataStreamReceiver.TrackPosition to)
    {
        float lat1 = (float)from.lat * Mathf.Deg2Rad;
        float lat2 = (float)to.lat * Mathf.Deg2Rad;
        float dLng = (float)(to.lng - from.lng) * Mathf.Deg2Rad;
        float y = Mathf.Sin(dLng) * Mathf.Cos(lat2);
        float x = Mathf.Cos(lat1) * Mathf.Sin(lat2) - Mathf.Sin(lat1) * Mathf.Cos(lat2) * Mathf.Cos(dLng);
        return Mathf.Atan2(y, x) * Mathf.Rad2Deg;
    }

    private static string FormatDistance(float meters)
    {
        return meters >= 1000f ? $"{meters / 1000f:F2} km" : $"{meters:F1} m";
    }

    private static string FormatSigned(float value, string unit)
    {
        return $"{value:+0.0;-0.0;0.0} {unit}";
    }

    private TextMesh GetLabel(string trackId)
    {
        if (labels.TryGetValue(trackId, out var existing) && existing != null)
            return existing;
        var go = new GameObject("DiffLabel_" + trackId);
        go.transform.SetParent(root, false);
        var mesh = go.AddComponent<TextMesh>();
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.fontSize = 48;
        mesh.characterSize = 0.22f;
        mesh.anchor = TextAnchor.LowerCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = new Color(0.92f, 0.96f, 1f, 1f);
        mesh.text = trackId;
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        SetLayer(go);
        labels[trackId] = mesh;
        return mesh;
    }

    private GameObject GetTemplate()
    {
        if (modelTemplate != null)
            return modelTemplate;
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null || !t.gameObject.scene.IsValid())
                continue;
            if (t.name != "J35")
                continue;
            if (t.parent != null && t.parent.name == "FlightSceneRoot")
                continue;
            modelTemplate = t.gameObject;
            break;
        }
        if (modelTemplate == null)
        {
            modelTemplate = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            modelTemplate.name = "FlightPlaneTemplate";
            modelTemplate.transform.localScale = new Vector3(2f, 6f, 2f);
            modelTemplate.SetActive(false);
        }
        return modelTemplate;
    }

    private static Color TrailColor(string trackId)
    {
        var map = FindAnyObjectByType<PlaneDisplayController>();
        if (map == null)
            return new Color(0.05f, 0.85f, 1f, 0.9f);
        Color color = map.GetTrailColor(trackId);
        color.a = Mathf.Max(color.a, 0.9f);
        return color;
    }

    private static bool TrailVisible(string trackId)
    {
        var map = FindAnyObjectByType<PlaneDisplayController>();
        return map == null || map.IsPlaneTrailVisible(trackId);
    }

    private static void ApplyTrailColor(LineRenderer lr, Color color)
    {
        lr.startColor = lr.endColor = color;
        lr.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) }
        };
    }

    private void DrawTracks(FlightDataStreamReceiver receiver)
    {
        var lines = receiver.SnapshotTrackLines;
        var used = new HashSet<string>();
        if (lines != null)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null || line.positions == null || line.positions.Count < 2)
                    continue;
                string key = string.IsNullOrWhiteSpace(line.trackId) ? "track_" + i : line.trackId;
                float planWidth = Mathf.Max(0.002f, TrailWidthMeters * Mathf.Max(unitsPerMeter.x, unitsPerMeter.z));
                var lr = GetLine(tracks, key, planWidth, TrailColor(line.trackId));
                lr.widthMultiplier = planWidth;
                int count = 0;
                var buffer = new Vector3[line.positions.Count];
                for (int p = 0; p < line.positions.Count; p++)
                {
                    var pos = line.positions[p];
                    if (pos == null)
                        continue;
                    buffer[count++] = ToVisual(pos.lng, pos.lat, pos.alt);
                }
                if (count < 2)
                {
                    lr.gameObject.SetActive(false);
                    continue;
                }
                lr.positionCount = count;
                for (int p = 0; p < count; p++)
                    lr.SetPosition(p, buffer[p]);
                ApplyTrailColor(lr, TrailColor(line.trackId));
                lr.gameObject.SetActive(TrailVisible(line.trackId));
                used.Add(key);
            }
        }
        float liveWidth = Mathf.Max(0.002f, TrailWidthMeters * Mathf.Max(unitsPerMeter.x, unitsPerMeter.z));
        foreach (var kv in liveTrails)
        {
            if (kv.Value.Count < 2)
                continue;
            var lr = GetLine(tracks, "live_" + kv.Key, liveWidth, TrailColor(kv.Key));
            lr.widthMultiplier = liveWidth;
            // 全程航迹点数很多，一次 SetPositions 比逐点 SetPosition 快得多。
            lr.positionCount = kv.Value.Count;
            lr.SetPositions(kv.Value.ToArray());
            ApplyTrailColor(lr, TrailColor(kv.Key));
            lr.gameObject.SetActive(TrailVisible(kv.Key));
            used.Add("live_" + kv.Key);
        }
        HideUnused(tracks, used);
    }

    private void DrawVolumes(RadarDetectionSystem radar, List<FlightDataStreamReceiver.TrackPosition> positions)
    {
        var usedVolumes = new HashSet<string>();
        var usedEdges = new HashSet<string>();
        var usedHits = new HashSet<string>();
        if (radar == null)
        {
            HideUnused(volumes, usedVolumes);
            HideUnused(volumeEdges, usedEdges);
            HideUnused(hits, usedHits);
            return;
        }

        var sensors = radar.ActiveSensors;
        for (int i = 0; i < sensors.Count; i++)
        {
            var sensor = sensors[i];
            if (sensor.maxRangeMeters <= 0f)
                continue;
            string key = RadarDetectionSystem.MakeSensorKey(sensor.trackId, sensor.detectorType);
            if (!radar.IsSensorVisible(sensor.trackId, sensor.detectorType))
                continue;
            float alt = StableAltitude(sensor.trackId, sensor.alt);
            Vector3 apex = AircraftApex(sensor, alt);
            Vector3 originMeters = FromVisual(apex);
            Color color = radar.GetSensorColor(sensor.trackId, sensor.detectorType);
            bool anyHit = false;
            // 直接读二维那套检测结果，保证同一时刻二维填充、触发环和三维触发线是同一个集合。
            var detections = radar.CurrentDetections;
            for (int d = 0; d < detections.Count; d++)
            {
                var det = detections[d];
                if (det.sourceTrackId != sensor.trackId || det.detectorType != sensor.detectorType)
                    continue;
                anyHit = true;
                string hitKey = key + "->" + det.targetTrackId;
                float hitWidth = Mathf.Max(0.002f, TrailWidthMeters * Mathf.Max(unitsPerMeter.x, unitsPerMeter.z));
                var beam = GetLine(hits, hitKey, hitWidth, Color.white);
                beam.widthMultiplier = hitWidth;
                beam.positionCount = 2;
                beam.SetPosition(0, apex);
                Vector3 targetMeters = RadarDetectionSystem.GeoToLocalMeters(det.targetLng, det.targetLat, det.targetAlt, originLat, originLng);
                beam.SetPosition(1, ToVisual(targetMeters));
                beam.startColor = beam.endColor = color;
                beam.gameObject.SetActive(true);
                usedHits.Add(hitKey);
            }
            var filter = GetVolume(key);
            BuildVolume(filter.mesh, sensor, originMeters);
            var renderer = filter.GetComponent<MeshRenderer>();
            ApplyRadarVolumeMaterial(renderer, color, anyHit);
            filter.gameObject.SetActive(true);
            DrawCapOutline(key, sensor, originMeters, color, usedEdges);
            usedVolumes.Add(key);
        }
        HideUnused(volumes, usedVolumes);
        HideUnused(volumeEdges, usedEdges);
        HideUnused(hits, usedHits);
    }

    private void BuildVolume(Mesh mesh, RadarDetectionSystem.SensorInfo sensor, Vector3 origin)
    {
        if (sensor.spherical)
        {
            BuildSphereVolume(mesh, origin, sensor.maxRangeMeters);
            return;
        }
        const int seg = 28;
        const int rings = 6;
        RadarDetectionSystem.ConeBasis(sensor, out Vector3 axis, out Vector3 side, out Vector3 up);
        float range = sensor.maxRangeMeters;
        var verts = new List<Vector3>(2 + rings * seg) { ToVisual(origin) };
        var tris = new List<int>();
        int tip = verts.Count;
        verts.Add(ToVisual(origin + axis * range));
        for (int r = 1; r <= rings; r++)
        {
            for (int i = 0; i < seg; i++)
            {
                float az = Mathf.PI * 2f * i / seg;
                float polarMax = RadarDetectionSystem.EllipticalHalfRad(sensor, az);
                float polar = polarMax * r / rings;
                verts.Add(ToVisual(origin + SphereOffset(axis, side, up, range, polar, az)));
            }
        }

        int Ring(int r) { return 2 + (r - 1) * seg; }
        int outer = Ring(rings);
        int coneApex = verts.Count;
        for (int i = 0; i < seg; i++)
        {
            float ang = Mathf.PI * 2f * (i + 0.5f) / seg;
            Vector3 outward = ConeOutward(axis, side, up, ang, RadarDetectionSystem.EllipticalHalfRad(sensor, ang));
            int rimA = verts.Count;
            verts.Add(verts[outer + i]);
            int rimB = verts.Count;
            verts.Add(verts[outer + (i + 1) % seg]);
            int apexI = verts.Count;
            verts.Add(verts[0]);
            Vector3 e1 = verts[rimA] - verts[apexI];
            Vector3 e2 = verts[rimB] - verts[apexI];
            if (Vector3.Dot(Vector3.Cross(e1, e2), outward) < 0f)
            {
                tris.Add(apexI); tris.Add(rimB); tris.Add(rimA);
            }
            else
            {
                tris.Add(apexI); tris.Add(rimA); tris.Add(rimB);
            }
        }

        int first = Ring(1);
        for (int i = 0; i < seg; i++)
        {
            int a = tip;
            int b = first + (i + 1) % seg;
            int c = first + i;
            AddOutward(tris, verts, a, b, c, verts[b] - verts[0]);
        }
        for (int r = 1; r < rings; r++)
        {
            int inner = Ring(r);
            int next = Ring(r + 1);
            for (int i = 0; i < seg; i++)
            {
                int i1 = (i + 1) % seg;
                Vector3 outward = verts[next + i] - verts[0];
                AddOutward(tris, verts, inner + i, next + i, next + i1, outward);
                AddOutward(tris, verts, inner + i, next + i1, inner + i1, outward);
            }
        }

        var colors = new List<Color>(verts.Count);
        var normals = new List<Vector3>(verts.Count);
        Vector3 apex = verts[0];
        float maxDist = 0.001f;
        for (int i = 0; i < verts.Count; i++)
            maxDist = Mathf.Max(maxDist, (verts[i] - apex).magnitude);
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 radial = verts[i] - apex;
            float t = Mathf.Clamp01(radial.magnitude / maxDist);
            colors.Add(new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0.45f, t)));
            normals.Add(radial.sqrMagnitude > 1e-6f ? radial.normalized : axis);
        }
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        for (int i = 0; i < seg; i++)
        {
            float ang = Mathf.PI * 2f * (i + 0.5f) / seg;
            Vector3 outward = ConeOutward(axis, side, up, ang, RadarDetectionSystem.EllipticalHalfRad(sensor, ang));
            int baseI = coneApex + i * 3;
            if (baseI + 2 < normals.Count)
            {
                normals[baseI] = outward;
                normals[baseI + 1] = outward;
                normals[baseI + 2] = outward;
            }
        }
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();
    }

    private void BuildSphereVolume(Mesh mesh, Vector3 origin, float range)
    {
        const int seg = 28;
        const int rings = 12;
        Vector3 axis = Vector3.up;
        Vector3 side = Vector3.right;
        Vector3 up = Vector3.forward;
        var verts = new List<Vector3>(2 + (rings - 1) * seg);
        var tris = new List<int>();
        verts.Add(ToVisual(origin + axis * range));
        int north = 0;
        for (int r = 1; r < rings; r++)
        {
            float polar = Mathf.PI * r / rings;
            for (int i = 0; i < seg; i++)
            {
                float az = Mathf.PI * 2f * i / seg;
                verts.Add(ToVisual(origin + SphereOffset(axis, side, up, range, polar, az)));
            }
        }
        int south = verts.Count;
        verts.Add(ToVisual(origin - axis * range));

        int Ring(int r) { return 1 + (r - 1) * seg; }
        for (int i = 0; i < seg; i++)
        {
            int b = Ring(1) + i;
            int c = Ring(1) + (i + 1) % seg;
            AddOutward(tris, verts, north, c, b, verts[b] - ToVisual(origin));
        }
        for (int r = 1; r < rings - 1; r++)
        {
            int inner = Ring(r);
            int next = Ring(r + 1);
            for (int i = 0; i < seg; i++)
            {
                int i1 = (i + 1) % seg;
                Vector3 outward = verts[next + i] - ToVisual(origin);
                AddOutward(tris, verts, inner + i, next + i, next + i1, outward);
                AddOutward(tris, verts, inner + i, next + i1, inner + i1, outward);
            }
        }
        int last = Ring(rings - 1);
        for (int i = 0; i < seg; i++)
        {
            int a = last + i;
            int b = last + (i + 1) % seg;
            AddOutward(tris, verts, a, b, south, verts[a] - ToVisual(origin));
        }

        Vector3 center = ToVisual(origin);
        var colors = new List<Color>(verts.Count);
        var normals = new List<Vector3>(verts.Count);
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 radial = verts[i] - center;
            float t = Mathf.Clamp01(radial.magnitude / Mathf.Max(0.001f, range * Mathf.Max(unitsPerMeter.x, 0.0001f)));
            colors.Add(new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0.45f, t)));
            normals.Add(radial.sqrMagnitude > 1e-6f ? radial.normalized : axis);
        }
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();
    }

    private static void AddOutward(List<int> tris, List<Vector3> verts, int a, int b, int c, Vector3 outward)
    {
        Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
        if (Vector3.Dot(n, outward) < 0f)
        {
            tris.Add(a); tris.Add(c); tris.Add(b);
        }
        else
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
        }
    }

    private static Vector3 ConeOutward(Vector3 axis, Vector3 side, Vector3 up, float az, float half)
    {
        Vector3 radial = side * Mathf.Cos(az) + up * Mathf.Sin(az);
        Vector3 surface = axis * Mathf.Cos(half) + radial * Mathf.Sin(half);
        Vector3 outward = surface * Mathf.Cos(half) - axis;
        return outward.sqrMagnitude > 1e-6f ? outward.normalized : radial;
    }

    private void DrawCapOutline(string key, RadarDetectionSystem.SensorInfo sensor, Vector3 originMeters, Color color, HashSet<string> used)
    {
        if (sensor.spherical)
        {
            DrawSphereOutline(key, originMeters, sensor.maxRangeMeters, color, used);
            return;
        }
        RadarDetectionSystem.ConeBasis(sensor, out Vector3 axis, out Vector3 side, out Vector3 up);
        float range = sensor.maxRangeMeters;
        Color edge = new Color(
            Mathf.Min(1f, color.r + 0.35f),
            Mathf.Min(1f, color.g + 0.35f),
            Mathf.Min(1f, color.b + 0.35f),
            0.95f);

        const int seg = 48;
        var rim = new Vector3[seg + 1];
        for (int i = 0; i <= seg; i++)
        {
            float ang = Mathf.PI * 2f * i / seg;
            float polar = RadarDetectionSystem.EllipticalHalfRad(sensor, ang);
            rim[i] = ToVisual(originMeters + SphereOffset(axis, side, up, range, polar, ang));
        }
        StrokeLoop(key + "|cap", rim, edge, used);
        float sideHalf = Mathf.Max(
            RadarDetectionSystem.EllipticalHalfRad(sensor, 0f),
            RadarDetectionSystem.EllipticalHalfRad(sensor, Mathf.PI * 0.5f));
        StrokeArc(key + "|capArc", SphereSilhouette(originMeters, axis, range, sideHalf), edge, used);
    }

    private void DrawSphereOutline(string key, Vector3 originMeters, float range, Color color, HashSet<string> used)
    {
        Color edge = new Color(
            Mathf.Min(1f, color.r + 0.35f),
            Mathf.Min(1f, color.g + 0.35f),
            Mathf.Min(1f, color.b + 0.35f),
            0.95f);
        const int seg = 48;
        var equator = new Vector3[seg + 1];
        for (int i = 0; i <= seg; i++)
        {
            float ang = Mathf.PI * 2f * i / seg;
            equator[i] = ToVisual(originMeters + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * range);
        }
        StrokeLoop(key + "|equator", equator, edge, used);
        StrokeArc(key + "|sphereArc", SphereSilhouette(originMeters, Vector3.up, range, Mathf.PI), edge, used);
    }

    private void StrokeLoop(string lineKey, Vector3[] points, Color edge, HashSet<string> used)
    {
        if (points == null || points.Length < 2)
            return;
        var lr = GetLine(volumeEdges, lineKey, 1f, edge);
        lr.positionCount = points.Length;
        lr.SetPositions(points);
        lr.widthMultiplier = PixelWidth(points[0], RadarEdgePixels);
        lr.startColor = lr.endColor = edge;
        lr.numCapVertices = 2;
        lr.gameObject.SetActive(true);
        used.Add(lineKey);
    }

    private void StrokeArc(string lineKey, List<Vector3> points, Color edge, HashSet<string> used)
    {
        if (points == null || points.Count < 2)
            return;
        var lr = GetLine(volumeEdges, lineKey, 1f, edge);
        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());
        lr.widthMultiplier = PixelWidth(points[0], RadarEdgePixels);
        lr.startColor = lr.endColor = edge;
        lr.numCapVertices = 2;
        lr.gameObject.SetActive(true);
        used.Add(lineKey);
    }

    private float PixelWidth(Vector3 worldPoint, float pixels)
    {
        if (flightCamera == null)
            return 0.02f;
        float dist = Vector3.Distance(flightCamera.transform.position, worldPoint);
        float height = Mathf.Max(1f, flightCamera.pixelHeight);
        return pixels * 2f * dist * Mathf.Tan(flightCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / height;
    }

    private List<Vector3> SphereSilhouette(Vector3 originMeters, Vector3 axis, float range, float half)
    {
        var arc = new List<Vector3>();
        if (flightCamera == null)
            return arc;
        Vector3 apex = ToVisual(originMeters);
        Vector3 axisV = (ToVisual(originMeters + axis) - apex).normalized;
        float rangeV = (ToVisual(originMeters + axis * range) - apex).magnitude;
        Vector3 view = flightCamera.transform.position - apex;
        float camDist = view.magnitude;
        if (camDist <= rangeV + 0.05f || rangeV < 0.01f)
            return arc;
        view /= camDist;
        float hDist = rangeV * rangeV / camDist;
        float hRad = rangeV * Mathf.Sqrt(Mathf.Max(0f, 1f - (rangeV / camDist) * (rangeV / camDist)));
        Vector3 center = apex + view * hDist;
        Vector3 side = Vector3.Cross(view, axisV);
        if (side.sqrMagnitude < 1e-6f)
            side = Vector3.Cross(view, Vector3.up);
        side.Normalize();
        Vector3 up = Vector3.Cross(side, view);
        const int seg = 72;
        var on = new bool[seg];
        var sample = new Vector3[seg];
        float limit = half * Mathf.Rad2Deg + 0.4f;
        for (int i = 0; i < seg; i++)
        {
            float ang = Mathf.PI * 2f * i / seg;
            sample[i] = center + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * hRad;
            on[i] = Vector3.Angle(sample[i] - apex, axisV) <= limit;
        }
        int start = -1;
        for (int i = 0; i < seg; i++)
        {
            if (on[i] && !on[(i + seg - 1) % seg])
            {
                start = i;
                break;
            }
        }
        if (start < 0 && on[0])
            start = 0;
        if (start < 0)
            return arc;
        for (int n = 0; n < seg; n++)
        {
            int i = (start + n) % seg;
            if (!on[i])
                break;
            arc.Add(sample[i]);
        }
        return arc;
    }

    private static void ApplyRadarVolumeMaterial(MeshRenderer renderer, Color color, bool anyHit)
    {
        if (renderer == null)
            return;
        var mat = renderer.sharedMaterial;
        var shader = Shader.Find("HK132/RadarVolume");
        if (mat == null || (shader != null && mat.shader != shader))
        {
            mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            renderer.sharedMaterial = mat;
        }
        float alpha = anyHit ? 0.18f : 0.12f;
        Color fill = new Color(color.r, color.g, color.b, alpha);
        Color edge = new Color(
            Mathf.Min(1f, color.r + 0.2f),
            Mathf.Min(1f, color.g + 0.2f),
            Mathf.Min(1f, color.b + 0.2f),
            0.85f);
        mat.SetColor("_Color", fill);
        mat.SetColor("_EdgeColor", edge);
        mat.SetFloat("_EdgeStart", 0.28f);
        mat.SetFloat("_EdgeEnd", 0.02f);
        mat.color = fill;
    }

    private static Vector3 SphereOffset(Vector3 axis, Vector3 side, Vector3 up, float range, float polar, float az)
    {
        float along = Mathf.Cos(polar) * range;
        float ring = Mathf.Sin(polar) * range;
        return axis * along + (side * Mathf.Cos(az) + up * Mathf.Sin(az)) * ring;
    }

    private MeshFilter GetVolume(string key)
    {
        if (volumes.TryGetValue(key, out var existing) && existing != null)
            return existing;
        var go = new GameObject("ScanVolume_" + key);
        go.transform.SetParent(root, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.mesh = new Mesh { name = key };
        var renderer = go.AddComponent<MeshRenderer>();
        var shader = Shader.Find("HK132/RadarVolume");
        renderer.sharedMaterial = shader != null
            ? new Material(shader)
            : MakeTransparent(new Color(1f, 0.82f, 0.12f, 0.08f));
        renderer.sharedMaterial.SetColor("_Color", new Color(1f, 0.82f, 0.12f, 0.06f));
        renderer.sharedMaterial.SetColor("_EdgeColor", new Color(1f, 0.9f, 0.35f, 0.45f));
        SetLayer(go);
        volumes[key] = filter;
        return filter;
    }

    private static Material MakeTransparent(Color color)
    {
        var shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.color = color;
        mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        return mat;
    }

    private LineRenderer GetLine(Dictionary<string, LineRenderer> map, string key, float width, Color color)
    {
        if (map.TryGetValue(key, out var existing) && existing != null)
            return existing;
        var go = new GameObject("FlightLine_" + key);
        go.transform.SetParent(root, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;
        lr.numCapVertices = 2;
        SetLayer(go);
        map[key] = lr;
        return lr;
    }

    private void DrawVolumeEdges(string key, RadarDetectionSystem.SensorInfo sensor, Vector3 originMeters, Color color, HashSet<string> used)
    {
        if (flightCamera == null)
            return;
        RadarDetectionSystem.ConeBasis(sensor, out Vector3 axisM, out Vector3 sideM, out Vector3 upM);
        float half = Mathf.Max(0.5f, RadarDetectionSystem.ConeHalfAngleDeg(sensor)) * Mathf.Deg2Rad;
        float rangeM = sensor.maxRangeMeters;
        Vector3 apex = ToVisual(originMeters);
        Vector3 axis = (ToVisual(originMeters + axisM) - apex).normalized;
        Vector3 side = (ToVisual(originMeters + sideM) - apex).normalized;
        Vector3 up = (ToVisual(originMeters + upM) - apex).normalized;
        float range = (ToVisual(originMeters + axisM * rangeM) - apex).magnitude;
        if (range < 0.01f)
            return;

        Vector3 cam = flightCamera.transform.position;
        if (!TryConeSilhouette(apex, axis, side, up, range, half, cam, out Vector3 left, out Vector3 right))
            return;

        Color far = new Color(color.r, color.g, color.b, 0.28f);
        string leftKey = key + "|edgeL";
        string rightKey = key + "|edgeR";
        string capKey = key + "|cap";
        DrawGradientLine(leftKey, apex, left, color, far, 2f);
        DrawGradientLine(rightKey, apex, right, color, far, 2f);
        used.Add(leftKey);
        used.Add(rightKey);

        var cap = BuildCapSilhouette(apex, axis, range, half, cam, left, right);
        if (cap.Count >= 2)
        {
            var lr = GetLine(volumeEdges, capKey, 1.8f, far);
            lr.positionCount = cap.Count;
            lr.SetPositions(cap.ToArray());
            lr.startColor = lr.endColor = far;
            lr.colorGradient = FlatGradient(far);
            lr.gameObject.SetActive(true);
            used.Add(capKey);
        }
    }

    private void DrawGradientLine(string key, Vector3 from, Vector3 to, Color near, Color far, float width)
    {
        var lr = GetLine(volumeEdges, key, width, near);
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.colorGradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(near, 0f),
                new GradientColorKey(far, 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(near.a, 0f),
                new GradientAlphaKey(far.a, 1f)
            }
        };
        lr.gameObject.SetActive(true);
    }

    private static Gradient FlatGradient(Color color)
    {
        return new Gradient
        {
            colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) }
        };
    }

    private static bool TryConeSilhouette(Vector3 apex, Vector3 axis, Vector3 side, Vector3 up, float range, float half, Vector3 cam, out Vector3 left, out Vector3 right)
    {
        Vector3 center = apex + axis * (Mathf.Cos(half) * range);
        float radius = Mathf.Sin(half) * range;
        Vector3 toCam = cam - center;
        Vector3 inPlane = toCam - axis * Vector3.Dot(toCam, axis);
        if (inPlane.sqrMagnitude < 1e-6f)
            inPlane = side;
        float dist = inPlane.magnitude;
        Vector3 dir = inPlane / Mathf.Max(1e-4f, dist);
        Vector3 perp = Vector3.Cross(axis, dir).normalized;
        float cosB = Mathf.Clamp(radius / Mathf.Max(dist, radius + 0.01f), -1f, 1f);
        float sinB = Mathf.Sqrt(Mathf.Max(0f, 1f - cosB * cosB));
        left = center + (dir * cosB + perp * sinB) * radius;
        right = center + (dir * cosB - perp * sinB) * radius;
        return true;
    }

    private static List<Vector3> BuildCapSilhouette(Vector3 apex, Vector3 axis, float range, float half, Vector3 cam, Vector3 left, Vector3 right)
    {
        var arc = new List<Vector3>();
        Vector3 view = cam - apex;
        float camDist = view.magnitude;
        if (camDist > range + 0.05f)
        {
            view /= camDist;
            float hDist = range * range / camDist;
            float hRad = range * Mathf.Sqrt(Mathf.Max(0f, 1f - (range / camDist) * (range / camDist)));
            Vector3 hCenter = apex + view * hDist;
            Vector3 hSide = Vector3.Cross(view, axis);
            if (hSide.sqrMagnitude < 1e-6f)
                hSide = Vector3.Cross(view, Vector3.up);
            hSide.Normalize();
            Vector3 hUp = Vector3.Cross(hSide, view);
            const int seg = 64;
            var on = new bool[seg];
            var sample = new Vector3[seg];
            for (int i = 0; i < seg; i++)
            {
                float ang = Mathf.PI * 2f * i / seg;
                sample[i] = hCenter + (hSide * Mathf.Cos(ang) + hUp * Mathf.Sin(ang)) * hRad;
                on[i] = Vector3.Angle(sample[i] - apex, axis) <= half * Mathf.Rad2Deg + 0.5f;
            }
            int start = -1;
            for (int i = 0; i < seg; i++)
            {
                if (on[i] && !on[(i + seg - 1) % seg])
                {
                    start = i;
                    break;
                }
            }
            if (start < 0 && on[0])
                start = 0;
            if (start >= 0)
            {
                for (int n = 0; n < seg; n++)
                {
                    int i = (start + n) % seg;
                    if (!on[i])
                        break;
                    arc.Add(sample[i]);
                }
            }
        }

        if (arc.Count >= 3)
            return arc;

        const int rim = 28;
        var circle = new List<Vector3>(rim + 1);
        Vector3 side = (left - (apex + axis * Vector3.Dot(left - apex, axis)));
        if (side.sqrMagnitude < 1e-6f)
            side = Vector3.Cross(axis, Vector3.up);
        side.Normalize();
        Vector3 up = Vector3.Cross(side, axis).normalized;
        float ring = Mathf.Sin(half) * range;
        float along = Mathf.Cos(half) * range;
        for (int i = 0; i <= rim; i++)
        {
            float ang = Mathf.PI * 2f * i / rim;
            circle.Add(apex + axis * along + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * ring);
        }
        return circle;
    }

    private Vector3 lastFocus;
    private float lastHeading;
    private float lastSpan = 80f;

    private void Navigate()
    {
        if (flightCamera == null || viewImage == null || !viewImage.gameObject.activeInHierarchy)
            return;
        if (IsTyping())
            return;

        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool over = IsPointerOverView();
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");
        float scroll = Input.mouseScrollDelta.y;

        if (Input.GetMouseButtonDown(1))
        {
            if (!over)
                return;
            userHasControl = true;
            if (alt)
                zoomDragging = true;
            else
            {
                flying = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SyncAnglesFromCamera();
            }
        }
        if (Input.GetMouseButtonDown(2) && over)
        {
            userHasControl = true;
            panning = true;
        }
        if (Input.GetMouseButtonDown(0) && alt && over)
        {
            userHasControl = true;
            orbiting = true;
            orbitPivot = flightCamera.transform.position + flightCamera.transform.forward * orbitDistance;
        }
        if (Input.GetMouseButtonUp(1))
        {
            flying = false;
            zoomDragging = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Input.GetMouseButtonUp(2))
            panning = false;
        if (Input.GetMouseButtonUp(0))
            orbiting = false;

        if (over && Input.GetKeyDown(KeyCode.F))
        {
            FrameCamera(lastFocus, lastHeading, lastSpan, true);
            return;
        }

        if (flying)
        {
            yaw += mx * 2.2f;
            pitch = Mathf.Clamp(pitch - my * 2.2f, -89f, 89f);
            flightCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (Mathf.Abs(scroll) > 0.01f)
                flySpeed = Mathf.Clamp(flySpeed * (scroll > 0f ? 1.12f : 0.88f), 0.4f, 800f);
            Vector3 move = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
            if (Input.GetKey(KeyCode.E))
                move.y += 1f;
            if (Input.GetKey(KeyCode.Q))
                move.y -= 1f;
            float boost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 3f : 1f;
            flightCamera.transform.position += flightCamera.transform.rotation * move * flySpeed * boost * Time.unscaledDeltaTime;
            orbitDistance = Mathf.Max(10f, Vector3.Distance(flightCamera.transform.position, orbitPivot));
            return;
        }

        if (orbiting)
        {
            yaw += mx * 3f;
            pitch = Mathf.Clamp(pitch - my * 3f, -89f, 89f);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            flightCamera.transform.rotation = rot;
            flightCamera.transform.position = orbitPivot - rot * Vector3.forward * orbitDistance;
            return;
        }

        if (panning)
        {
            float scale = flySpeed * 0.02f;
            Vector3 delta = (-flightCamera.transform.right * mx - flightCamera.transform.up * my) * scale;
            flightCamera.transform.position += delta;
            orbitPivot += delta;
            return;
        }

        if (zoomDragging)
        {
            Dolly(-my * flySpeed * 0.08f);
            return;
        }

        if (over && Mathf.Abs(scroll) > 0.01f)
            Dolly(scroll * flySpeed * 0.35f);
    }

    private void Dolly(float distance)
    {
        flightCamera.transform.position += flightCamera.transform.forward * distance;
        orbitDistance = Mathf.Max(5f, orbitDistance - distance);
        userHasControl = true;
    }

    private void SyncAnglesFromCamera()
    {
        Vector3 euler = flightCamera.transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x > 180f ? euler.x - 360f : euler.x;
    }

    private bool IsPointerOverView()
    {
        if (viewImage == null)
            return false;
        var canvas = viewImage.canvas;
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(viewImage.rectTransform, Input.mousePosition, eventCamera);
    }

    private static bool IsTyping()
    {
        if (EventSystem.current == null)
            return false;
        var selected = EventSystem.current.currentSelectedGameObject;
        return selected != null && selected.GetComponent<InputField>() != null;
    }

    private bool placedDefaultCamera;
    private static readonly Vector3 DefaultCameraPosition = new Vector3(-16.30917f, 18.95884f, -16.00072f);

    private void ApplyDefaultCameraIfNeeded()
    {
        if (placedDefaultCamera || flightCamera == null)
            return;
        placedDefaultCamera = true;
        userHasControl = true;
        flightCamera.transform.position = DefaultCameraPosition;
        flightCamera.transform.LookAt(Vector3.zero);
        flySpeed = 6f;
        orbitPivot = Vector3.zero;
        orbitDistance = Mathf.Max(5f, DefaultCameraPosition.magnitude);
        SyncAnglesFromCamera();
    }

    private void FrameCamera(Vector3 focus, float heading, float span, bool force = false)
    {
        lastFocus = focus;
        lastHeading = heading;
        lastSpan = span;
        if (flightCamera == null || (userHasControl && !force))
            return;
        if (force)
            userHasControl = false;
        float dist = Mathf.Clamp(span * 1.6f, 80f, 120000f);
        flySpeed = Mathf.Max(2f, dist * 0.04f);
        orbitDistance = dist;
        orbitPivot = focus;
        float rad = heading * Mathf.Deg2Rad;
        Vector3 forward = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        flightCamera.farClipPlane = Mathf.Max(250000f, dist * 3f);
        flightCamera.transform.position = focus - forward * dist + Vector3.up * dist * 0.38f;
        flightCamera.transform.LookAt(focus + Vector3.up * dist * 0.05f);
        SyncAnglesFromCamera();
    }

    private static void NormalizeLength(GameObject go, float targetLength)
    {
        var filters = go.GetComponentsInChildren<MeshFilter>(true);
        float length = 0.01f;
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh == null)
                continue;
            Vector3 size = Vector3.Scale(filters[i].sharedMesh.bounds.size, Abs(filters[i].transform.lossyScale));
            length = Mathf.Max(length, size.x, size.y, size.z);
        }
        float factor = targetLength / length;
        go.transform.localScale *= factor;
    }

    private static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private float FormationSpan(List<FlightDataStreamReceiver.TrackPosition> positions)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < positions.Count; i++)
        {
            var pos = positions[i];
            if (pos == null)
                continue;
            Vector3 world = ToVisual(pos.lng, pos.lat, pos.alt);
            min = Vector3.Min(min, world);
            max = Vector3.Max(max, world);
        }
        return Mathf.Max(40f, Vector3.Distance(min, max));
    }

    private static void HideUnused<T>(Dictionary<string, T> map, HashSet<string> used) where T : Component
    {
        foreach (var kv in map)
        {
            if (kv.Value != null && !used.Contains(kv.Key))
                kv.Value.gameObject.SetActive(false);
        }
    }

    private static void SetLayer(GameObject go)
    {
        var all = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            all[i].gameObject.layer = FlightLayer;
    }
}
