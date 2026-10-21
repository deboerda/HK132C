using Google.Protobuf;
using GoogleProtobuf;
using Pb;
using UnityEngine;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;
using System.Text;
using System.Collections.Generic;

public class UdpProtobufServer : MonoBehaviour
{
    // 配置参数，可在检查器中设置
    [Header("UDP配置")]
    public int port = 32702; // 监听端口
    public string localAddress = "127.0.0.1"; // 本地地址
    
    [Header("UI配置")]
    public Text outputText; // 用于显示数据的Text (Legacy)组件
    public UnityEngine.UI.ScrollRect scrollView; // 可选的ScrollView组件，用于滚动查看数据
    public int maxLines = 50; // 最大显示行数
    
    [Header("调试配置")]
    public bool enableDebug = true; // 是否启用调试信息
    
    [Header("UI更新配置")]
    public float updateInterval = 0.1f; // UI更新间隔（秒）
    public bool autoScrollToBottom = true; // 是否自动滚动到底部
    
    // 私有变量
    private UdpClient udpClient;
    private bool isRunning;
    private Thread listenerThread;
    private IPEndPoint remoteEndPoint;
    private StringBuilder messageBuffer = new StringBuilder();
    
    // 用于数据合并的变量
    private Dictionary<string, string> dataValues = new Dictionary<string, string>();
    private float lastUpdateTime = 0f;
    private string lastDisplayedText = "";

    void Start()
    {
        // 初始化UnityMainThreadDispatcher
        UnityMainThreadDispatcher.Instance();
        StartServer();
    }
    
    void Update()
    {
        // 定时更新UI
        if (Time.time - lastUpdateTime > updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdateUI();
        }
    }

