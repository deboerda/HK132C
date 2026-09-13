using System.Collections;
using UnityEngine;

public class FlightRouteRequestOnStart : MonoBehaviour
{
    [SerializeField] private FlightDataStreamClient dataClient;
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private string taskId = "";
    [SerializeField] private float initialDelay = 0.5f;
    [SerializeField] private float retryInterval = 1.0f;
    [SerializeField] private int maxAttempts = 60;
    [SerializeField] private bool waitForDataChannel = true;
    [SerializeField] private bool requestOnStart = true;
    [SerializeField] private bool enableDebugLog = false;

    private Coroutine requestRoutine;

    private void OnEnable()
    {
        FlightDataStreamReceiver.DataChannelConnected -= OnDataChannelConnected;
        FlightDataStreamReceiver.DataChannelConnected += OnDataChannelConnected;
    }

    private void OnDisable()
    {
        FlightDataStreamReceiver.DataChannelConnected -= OnDataChannelConnected;
    }

    private void Start()
    {
        if (requestOnStart)
        {
            RequestRouteWithRetry();
        }
    }

    public void RequestRouteWithRetry()
    {
        if (requestRoutine != null)
        {
            StopCoroutine(requestRoutine);
        }

        requestRoutine = StartCoroutine(RequestRoutine());
    }

    private void OnDataChannelConnected()
    {
        if (receiver == null || !receiver.HasPlannedRouteLines)
        {
            Log("Data channel connected, requesting planned route.");
            RequestRouteWithRetry();
        }
    }

    private IEnumerator RequestRoutine()
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(initialDelay);
        }

        for (int attempt = 1; attempt <= Mathf.Max(1, maxAttempts); attempt++)
        {
            EnsureClient();
            EnsureReceiver();

            if (receiver != null && receiver.HasPlannedRouteLines)
            {
                Log($"Planned route already loaded before attempt {attempt}.");
                requestRoutine = null;
                yield break;
            }

            if (waitForDataChannel && (receiver == null || !receiver.IsConnected))
            {
                if (attempt == 1)
                {
                    Log($"Waiting for data channel before requesting planned route. attempt={attempt}/{maxAttempts}");
                }
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, retryInterval));
                continue;
            }

            int responseCountBeforeWait = receiver != null ? receiver.TrackPositionsResponseCount : 0;
            if (dataClient != null && dataClient.SendTrackPositionsRequest(taskId))
            {
                Log($"Requested planned route on attempt {attempt}.");

                float waitUntil = Time.unscaledTime + Mathf.Max(0.1f, retryInterval);
                while (Time.unscaledTime < waitUntil)
                {
                    EnsureReceiver();
                    if (receiver != null && receiver.HasPlannedRouteLines)
                    {
                        Log($"Planned route loaded after attempt {attempt}.");
                        requestRoutine = null;
                        yield break;
                    }

                    if (receiver != null && receiver.TrackPositionsResponseCount > responseCountBeforeWait)
                    {
                        Debug.LogWarning($"[FlightRouteRequestOnStart] TRACK_POSITIONS response was received but is not a renderable planned route. planPointCounts={receiver.LastTrackPositionsSummary}");
                        // The data service may still be loading its CSV files. Keep retrying
                        // instead of treating an early single-point response as final.
                        break;
                    }

                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, retryInterval));
            }
        }

        if (enableDebugLog)
        {
            Debug.LogWarning("[FlightRouteRequestOnStart] Planned route was not loaded. Data side must return TRACK_POSITIONS with full plans[].positions, at least 2 points per plan.");
        }

        requestRoutine = null;
    }

    private void EnsureClient()
    {
        if (dataClient != null)
        {
            return;
        }

        dataClient = FindAnyObjectByType<FlightDataStreamClient>();
        if (dataClient == null)
        {
            dataClient = gameObject.AddComponent<FlightDataStreamClient>();
        }
    }

    private void EnsureReceiver()
    {
        if (receiver != null)
        {
            return;
        }

        receiver = FlightDataStreamReceiver.Instance;
        if (receiver == null)
        {
            receiver = FindAnyObjectByType<FlightDataStreamReceiver>();
        }
    }

    private void Log(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[FlightRouteRequestOnStart] {message}");
        }
    }
}
