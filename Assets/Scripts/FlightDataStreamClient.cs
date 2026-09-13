using System;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

public class FlightDataStreamClient : MonoBehaviour
{
    [Header("Data Server")]
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private int port = 9999;
    [SerializeField] private bool connectOnStart = false;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private TcpClient client;
    private NetworkStream stream;
    private readonly object sendLock = new object();

    public string Host => host;
    public int Port => port;
    public bool IsConnected => client != null && client.Connected && stream != null;

    [Serializable]
    private class CommandEnvelope
    {
        public string uuid;
        public long timestamp;
        public string type;
        public object data;
    }

    private void Start()
    {
        if (connectOnStart)
        {
            Connect();
        }
    }

    public bool Connect()
    {
        if (IsConnected)
        {
            return true;
        }

        try
        {
            client = new TcpClient();
            client.NoDelay = true;
            client.Connect(host, port);
            stream = client.GetStream();

            if (enableDebugLog)
            {
                Debug.Log($"[FlightDataStreamClient] Connected to {host}:{port}");
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Connect failed: {ex.Message}");
            Disconnect();
            return false;
        }
    }

    public void Disconnect()
    {
        try
        {
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }
        }
        catch (Exception ex)
        {
            LogError($"Stream close failed: {ex.Message}");
        }

        try
        {
            if (client != null)
            {
                client.Close();
                client = null;
            }
        }
        catch (Exception ex)
        {
            LogError($"Client close failed: {ex.Message}");
        }
    }

    public bool SendStart(string taskId = null)
    {
        return SendCommand("START_DATA_STREAM", string.IsNullOrWhiteSpace(taskId) ? new { } : new { taskId });
    }

    public bool SendTaskTimeInfoRequest(string taskId = null)
    {
        return SendCommand("GET_TASK_TIME_INFO", string.IsNullOrWhiteSpace(taskId) ? new { } : new { taskId });
    }

    public bool SendTrackPositionsRequest(string taskId = null)
    {
        return SendCommand("GET_TRACK_POSITIONS", string.IsNullOrWhiteSpace(taskId) ? new { } : new { taskId });
    }

    public bool SendPause()
    {
        return SendCommand("PAUSE_DATA_STREAM", new { });
    }

    public bool SendResume()
    {
        return SendCommand("RESUME_DATA_STREAM", new { });
    }

    public bool SendStop()
    {
        return SendCommand("STOP_DATA_STREAM", new { });
    }

    public bool SendSpeed(float multiplier)
    {
        return SendCommand("SET_PLAYBACK_SPEED", new { speedMultiplier = multiplier });
    }

    public bool SendSeek(string targetTime)
    {
        if (string.IsNullOrWhiteSpace(targetTime))
        {
            LogError("SendSeek failed: targetTime is empty.");
            return false;
        }

        return SendCommand("SEEK_TO_TIME", new { targetTime = targetTime.Trim() });
    }

    public bool SendCurrentTimeAsSeek()
    {
        return SendSeek(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
    }

    public bool SendCommand(string type, object data)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            LogError("SendCommand failed: type is empty.");
            return false;
        }

        if (!EnsureConnected())
        {
            return false;
        }

        var envelope = new CommandEnvelope
        {
            uuid = Guid.NewGuid().ToString(),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            type = type,
            data = data ?? new { }
        };

        string payload = JsonConvert.SerializeObject(envelope, Formatting.None) + "\n";

        try
        {
            lock (sendLock)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }

            if (enableDebugLog)
            {
                Debug.Log($"[FlightDataStreamClient] Sent {type}: {payload.Trim()}");
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Send failed [{type}]: {ex.Message}");
            Disconnect();
            return false;
        }
    }

    private bool EnsureConnected()
    {
        if (IsConnected)
        {
            return true;
        }

        return Connect();
    }

    private void LogError(string message)
    {
        if (enableDebugLog)
        {
            Debug.LogError($"[FlightDataStreamClient] {message}");
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }
}