    void StartServer()
    {
        isRunning = true;
        remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        try
        {
            // 使用配置的端口号创建UDP客户端
            udpClient = new UdpClient(port);
            if (enableDebug)
            {
                Debug.Log("UDP Server started on port " + port);
            }
            
            listenerThread = new Thread(new ThreadStart(ListenForMessages));
            listenerThread.Start();
            if (enableDebug)
            {
                Debug.Log("UDP listener thread started");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to start UDP server: " + ex.Message);
        }
    }

    void ListenForMessages()
    {
        if (enableDebug)
        {
            Debug.Log("Listening for UDP messages...");
        }
        
        while (isRunning)
        {
            try
            {
                if (udpClient.Available > 0)
                {
                    byte[] receivedBytes = udpClient.Receive(ref remoteEndPoint);
                    if (enableDebug)
                    {
                        Debug.Log("Received " + receivedBytes.Length + " bytes from " + remoteEndPoint.ToString());
                    }
                    
                    try
                    {
                        // 1. 转换接收到的字节数组为字符串
                        string bufferString = Encoding.ASCII.GetString(receivedBytes);
                        if (string.IsNullOrEmpty(bufferString))
                        {
                            Debug.LogWarning("Received empty buffer string");
                            return;
                        }
                        
                        // 2. 解码Base64字符串
                        byte[] protobufBytes = Convert.FromBase64String(bufferString);
                        if (protobufBytes == null || protobufBytes.Length == 0)
                        {
                            Debug.LogWarning("Failed to decode Base64 string");
                            return;
                        }
                        
                        // 3. 解析Protobuf数据
                        DDMData ddmData = DDMData.Parser.ParseFrom(protobufBytes);
                        if (ddmData == null)
                        {
                            Debug.LogWarning("Failed to parse DDMData");
                            return;
                        }
                        
                        // 4. 处理数据，存储到字典中
                        try
                        {
                            // 存储SimTime
                            dataValues["SimTime"] = ddmData.SimTime.ToString();
                            dataValues["SimVars count"] = ddmData.SimVars.Count.ToString();
                            
                            // 存储每个simVar
                            foreach (var simVar in ddmData.SimVars)
                            {
                                try
                                {
                                    string valueStr = "";
                                    switch (simVar.Type)
                                    {
                                        case DDMData.Types.SimVar.Types.Type.Bool:
                                            valueStr = simVar.BoolValue.ToString();
                                            break;
                                        case DDMData.Types.SimVar.Types.Type.Int:
                                            valueStr = simVar.IntValue.ToString();
                                            break;
                                        case DDMData.Types.SimVar.Types.Type.Double:
                                            valueStr = simVar.DoubleValue.ToString();
                                            break;
                                        case DDMData.Types.SimVar.Types.Type.String:
                                            valueStr = simVar.StringValue;
                                            break;
                                    }
                                    dataValues[simVar.Name] = valueStr;
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning("Error processing sim var: " + ex.Message);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("Error processing data: " + ex.Message);
                            return;
                        }
                        
                        // 不需要立即更新UI，由Update方法定时更新
                        
                        // 注释掉输出到控制台的部分
                        /*
                        Debug.Log("Received DDMData:");
                        Debug.Log("  MachineTime: " + ddmData.MachineTime);
                        Debug.Log("  SimTime: " + ddmData.SimTime);
                        Debug.Log("  SimVars count: " + ddmData.SimVars.Count);
                        
                        foreach (var simVar in ddmData.SimVars)
                        {
                            string valueStr = "";
                            switch (simVar.Type)
                            {
                                case DDMData.Types.SimVar.Types.Type.Bool:
                                    valueStr = simVar.BoolValue.ToString();
                                    break;
                                case DDMData.Types.SimVar.Types.Type.Int:
                                    valueStr = simVar.IntValue.ToString();
                                    break;
                                case DDMData.Types.SimVar.Types.Type.Double:
                                    valueStr = simVar.DoubleValue.ToString();
                                    break;
                                case DDMData.Types.SimVar.Types.Type.String:
                                    valueStr = simVar.StringValue;
                                    break;
                            }
                            Debug.Log($"  {simVar.Name}: {valueStr} ({simVar.Type})");
                        }
                        
                        Console.WriteLine("Received DDMData with " + ddmData.SimVars.Count + " sim vars");
                        */
                    }
                    catch (Exception parseEx)
                    {
                        Debug.LogError("Failed to parse DDMData: " + parseEx.Message);
                        Debug.LogError("Exception type: " + parseEx.GetType().Name);
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    Debug.LogError("UDP error: " + ex.Message);
                }
            }
        }
    }
    
    // 更新UI显示
    private void UpdateUI()
    {
        if (outputText != null)
        {
            try
            {
                // 构建显示文本
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}]");
                sb.AppendLine();
                
                // 显示SimTime和SimVars count
                if (dataValues.ContainsKey("SimTime"))
                {
                    sb.AppendLine($"SimTime: {dataValues["SimTime"]}");
                }
                if (dataValues.ContainsKey("SimVars count"))
                {
                    sb.AppendLine($"SimVars count: {dataValues["SimVars count"]}");
                }
                sb.AppendLine();
                
                // 创建字典的副本，避免在遍历过程中修改字典导致的错误
                Dictionary<string, string> dataValuesCopy = new Dictionary<string, string>(dataValues);
                
                // 显示所有simVar
                foreach (var kvp in dataValuesCopy)
                {
                    // 跳过已经显示的SimTime和SimVars count
                    if (kvp.Key != "SimTime" && kvp.Key != "SimVars count")
                    {
                        sb.AppendLine($"{kvp.Key}: {kvp.Value}");
                    }
                }
                
                string newText = sb.ToString();
                
                // 只有当文本发生变化时才更新UI
                if (newText != lastDisplayedText)
                {
                    // 在主线程中更新UI
                    UnityMainThreadDispatcher.Instance().Enqueue(() => {
                        try
                        {
                            if (outputText != null)
                            {
                                // 限制文本长度，避免UI错误
                                if (newText.Length > 10000)
                                {
                                    newText = newText.Substring(0, 10000) + "...";
                                }
                                outputText.text = newText;
                                lastDisplayedText = newText;
                                
                                // 自动滚动到底部
                                if (autoScrollToBottom && scrollView != null)
                                {
                                    // 下一帧滚动到底部，确保文本已经更新
                                    UnityMainThreadDispatcher.Instance().Enqueue(() => {
                                        try
                                        {
                                            if (scrollView != null)
                                            {
                                                scrollView.verticalNormalizedPosition = 0f;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.LogError("Error scrolling to bottom: " + ex.Message);
                                        }
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("Error updating UI text: " + ex.Message);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error enqueuing UI update: " + ex.Message);
            }
        }
    }

    void OnDestroy()
    {
        StopServer();
    }

    void StopServer()
    {
        isRunning = false;
        
        if (listenerThread != null && listenerThread.IsAlive)
        {
            listenerThread.Join();
        }
        
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
        
        if (enableDebug)
        {
            Debug.Log("UDP Server stopped");
        }
    }
}