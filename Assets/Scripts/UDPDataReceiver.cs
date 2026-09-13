using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using Pb;

public class UDPDataReceiver : MonoBehaviour
{
    // Legacy receiver for older UDP/Protobuf demos.
    // Keep it as a manual tool script only: do not auto-start it unless explicitly enabled.
    // 单例模式
    private static UDPDataReceiver instance;
    public static UDPDataReceiver Instance
    {
        get { return instance; }
    }
    
    // UDP配置
    [Header("UDP配置")]
    public string ipAddress = "127.0.0.1"; // UDP IP地址
    public int port = 8888; // UDP端口
    public bool enableDebug = true; // 是否启用调试信息
    public bool autoStart = false; // 手动启用模式，默认关闭
    
    // 飞机数据存储
    public Dictionary<string, string> planeData = new Dictionary<string, string>();
    public int numberOfPlanes = 3; // 飞机数量
    
    // 线程安全的更新队列
    private Queue<Action> _mainThreadActions = new Queue<Action>();
    
    // 私有变量
    private UdpClient udpClient;
    private bool isRunning;
    private Thread listenerThread;
    private IPEndPoint remoteEndPoint;
    
    void Awake()
    {
        // 单例模式实现
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        if (autoStart)
        {
            StartServer();
        }
    }

    // 手动启动/停止接口，供旧调试场景显式调用
    public void EnableReceiver()
    {
        if (!isRunning)
        {
            StartServer();
        }
    }

    public void DisableReceiver()
    {
        StopServer();
    }
    
    void OnDestroy()
    {
        StopServer();
    }
    
    void StartServer()
    {
        isRunning = true;
        
        try
        {
            // 解析IP地址
            IPAddress ipAddressObj = IPAddress.Parse(ipAddress);
            remoteEndPoint = new IPEndPoint(ipAddressObj, port);
            
            // 使用配置的IP地址和端口号创建UDP客户端
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            
            if (enableDebug)
            {
                Debug.Log("UDP listener started on " + ipAddress + ":" + port);
            }
            
            // 创建监听线程
            listenerThread = new Thread(ListenForMessages);
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to start UDP server: " + e.Message);
        }
    }
    
    void StopServer()
    {
        isRunning = false;
        
        if (listenerThread != null && listenerThread.IsAlive)
        {
            listenerThread.Abort();
        }
        
        if (udpClient != null)
        {
            udpClient.Close();
        }
    }
    
