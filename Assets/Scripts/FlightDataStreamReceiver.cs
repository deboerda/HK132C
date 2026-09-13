using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class FlightDataStreamReceiver : MonoBehaviour
{
    [Serializable]
    public class TrackPosition
    {
        public string trackId;
        public string planId;
        public string flightTime;
        public float lng;
        public float lat;
        public float alt;
        public float heading;
        public float pitch;
        public float roll;
    }

    [Serializable]
    public class TrackLine
    {
        public string trackId;
        public string flightTime;
        public readonly List<TrackPosition> positions = new List<TrackPosition>();
        public readonly List<TrackPlanLine> plans = new List<TrackPlanLine>();
    }

    [Serializable]
    public class TrackPlanLine
    {
        public string trackId;
        public string planId;
        public string flightTime;
        public readonly List<TrackPosition> positions = new List<TrackPosition>();
    }

    [Serializable]
    public class TrackTimetableState
    {
        public string flightTime;
        public string trackId;
        public string state;
        public float battery;
        public float speed;
        public string alarm;
        public string detectorType;
        public string rangeType;
        public float detectRange;
        public float yawAngle;
        public float pitchAngle;
        public string centerPoint;
        public string radarStatus;
    }

    [Serializable]
    public class CycleTableRecord
    {
        public string flightTime;
        public string trackId;
        public int cycleId;
        public float lng;
        public float lat;
        public float alt;
        public float heading;
        public float pitch;
        public float roll;
        public string radarStatus;
    }

    [Serializable]
    public class TaskTimeInfo
    {
        public string taskId;
        public string startTime;
        public string endTime;
        public float durationSeconds;
    }

    public static FlightDataStreamReceiver Instance { get; private set; }
    public static event Action<TaskTimeInfo> TaskTimeInfoReceived;
    public static event Action<string, float> FlightTimeReceived;
    public static event Action DataChannelConnected;

    [Header("Data Server")]
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private int port = 8888;
    [SerializeField] private bool connectOnStart = true;
    [SerializeField] private bool listenOnAllInterfaces = true;
    [SerializeField] private int listenBacklog = 1;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;
    [SerializeField] private bool logIncomingMessageTypes = false;

    private TcpClient client;
    private TcpListener listener;
    private NetworkStream stream;
    private Thread listenThread;
    private Coroutine listenRetryRoutine;
    private bool warnedPortInUse;
    private volatile bool isRunning;
    private readonly object connectionLock = new object();
    private UnityMainThreadDispatcher mainThreadDispatcher;
    private bool isDuplicateInstance;
    private readonly object dataLock = new object();

    private readonly Dictionary<string, TrackPosition> latestPositionsByTrack = new Dictionary<string, TrackPosition>();
    private readonly Dictionary<string, TrackLine> latestTrackLinesByTrack = new Dictionary<string, TrackLine>();
    private readonly Dictionary<string, TrackTimetableState> latestStatesByTrack = new Dictionary<string, TrackTimetableState>();
    private readonly List<CycleTableRecord> latestCycleRecords = new List<CycleTableRecord>();
    private readonly HashSet<string> latestCycleUpdatedTrackIds = new HashSet<string>();
    private string latestCycleFlightTime;
    private int trackPositionsResponseCount;
    private bool lastTrackPositionsHadRenderableRoute;
    private string lastTrackPositionsSummary = "";
    private bool warnedNonRenderableTrackPositions;
    private readonly Dictionary<string, float> previousHeadingByTrack = new Dictionary<string, float>();
    private readonly Dictionary<string, DateTime> previousTimeByTrack = new Dictionary<string, DateTime>();
    private readonly Dictionary<string, List<string>> dataFields = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, List<string>> DataFields
    {
        get { lock (dataLock) { return new Dictionary<string, List<string>>(dataFields); } }
    }
    private string primaryTrackId;
    private TaskTimeInfo latestTaskTimeInfo;
    private DateTime? taskStartTime;

    public string Host => host;
    public int Port => port;
    public bool IsConnected => client != null && client.Connected && stream != null;

    public TrackPosition PrimaryPosition { get { lock (dataLock) { return GetPrimaryPositionUnsafe(); } } }
    public TrackTimetableState PrimaryTimetableState { get { lock (dataLock) { return GetPrimaryStateUnsafe(); } } }
    public float PrimaryAltitudeMeters { get { lock (dataLock) { return GetPrimaryPositionUnsafe()?.alt ?? 0f; } } }
    public float PrimaryAirspeedKnots { get { lock (dataLock) { return GetPrimaryStateUnsafe()?.speed ?? 0f; } } }
    public float PrimaryPitchDeg { get { lock (dataLock) { return GetPrimaryPositionUnsafe()?.pitch ?? 0f; } } }
    public float PrimaryRollDeg { get { lock (dataLock) { return GetPrimaryPositionUnsafe()?.roll ?? 0f; } } }
    public float PrimaryTurnRateDegPerSec { get { lock (dataLock) { return GetPrimaryTurnRateUnsafe(); } } }
    public float PrimarySlipSkidDeg { get { lock (dataLock) { return GetPrimaryPositionUnsafe()?.roll ?? 0f; } } }
    public string PrimaryTrackId { get { lock (dataLock) { return primaryTrackId; } } }
    public TaskTimeInfo LatestTaskTimeInfo { get { lock (dataLock) { return latestTaskTimeInfo; } } }
    public int TrackPositionsResponseCount { get { lock (dataLock) { return trackPositionsResponseCount; } } }
    public bool LastTrackPositionsHadRenderableRoute { get { lock (dataLock) { return lastTrackPositionsHadRenderableRoute; } } }
    public string LastTrackPositionsSummary { get { lock (dataLock) { return lastTrackPositionsSummary; } } }
    public List<TrackPosition> SnapshotPositions
    {
        get
        {
            lock (dataLock)
            {
                return new List<TrackPosition>(latestPositionsByTrack.Values);
            }
        }
    }
    public List<TrackTimetableState> SnapshotStates
    {
        get
        {
            lock (dataLock)
            {
                return new List<TrackTimetableState>(latestStatesByTrack.Values);
            }
        }
    }
    public List<CycleTableRecord> SnapshotCycleRecords
    {
        get
        {
            lock (dataLock)
            {
                return new List<CycleTableRecord>(latestCycleRecords);
            }
        }
    }

    private void Awake()
    {
        mainThreadDispatcher = UnityMainThreadDispatcher.Instance();

        if (Instance == null)
        {
            Instance = this;
            Application.wantsToQuit -= OnWantsToQuit;
            Application.wantsToQuit += OnWantsToQuit;
        }
        else if (Instance != this)
        {
            isDuplicateInstance = true;
            DisconnectImmediate();
            Destroy(gameObject);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        if (Instance != null)
        {
            Instance.DisconnectImmediate();
        }

        Instance = null;
        TaskTimeInfoReceived = null;
        FlightTimeReceived = null;
        DataChannelConnected = null;
    }

    public static void DisconnectAllReceivers()
    {
        if (Instance != null)
        {
            Instance.DisconnectImmediate();
        }

        var receivers = Resources.FindObjectsOfTypeAll<FlightDataStreamReceiver>();
        for (int i = 0; i < receivers.Length; i++)
        {
            var receiver = receivers[i];
            if (receiver != null)
            {
                receiver.DisconnectImmediate();
            }
        }
    }
    public bool HasPlannedRouteLines
    {
        get
        {
            lock (dataLock)
            {
                foreach (var kv in latestTrackLinesByTrack)
                {
                    TrackLine line = kv.Value;
                    if (line == null)
                    {
                        continue;
                    }

                    if (line.positions.Count >= 2)
                    {
                        return true;
                    }

                    for (int i = 0; i < line.plans.Count; i++)
                    {
                        if (line.plans[i] != null && line.plans[i].positions.Count >= 2)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }
    }

    private void Start()
    {
        if (isDuplicateInstance)
        {
            return;
        }

        if (connectOnStart)
        {
            if (!Connect())
            {
                listenRetryRoutine = StartCoroutine(RetryListenRoutine());
            }
        }
    }

    public bool Connect()
    {
        if (isDuplicateInstance)
        {
            return false;
        }

        lock (connectionLock)
        {
            if (listener != null && isRunning)
            {
                return true;
            }

            if (listenThread != null && listenThread.IsAlive)
            {
                LogWarning($"Listen thread is still stopping on port {port}; skip duplicate Connect.");
                return false;
            }

            try
            {
                isRunning = true;

                var bindAddress = listenOnAllInterfaces ? IPAddress.Any : IPAddress.Parse(host);
                StartListenerUnsafe(bindAddress);
                warnedPortInUse = false;

                Log($"Listening on {bindAddress}:{port}");
                return true;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                CleanupFailedConnectUnsafe();
                if (!warnedPortInUse)
                {
                    LogWarning($"Port {port} is temporarily unavailable. Unity will keep retrying until it can listen.");
                    warnedPortInUse = true;
                }
                return false;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AccessDenied)
            {
                LogWarning($"Listen skipped: access denied while binding {host}:{port}. Check whether the port is reserved or blocked by Windows/network policy.");
                CleanupFailedConnectUnsafe();
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Listen failed: {ex.Message}");
                CleanupFailedConnectUnsafe();
                return false;
            }
        }
    }

    private void StartListenerUnsafe(IPAddress bindAddress)
    {
        listener = new TcpListener(bindAddress, port);
        // A data client must always reach the current Unity play session. Allowing address
        // reuse can leave an old Unity process listening on the same port and receive data.
        listener.Server.ExclusiveAddressUse = true;

        listener.Start(Mathf.Max(1, listenBacklog));

        listenThread = new Thread(ListenLoop);
        listenThread.IsBackground = true;
        listenThread.Start();
    }

    private IEnumerator RetryListenRoutine()
    {
        while (!isDuplicateInstance && !isRunning)
        {
            yield return new WaitForSecondsRealtime(2f);
            if (Connect())
            {
                listenRetryRoutine = null;
                yield break;
            }
        }

        listenRetryRoutine = null;
    }

    public List<TrackLine> SnapshotTrackLines
    {
        get
        {
            lock (dataLock)
            {
                var result = new List<TrackLine>(latestTrackLinesByTrack.Count);
                foreach (var kv in latestTrackLinesByTrack)
                {
                    var source = kv.Value;
                    if (source == null)
                    {
                        continue;
                    }

                    var copy = new TrackLine
                    {
                        trackId = source.trackId,
                        flightTime = source.flightTime,
                    };

                    foreach (var position in source.positions)
                    {
                        copy.positions.Add(CloneTrackPosition(position));
                    }

                    foreach (var plan in source.plans)
                    {
                        if (plan == null)
                        {
                            continue;
                        }

                        var planCopy = new TrackPlanLine
                        {
                            trackId = plan.trackId,
                            planId = plan.planId,
                            flightTime = plan.flightTime,
                        };

                        foreach (var position in plan.positions)
                        {
                            planCopy.positions.Add(CloneTrackPosition(position));
                        }

                        copy.plans.Add(planCopy);
                    }

                    result.Add(copy);
                }

                return result;
            }
        }
    }

    public void Disconnect()
    {
        DisconnectInternal(500, false);
    }

    public void DisconnectImmediate()
    {
        DisconnectInternal(2500, true);
    }

    private void DisconnectInternal(int joinTimeoutMs, bool abortIfStillAlive)
    {
        isRunning = false;

        TcpListener oldListener = null;
        Thread oldListenThread = null;
        lock (connectionLock)
        {
            oldListener = listener;
            oldListenThread = listenThread;
            listener = null;
            listenThread = null;
        }

        CloseClientConnection();

        try
        {
            oldListener?.Stop();
        }
        catch { }

        if (oldListenThread != null && oldListenThread.IsAlive && Thread.CurrentThread != oldListenThread)
        {
            try
            {
                oldListenThread.Join(Mathf.Max(0, joinTimeoutMs));
            }
            catch { }

            if (abortIfStillAlive && oldListenThread.IsAlive)
            {
                try
                {
                    oldListenThread.Interrupt();
                }
                catch { }

                try
                {
                    oldListenThread.Abort();
                }
                catch { }
            }
        }
    }

    private void CleanupFailedConnectUnsafe()
    {
        isRunning = false;
        CloseClientConnection();

        try
        {
            listener?.Stop();
        }
        catch { }

        listener = null;
        listenThread = null;
    }

    private void ListenLoop()
    {
        try
        {
            while (isRunning && listener != null)
            {
                if (!listener.Pending())
                {
                    Thread.Sleep(50);
                    continue;
                }

                TcpClient acceptedClient = listener.AcceptTcpClient();
                acceptedClient.NoDelay = true;
                CloseClientConnection();
                client = acceptedClient;
                stream = client.GetStream();
                Log($"Data client connected from {client.Client.RemoteEndPoint}");
                DispatchDataChannelConnected();
                ReadLoop();
                CloseClientConnection();
            }
        }
        catch (Exception ex)
        {
            if (isRunning)
            {
                LogError($"Listen loop failed: {ex.Message}");
            }
        }
        finally
        {
            CloseClientConnection();
        }
    }

    private void DispatchDataChannelConnected()
    {
        try
        {
            if (mainThreadDispatcher != null)
            {
                mainThreadDispatcher.Enqueue(() => DataChannelConnected?.Invoke());
            }
            else
            {
                LogWarning("Data channel event skipped: main thread dispatcher is not initialized.");
            }
        }
        catch (Exception ex)
        {
            LogError($"Dispatch data channel event failed: {ex.Message}");
        }
    }

    private void CloseClientConnection()
    {
        try
        {
            stream?.Close();
        }
        catch { }
        stream = null;

        try
        {
            client?.Close();
        }
        catch { }
        client = null;
    }

    private void ReadLoop()
    {
        try
        {
            using (var reader = new StreamReader(stream))
            {
                while (isRunning && stream != null && client != null && client.Connected)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    HandleMessage(line);
                }
            }
        }
        catch (Exception ex)
        {
            if (isRunning)
            {
                LogError($"Read loop failed: {ex.Message}");
            }
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            var envelope = JObject.Parse(json);
            string type = envelope.Value<string>("type");
            var data = envelope["data"];

            switch (type)
            {
                case "TRACK_POSITIONS":
                    LogIncomingType("TRACK_POSITIONS");
                    ParseTrackPositions(data, envelope);
                    break;
                case "TIMETABLE_STATE":
                    LogIncomingType("TIMETABLE_STATE");
                    ParseTimetableState(data, envelope);
                    break;
                case "CYCLE_TABLE_DATA":
                    LogIncomingType("CYCLE_TABLE_DATA");
                    ParseCycleTableData(data, envelope);
                    break;
                case "TASK_TIME_INFO":
                    LogIncomingType("TASK_TIME_INFO");
                    ParseTaskTimeInfo(data);
                    break;
                case "DATA_FIELDS":
                    LogIncomingType("DATA_FIELDS");
                    ParseDataFields(data);
                    break;
                default:
                    Log($"Received unknown message type={type}");
                    break;
            }

        }
        catch (Exception ex)
        {
            LogError($"Parse message failed: {ex.Message}");
        }
    }

    private void ParseTrackPositions(JToken data, JToken envelope = null)
    {
        if (data == null)
        {
            return;
        }

        string messageFlightTime = GetFlightTime(data, envelope);
        PublishFlightTime(messageFlightTime);

        int parsedPositionCount = 0;
        int parsedLineCount = 0;
        string frameFlightTime = messageFlightTime;
        bool hasRenderableTrackLine = false;
        var routeSummaryParts = new List<string>();
        var parsedTrackLines = new Dictionary<string, TrackLine>();

        lock (dataLock)
        {
            trackPositionsResponseCount++;

            var tracks = data["tracks"] as JArray;
            if (tracks == null)
            {
                lastTrackPositionsHadRenderableRoute = false;
                lastTrackPositionsSummary = "no tracks array";
                return;
            }

            foreach (var trackToken in tracks)
            {
                string trackId = GetString(trackToken, "trackId");
                if (string.IsNullOrWhiteSpace(trackId))
                {
                    continue;
                }

                var trackLine = new TrackLine
                {
                    trackId = trackId,
                    flightTime = frameFlightTime,
                };

                AppendTrackPositions(trackToken["positions"] as JArray, trackId, null, frameFlightTime, trackLine, null, ref parsedPositionCount);
                if (trackLine.positions.Count >= 2)
                {
                    hasRenderableTrackLine = true;
                }

                var plans = trackToken["plans"] as JArray;
                if (plans != null)
                {
                    int planIndex = 0;
                    foreach (var planToken in plans)
                    {
                        string planId = GetString(planToken, "planId");
                        if (string.IsNullOrWhiteSpace(planId))
                        {
                            planId = $"plan_{planIndex + 1}";
                        }

                        var planLine = new TrackPlanLine
                        {
                            trackId = trackId,
                            planId = planId,
                            flightTime = frameFlightTime,
                        };

                        AppendTrackPositions(planToken["positions"] as JArray, trackId, planId, frameFlightTime, trackLine, planLine, ref parsedPositionCount);
                        routeSummaryParts.Add($"{trackId}/{planId}:{planLine.positions.Count}");
                        if (planLine.positions.Count > 0)
                        {
                            trackLine.plans.Add(planLine);
                        }

                        if (planLine.positions.Count >= 2)
                        {
                            hasRenderableTrackLine = true;
                        }

                        planIndex++;
                    }
                }

                if (trackLine.positions.Count > 0)
                {
                    parsedTrackLines[trackId] = trackLine;
                    parsedLineCount++;
                }
                else
                {
                    routeSummaryParts.Add($"{trackId}:0");
                }
            }

            if (hasRenderableTrackLine)
            {
                latestTrackLinesByTrack.Clear();
                foreach (var kv in parsedTrackLines)
                {
                    latestTrackLinesByTrack[kv.Key] = kv.Value;
                }
            }

            lastTrackPositionsHadRenderableRoute = hasRenderableTrackLine;
            lastTrackPositionsSummary = routeSummaryParts.Count > 0 ? string.Join(", ", routeSummaryParts) : "empty";
        }

        if (parsedPositionCount > 0)
        {
            if (logIncomingMessageTypes)
            {
                Log($"Track positions received: {parsedPositionCount}, lines={parsedLineCount}, routeUpdated={hasRenderableTrackLine}, planPointCounts={lastTrackPositionsSummary}");
            }

            if (!hasRenderableTrackLine && !warnedNonRenderableTrackPositions)
            {
                LogWarning("TRACK_POSITIONS did not contain a complete planned route. Expected at least one plan.positions array with 2 or more points.");
                warnedNonRenderableTrackPositions = true;
            }
        }
    }

    private void AppendTrackPositions(JArray positions, string trackId, string planId, string frameFlightTime, TrackLine trackLine, TrackPlanLine planLine, ref int parsedPositionCount)
    {
        if (positions == null)
        {
            return;
        }

        foreach (var posToken in positions)
        {
            var position = new TrackPosition
            {
                trackId = trackId,
                planId = planId,
                flightTime = GetString(posToken, "flightTime") ?? frameFlightTime,
                lng = GetFloat(posToken, "lng") ?? 0f,
                lat = GetFloat(posToken, "lat") ?? 0f,
                alt = GetFloat(posToken, "alt") ?? 0f,
                heading = GetFloat(posToken, "heading") ?? 0f,
                pitch = GetFloat(posToken, "pitch") ?? 0f,
                roll = GetFloat(posToken, "roll") ?? 0f,
            };

            trackLine.positions.Add(position);
            if (planLine != null)
            {
                planLine.positions.Add(position);
            }

            // 计划航迹只用于画线，不更新飞机实时位置
            parsedPositionCount++;
        }
    }

    private static TrackPosition CloneTrackPosition(TrackPosition source)
    {
        if (source == null)
        {
            return null;
        }

        return new TrackPosition
        {
            trackId = source.trackId,
            planId = source.planId,
            flightTime = source.flightTime,
            lng = source.lng,
            lat = source.lat,
            alt = source.alt,
            heading = source.heading,
            pitch = source.pitch,
            roll = source.roll,
        };
    }

    private void ParseTaskTimeInfo(JToken data)
    {
        if (data == null)
        {
            return;
        }

        string startTime = data.Value<string>("startTime");
        string endTime = data.Value<string>("endTime");
        float durationSeconds = 0f;

        if (TryParseLocalTime(startTime, out DateTime start) && TryParseLocalTime(endTime, out DateTime end))
        {
            durationSeconds = Mathf.Max(0f, (float)(end - start).TotalSeconds);
            taskStartTime = start;
        }

        var info = new TaskTimeInfo
        {
            taskId = data.Value<string>("taskId"),
            startTime = startTime,
            endTime = endTime,
            durationSeconds = durationSeconds,
        };

        lock (dataLock)
        {
            latestTaskTimeInfo = info;
        }

        TaskTimeInfoReceived?.Invoke(info);
        Log($"Task time info: {info.startTime} ~ {info.endTime} ({info.durationSeconds:F3}s)");
    }

    private void ParseDataFields(JToken data)
    {
        if (data == null)
            return;

        var fieldsToken = data["fields"] as JObject ?? data as JObject;
        if (fieldsToken == null)
        {
            LogWarning("DATA_FIELDS has no fields object.");
            return;
        }

        lock (dataLock)
        {
            dataFields.Clear();
            foreach (var property in fieldsToken.Properties())
            {
                string jsonKey = (property.Name ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(jsonKey) || jsonKey == "fields")
                    continue;

                var aliases = new List<string>();
                if (property.Value is JArray array)
                {
                    foreach (var item in array)
                    {
                        string alias = item?.Value<string>();
                        if (!string.IsNullOrWhiteSpace(alias))
                            aliases.Add(alias.Trim());
                    }
                }
                else if (property.Value != null && property.Value.Type != JTokenType.Null)
                {
                    string alias = property.Value.Value<string>();
                    if (!string.IsNullOrWhiteSpace(alias))
                        aliases.Add(alias.Trim());
                }

                dataFields[jsonKey] = aliases;
            }
        }

        Log($"DATA_FIELDS received: {dataFields.Count} json keys ({string.Join(", ", dataFields.Keys)})");
    }

    // Data-side implementations may keep the frame time beside a record rather
    // than at data level. Resolve it once so every visual consumer sees the same time.
    private string GetFlightTime(JToken data, JToken envelope)
    {
        string flightTime = FindFlightTimeRecursive(data);
        if (!string.IsNullOrWhiteSpace(flightTime))
        {
            return flightTime;
        }

        // The protocol also permits the time at envelope level. Search that
        // separately because some data-side implementations attach it there.
        return FindFlightTimeRecursive(envelope);
    }

    private string FindFlightTimeRecursive(JToken token)
    {
        if (token == null)
        {
            return null;
        }

        string flightTime = null;
        if (token is JObject)
        {
            flightTime = GetString(token, "flightTime");
            if (!string.IsNullOrWhiteSpace(flightTime))
            {
                return flightTime;
            }
        }

        if (token is JObject objectToken)
        {
            foreach (var property in objectToken.Properties())
            {
                flightTime = FindFlightTimeRecursive(property.Value);
                if (!string.IsNullOrWhiteSpace(flightTime))
                {
                    return flightTime;
                }
            }
        }
        else if (token is JArray array)
        {
            foreach (var item in array)
            {
                flightTime = FindFlightTimeRecursive(item);
                if (!string.IsNullOrWhiteSpace(flightTime))
                {
                    return flightTime;
                }
            }
        }

        return null;
    }

    private void PublishFlightTime(string flightTime)
    {
        if (string.IsNullOrWhiteSpace(flightTime))
        {
            return;
        }

        if (TryParseElapsedTime(flightTime, out float elapsedFromStart))
        {
            FlightTimeReceived?.Invoke(flightTime, elapsedFromStart);
            return;
        }

        if (!TryParseLocalTime(flightTime, out DateTime current))
        {
            return;
        }

        DateTime? start;
        lock (dataLock)
        {
            if (!taskStartTime.HasValue)
            {
                taskStartTime = current;
            }

            start = taskStartTime;
        }

        if (!start.HasValue)
        {
            return;
        }

        float elapsedSeconds = Mathf.Max(0f, (float)(current - start.Value).TotalSeconds);
        FlightTimeReceived?.Invoke(flightTime, elapsedSeconds);
    }

    private static bool TryParseElapsedTime(string value, out float elapsedSeconds)
    {
        elapsedSeconds = 0f;
        if (string.IsNullOrWhiteSpace(value) || value.IndexOf(':') < 0 || value.IndexOf('-') >= 0)
        {
            return false;
        }

        string[] parts = value.Trim().Split(':');
        if (parts.Length != 2 && parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out int first) ||
            !int.TryParse(parts[parts.Length - 2], out int minutes) ||
            !float.TryParse(parts[parts.Length - 1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float seconds))
        {
            return false;
        }

        elapsedSeconds = parts.Length == 2
            ? first * 60f + seconds
            : first * 3600f + minutes * 60f + seconds;
        return elapsedSeconds >= 0f;
    }

    private static bool TryParseLocalTime(string value, out DateTime time)
    {
        if (DateTime.TryParseExact(
            value,
            "yyyy-MM-dd HH:mm:ss.fff",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out time))
        {
            return true;
        }

        // Fallback: try general parse (supports formats like "2026/7/21 8:00:00")
        return DateTime.TryParse(value, out time);
    }

    private string GetString(JToken token, params string[] names)
    {
        if (token == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var value = token[name];
            if (value == null || value.Type == JTokenType.Null)
                continue;
            string text = value.Value<string>();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private float? GetFloat(JToken token, params string[] names)
    {
        if (token == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var value = token[name];
            if (value == null || value.Type == JTokenType.Null)
                continue;
            float? number = value.Value<float?>();
            if (number.HasValue)
                return number.Value;
        }

        return null;
    }

    private int? GetInt(JToken token, params string[] names)
    {
        if (token == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var value = token[name];
            if (value == null || value.Type == JTokenType.Null)
                continue;
            int? number = value.Value<int?>();
            if (number.HasValue)
                return number.Value;
        }

        return null;
    }

    private void ParseTimetableState(JToken data, JToken envelope = null)
    {
        if (data == null)
        {
            return;
        }

        string messageFlightTime = GetFlightTime(data, envelope);
        PublishFlightTime(messageFlightTime);

        lock (dataLock)
        {
            string frameFlightTime = messageFlightTime;
            // 飞机ID从 data 层级获取（如 "UAV-001"）
            string dataTrackId = GetString(data, "trackId");
            var statesToken = data["flightStates"];
            if (statesToken == null)
            {
                // 如果没有 flightStates，但有 data.trackId，直接用 data 层级的信息
                if (!string.IsNullOrWhiteSpace(dataTrackId))
                {
                    latestStatesByTrack[dataTrackId] = new TrackTimetableState
                    {
                        flightTime = frameFlightTime,
                        trackId = dataTrackId,
                        state = GetString(data, "state"),
                        battery = GetFloat(data, "battery") ?? 0f,
                        speed = GetFloat(data, "speed") ?? 0f,
                        alarm = GetString(data, "alarm") ?? string.Empty,
                        detectorType = GetString(data, "detector_type") ?? string.Empty,
                        rangeType = GetString(data, "range_type") ?? string.Empty,
                        detectRange = GetFloat(data, "detect_range") ?? 0f,
                        yawAngle = GetFloat(data, "yaw_angle") ?? 0f,
                        pitchAngle = GetFloat(data, "pitch_angle") ?? 0f,
                        centerPoint = GetString(data, "center_point") ?? string.Empty,
                        radarStatus = GetString(data, "radar_status") ?? string.Empty,
                    };
                    if (string.IsNullOrWhiteSpace(primaryTrackId))
                    {
                        primaryTrackId = dataTrackId;
                    }
                }
                return;
            }

            // flightStates 可以是对象或数组
            IEnumerable<JToken> stateTokens;
            if (statesToken.Type == JTokenType.Array)
            {
                stateTokens = statesToken.Children();
            }
            else
            {
                stateTokens = new[] { statesToken };
            }

            foreach (var stateToken in stateTokens)
            {
                // 飞机ID优先用 data 层级的 trackId（UAV-001），不是 flightStates 内部的 trackId
                string trackId = dataTrackId ?? GetString(stateToken, "trackId");
                if (string.IsNullOrWhiteSpace(trackId))
                {
                    continue;
                }

                latestStatesByTrack[trackId] = new TrackTimetableState
                {
                    flightTime = frameFlightTime,
                    trackId = trackId,
                    state = GetString(stateToken, "state") ?? GetString(data, "state"),
                    battery = GetFloat(stateToken, "battery") ?? GetFloat(data, "battery") ?? 0f,
                    speed = GetFloat(stateToken, "speed") ?? GetFloat(data, "speed") ?? 0f,
                    alarm = GetString(stateToken, "alarm") ?? GetString(data, "alarm") ?? string.Empty,
                    detectorType = GetString(stateToken, "detector_type") ?? string.Empty,
                    rangeType = GetString(stateToken, "range_type") ?? string.Empty,
                    detectRange = GetFloat(stateToken, "detect_range") ?? 0f,
                    yawAngle = GetFloat(stateToken, "yaw_angle") ?? 0f,
                    pitchAngle = GetFloat(stateToken, "pitch_angle") ?? 0f,
                    centerPoint = GetString(stateToken, "center_point") ?? string.Empty,
                    radarStatus = GetString(stateToken, "radar_status") ?? string.Empty,
                };
                if (string.IsNullOrWhiteSpace(primaryTrackId))
                {
                    primaryTrackId = trackId;
                }
            }
        }
    }

    private void ParseCycleTableData(JToken data, JToken envelope = null)
    {
        if (data == null)
        {
            return;
        }

        string messageFlightTime = GetFlightTime(data, envelope);
        PublishFlightTime(messageFlightTime);

        var recordsToken = data["records"];
        if (recordsToken == null)
        {
            return;
        }

        lock (dataLock)
        {
            string flightTime = messageFlightTime;
            string dataTrackId = GetString(data, "trackId");
            if (latestCycleFlightTime != flightTime)
            {
                latestCycleRecords.Clear();
                latestCycleUpdatedTrackIds.Clear();
                latestCycleFlightTime = flightTime;
            }

            if (recordsToken is JArray recordArray)
            {
                foreach (var recordToken in recordArray)
                {
                    ParseCycleRecordUnsafe(flightTime, dataTrackId, recordToken);
                }
            }
            else
            {
                ParseCycleRecordUnsafe(flightTime, dataTrackId, recordsToken);
            }
        }
    }

    private void ParseCycleRecordUnsafe(string flightTime, string dataTrackId, JToken recordToken)
    {
        if (recordToken == null)
        {
            return;
        }

        // 飞机ID优先用 data 层级的 trackId（如 "UAV-001"），不是 records 内部的
        string trackId = dataTrackId ?? GetString(recordToken, "trackId");
        float lng = GetFloat(recordToken, "lng") ?? 0f;
        float lat = GetFloat(recordToken, "lat") ?? 0f;
        float alt = GetFloat(recordToken, "alt") ?? 0f;
        float heading = GetFloat(recordToken, "heading") ?? 0f;
        float pitch = GetFloat(recordToken, "pitch") ?? 0f;
        float roll = GetFloat(recordToken, "roll") ?? 0f;

        if (string.IsNullOrWhiteSpace(trackId))
        {
            trackId = ResolveTrackIdForCycleRecordUnsafe(lng, lat);
        }

        int cycleId = GetInt(recordToken, "cycleId") ?? 0;
        PublishFlightTimeFromCycleId(flightTime, cycleId);
        var record = new CycleTableRecord
        {
            flightTime = flightTime,
            trackId = trackId,
            cycleId = cycleId,
            lng = lng,
            lat = lat,
            alt = alt,
            heading = heading,
            pitch = pitch,
            roll = roll,
            radarStatus = GetString(recordToken, "radar_status") ?? string.Empty,
        };

        latestCycleRecords.Add(record);

        if (!string.IsNullOrWhiteSpace(trackId))
        {
            latestCycleUpdatedTrackIds.Add(trackId);
            if (latestStatesByTrack.TryGetValue(trackId, out var state))
            {
                state.radarStatus = record.radarStatus;
            }

            var position = new TrackPosition
            {
                trackId = trackId,
                flightTime = flightTime,
                lng = lng,
                lat = lat,
                alt = alt,
                heading = heading,
                pitch = pitch,
                roll = roll,
            };

            latestPositionsByTrack[trackId] = position;
            if (string.IsNullOrWhiteSpace(primaryTrackId))
            {
                primaryTrackId = trackId;
            }

            UpdateTurnRate(trackId, position);
        }
    }

    private void PublishFlightTimeFromCycleId(string flightTime, int cycleId)
    {
        if (cycleId <= 0 || HasUsableFlightTime(flightTime))
        {
            return;
        }

        // The transmitted cycle tables use cycleId 1..601 for 00:00.0..10:00.0.
        // This is only a fallback for senders that omit flightTime from the JSON frame.
        float elapsedSeconds = cycleId - 1;
        FlightTimeReceived?.Invoke($"cycle:{cycleId}", elapsedSeconds);
    }

    private static bool HasUsableFlightTime(string flightTime)
    {
        return TryParseElapsedTime(flightTime, out _) || TryParseLocalTime(flightTime, out _);
    }

    private string ResolveTrackIdForCycleRecordUnsafe(float lng, float lat)
    {
        string bestTrackId = null;
        float bestDistance = float.MaxValue;

        foreach (var kv in latestPositionsByTrack)
        {
            if (latestCycleUpdatedTrackIds.Contains(kv.Key) || kv.Value == null)
            {
                continue;
            }

            float dLng = kv.Value.lng - lng;
            float dLat = kv.Value.lat - lat;
            float distance = dLng * dLng + dLat * dLat;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTrackId = kv.Key;
            }
        }

        if (string.IsNullOrWhiteSpace(bestTrackId))
        {
            foreach (var kv in latestStatesByTrack)
            {
                if (!latestCycleUpdatedTrackIds.Contains(kv.Key))
                {
                    return kv.Key;
                }
            }
        }

        return bestTrackId;
    }

    private void UpdateTurnRate(string trackId, TrackPosition position)
    {
        if (string.IsNullOrWhiteSpace(position.flightTime))
        {
            previousHeadingByTrack[trackId] = position.heading;
            return;
        }

        if (!DateTime.TryParse(position.flightTime, out var currentTime))
        {
            previousHeadingByTrack[trackId] = position.heading;
            return;
        }

        if (previousHeadingByTrack.TryGetValue(trackId, out var previousHeading) &&
            previousTimeByTrack.TryGetValue(trackId, out var previousTime))
        {
            double deltaSeconds = (currentTime - previousTime).TotalSeconds;
            if (deltaSeconds > 0.001)
            {
                float deltaHeading = Mathf.DeltaAngle(previousHeading, position.heading);
                _primaryTurnRateCacheByTrack[trackId] = deltaHeading / (float)deltaSeconds;
            }
        }

        previousHeadingByTrack[trackId] = position.heading;
        previousTimeByTrack[trackId] = currentTime;
    }

    private readonly Dictionary<string, float> _primaryTurnRateCacheByTrack = new Dictionary<string, float>();

    private TrackPosition GetPrimaryPositionUnsafe()
    {
        if (!string.IsNullOrWhiteSpace(primaryTrackId) && latestPositionsByTrack.TryGetValue(primaryTrackId, out var position))
        {
            return position;
        }

        foreach (var kv in latestPositionsByTrack)
        {
            primaryTrackId = kv.Key;
            return kv.Value;
        }

        return null;
    }

    private TrackTimetableState GetPrimaryStateUnsafe()
    {
        if (!string.IsNullOrWhiteSpace(primaryTrackId) && latestStatesByTrack.TryGetValue(primaryTrackId, out var state))
        {
            return state;
        }

        foreach (var kv in latestStatesByTrack)
        {
            primaryTrackId = kv.Key;
            return kv.Value;
        }

        return null;
    }

    private float GetPrimaryTurnRateUnsafe()
    {
        if (!string.IsNullOrWhiteSpace(primaryTrackId) && _primaryTurnRateCacheByTrack.TryGetValue(primaryTrackId, out var rate))
        {
            return rate;
        }

        return 0f;
    }

    private void OnDisable()
    {
        if (!isDuplicateInstance)
        {
            DisconnectImmediate();
        }
    }

    private void OnApplicationQuit()
    {
        DisconnectImmediate();
    }

    private bool OnWantsToQuit()
    {
        DisconnectImmediate();
        return true;
    }

    private void OnDestroy()
    {
        Application.wantsToQuit -= OnWantsToQuit;
        DisconnectImmediate();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Log(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[FlightDataStreamReceiver] {message}");
        }
    }

    private void LogIncomingType(string type)
    {
        if (enableDebugLog && logIncomingMessageTypes)
        {
            Debug.Log($"[FlightDataStreamReceiver] Received message type={type}");
        }
    }

    private void LogError(string message)
    {
        if (enableDebugLog)
        {
            Debug.LogError($"[FlightDataStreamReceiver] {message}");
        }
    }

    private void LogWarning(string message)
    {
        if (enableDebugLog)
        {
            Debug.LogWarning($"[FlightDataStreamReceiver] {message}");
        }
    }
}