    void ListenForMessages()
    {
        while (isRunning)
        {
            try
            {
                if (udpClient.Available > 0)
                {
                    // 接收数据
                    byte[] receivedBytes = udpClient.Receive(ref remoteEndPoint);
                    
                    if (enableDebug)
                    {
                        Debug.Log("Received " + receivedBytes.Length + " bytes from " + remoteEndPoint.ToString());
                    }
                    
                    try
                    {
                        // 转换接收到的字节数组为字符串
                        string bufferString = Encoding.ASCII.GetString(receivedBytes);
                        if (string.IsNullOrEmpty(bufferString))
                        {
                            Debug.LogWarning("Received empty buffer string");
                            continue;
                        }
                        
                        // 解码Base64字符串
                        byte[] protobufBytes = Convert.FromBase64String(bufferString);
                        if (protobufBytes == null || protobufBytes.Length == 0)
                        {
                            Debug.LogWarning("Failed to decode Base64 string");
                            continue;
                        }
                        
                        // 解析Protobuf数据
                        DDMData ddmData = DDMData.Parser.ParseFrom(protobufBytes);
                        if (ddmData == null)
                        {
                            Debug.LogWarning("Failed to parse DDMData");
                            continue;
                        }
                        
                        // 处理飞机数据
                        ProcessPlaneData(ddmData);
                    }
                    catch (Exception parseEx)
                    {
                        Debug.LogError("Failed to parse DDMData: " + parseEx.Message);
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                if (isRunning)
                {
                    Debug.LogError("UDP receive error: " + e.Message);
                }
                Thread.Sleep(100);
            }
        }
    }
    
    void ProcessPlaneData(DDMData ddmData)
    {
        // 遍历所有仿真变量
        foreach (var simVar in ddmData.SimVars)
        {
            if (simVar == null || string.IsNullOrEmpty(simVar.Name))
            {
                continue;
            }

            string varName = simVar.Name;

            // 1. 特殊处理全局变量
            if (varName == "simTime" || varName == "cur_time" || varName == "SimTime")
            {
                if (simVar.Type == DDMData.Types.SimVar.Types.Type.Double && simVar.HasDoubleValue)
                {
                    planeData["SimTime"] = simVar.DoubleValue.ToString();
                    continue;
                }
            }

            // 2. 解析带编号的变量 (如 "0_latitude")
            if (varName.Contains("_"))
            {
                string[] parts = varName.Split(new char[] { '_' });
                // 严格限制两个部分，避免 0_longitude_diff[0] 这种干扰项
                if (parts.Length == 2)
                {
                    int planeId;
                    if (int.TryParse(parts[0], out planeId) && planeId < numberOfPlanes)
                    {
                        string dataType = parts[1];
                        
                        // 只处理我们需要的主变量
                        if (dataType == "longitude" || dataType == "latitude" || dataType == "height" || dataType == "altitude" || dataType == "time")
                        {
                            string key = planeId + "_" + dataType;
                            if (simVar.Type == DDMData.Types.SimVar.Types.Type.Double && simVar.HasDoubleValue)
                            {
                                planeData[key] = simVar.DoubleValue.ToString();
                                
                                // 备选时间源
                                if (dataType == "time" && !planeData.ContainsKey("SimTime"))
                                {
                                    planeData["SimTime"] = simVar.DoubleValue.ToString();
                                }
                            }
                        }
                    }
                }
            }
            
            // 3. 处理姿态数据变量（如 "[载机俯仰角][_角度_毫弧度]"）
            if (simVar.Type == DDMData.Types.SimVar.Types.Type.Double && simVar.HasDoubleValue)
            {
                // 直接使用变量名作为键存储数据
                planeData[varName] = simVar.DoubleValue.ToString();
            }
        }

        // 更新飞机位置
        UpdatePlanePositions();

        if (enableDebug)
        {
            Debug.Log($"Processed {ddmData.SimVars.Count} vars. SimTime: {planeData.GetValueOrDefault("SimTime", "N/A")}");
            // 打印姿态数据
            Debug.Log("Attitude data in planeData:");
            string[] attitudeKeys = { "[载机俯仰角][_角度_毫弧度]", "[载机横滚角][_角度_毫弧度]", "[载机真航角][_角度_毫弧度]" };
            foreach (var key in attitudeKeys)
            {
                if (planeData.ContainsKey(key))
                {
                    Debug.Log($"  {key}: {planeData[key]}");
                }
                else
                {
                    Debug.Log($"  {key}: Not found");
                }
            }
        }
    }
    
    void UpdatePlanePositions()
    {
        // 旧版 PlaneDisplayController 已移除，这里保留空队列消费，避免旧逻辑继续参与界面更新。
        lock (_mainThreadActions)
        {
            _mainThreadActions.Enqueue(() => { });
        }
    }
    
    // 主线程更新方法
    private void Update()
    {
        // 处理主线程队列中的操作
        while (true)
        {
            Action action = null;
            lock (_mainThreadActions)
            {
                if (_mainThreadActions.Count > 0)
                {
                    action = _mainThreadActions.Dequeue();
                }
                else
                {
                    break;
                }
            }
            
            if (action != null)
            {
                action.Invoke();
            }
        }
    }
    
    // 公开方法：获取飞机数据
    public double GetPlaneLatitude(int planeId)
    {
        string key = planeId + "_latitude";
        double lat = 0;
        if (planeData.ContainsKey(key))
        {
            double.TryParse(planeData[key], out lat);
        }
        return lat;
    }
    
    public double GetPlaneLongitude(int planeId)
    {
        string key = planeId + "_longitude";
        double lon = 0;
        if (planeData.ContainsKey(key))
        {
            double.TryParse(planeData[key], out lon);
        }
        return lon;
    }
    
    public double GetPlaneAltitude(int planeId)
    {
        // 优先匹配 "altitude"，如果不存在则匹配 "height"
        string altKey = planeId + "_altitude";
        string heightKey = planeId + "_height";
        
        if (planeData.ContainsKey(altKey))
        {
            double val;
            if (double.TryParse(planeData[altKey], out val)) return val;
        }
        
        if (planeData.ContainsKey(heightKey))
        {
            double val;
            if (double.TryParse(planeData[heightKey], out val)) return val;
        }
        
        return 0;
    }
    
    // 公开方法：检查飞机数据是否存在
    public bool HasPlaneData(int planeId)
    {
        string latKey = planeId + "_latitude";
        string lonKey = planeId + "_longitude";
        return planeData.ContainsKey(latKey) && planeData.ContainsKey(lonKey);
    }
    
    // 公开方法：发送坐标边界配置
    public void SendCoordinateBounds(float minLon, float maxLon, float minLat, float maxLat, float minGameX, float maxGameX, float minGameY, float maxGameY)
    {
        try
        {
            if (udpClient != null && remoteEndPoint != null)
            {
                // 构建坐标边界配置消息
                string boundsMessage = string.Format("BOUNDS:{0},{1},{2},{3},{4},{5},{6},{7}", 
                    minLon, maxLon, minLat, maxLat, minGameX, maxGameX, minGameY, maxGameY);
                
                // 转换为字节数组
                byte[] messageBytes = Encoding.UTF8.GetBytes(boundsMessage);
                
                // 发送消息
                udpClient.Send(messageBytes, messageBytes.Length, remoteEndPoint);
                
                if (enableDebug)
                {
                    Debug.Log("Sent coordinate bounds: " + boundsMessage);
                }
            }
            else
            {
                Debug.LogWarning("UDP client not initialized, cannot send coordinate bounds");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to send coordinate bounds: " + e.Message);
        }
    }

    // 公开方法：获取浮点数值
    public float GetFloatValue(string key, float defaultValue = 0.0f)
    {
        if (planeData.ContainsKey(key))
        {
            float value;
            if (float.TryParse(planeData[key], out value))
            {
                return value;
            }
        }
        return defaultValue;
    }

    // 公开方法：获取双精度浮点数值
    public double GetDoubleValue(string key, double defaultValue = 0.0)
    {
        if (planeData.ContainsKey(key))
        {
            double value;
            if (double.TryParse(planeData[key], out value))
            {
                return value;
            }
        }
        return defaultValue;
    }
}
