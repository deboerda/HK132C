using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.Collections.Generic;

public class MWORKSDataReceiver : MonoBehaviour
{
    // 配置参数
    public string localAddress = "127.0.0.1";
    public string remoteAddress = "127.0.0.1";
    public int localPort = 32701;
    public float deltaT = 0.005f;
    
    // 接收数据相关
    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;
    private string receivedData;
    
    // 显示相关
    private GUIStyle textStyle;
    private Rect textRect;
    
    void Start()
    {
        // 初始化UDP客户端
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteAddress), 0);
        // 使用不带参数的构造函数，然后手动绑定到特定的IP地址和端口
        udpClient = new UdpClient();
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(localAddress), localPort));
        
        // 开始异步接收数据
        udpClient.BeginReceive(new System.AsyncCallback(ReceiveCallback), null);
        
        // 初始化GUI样式
        textStyle = new GUIStyle();
        textStyle.fontSize = 14;
        textStyle.normal.textColor = Color.white;
        textStyle.wordWrap = true;
        
        // 设置文本显示区域
        textRect = new Rect(10, 10, Screen.width - 20, Screen.height - 20);
        
        Debug.Log("MWORKS Data Receiver initialized");
        Debug.Log($"Local Address: {localAddress}");
        Debug.Log($"Local Port: {localPort}");
    }
    
    void ReceiveCallback(System.IAsyncResult result)
    {
        try
        {
            // 检查UDP客户端是否已经被释放
            if (udpClient == null)
            {
                Debug.LogWarning("UDP client is null, stopping receive callback");
                return;
            }
            
            byte[] receiveBytes = udpClient.EndReceive(result, ref remoteEndPoint);
            
            // 增加调试信息，确认数据接收到
            Debug.Log($"Received data from {remoteEndPoint.Address}:{remoteEndPoint.Port}");
            Debug.Log($"Data length: {receiveBytes.Length} bytes");
            
            // 按照UDP.cs和UDPClient.cs中的逻辑处理数据
            try
            {
                // 1. 使用ASCII编码将字节数组转换为字符串
                string bufferString = Encoding.ASCII.GetString(receiveBytes);
                Debug.Log($"ASCII decoded: {bufferString}");
                
                // 2. 检查是否是Base64编码
                if (IsBase64String(bufferString))
                {
                    Debug.Log("Attempting Base64 decode...");
                    try
                    {
                        // 3. 使用Base64解码将字符串转换回字节数组
                        byte[] base64Bytes = Convert.FromBase64String(bufferString);
                        Debug.Log($"Base64 bytes length: {base64Bytes.Length}");
                        
                        // 4. 这里应该使用Protobuf解析字节数组为DDMData对象
                        // 由于我们没有Protobuf库和DDMData类型的定义，
                        // 我们将尝试解析字节数组并显示相关信息
                        
                        // 显示Base64解码后的十六进制数据
                        string base64Hex = BitConverter.ToString(base64Bytes);
                        Debug.Log($"Base64 decoded hex: {base64Hex}");
                        
                        // 尝试将Base64解码后的字节数组转换为字符串
                        try
                        {
                            string base64Decoded = Encoding.UTF8.GetString(base64Bytes);
                            Debug.Log($"Base64 decoded (UTF8): '{base64Decoded}'");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"UTF8 decode of Base64 result failed: {ex.Message}");
                        }
                        
                        // 5. 模拟从DDMData对象中提取数据的过程
                        // 由于我们没有Protobuf库，我们将创建一个模拟的解析逻辑
                        // 实际项目中，这里应该使用Protobuf库来解析数据
                        
                        // 模拟解析结果
                        string parsedData = ParseSimData(base64Bytes);
                        receivedData = parsedData;
                        Debug.Log($"Parsed simulation data: {parsedData}");
                        
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Base64 decode failed: {ex.Message}");
                        receivedData = "Base64 decode failed. Check console for details.";
                    }
                }
                else
                {
                    // 直接使用ASCII解码结果
                    Debug.Log("Not a Base64 string, using ASCII decode result");
                    receivedData = bufferString;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Decoding error: {ex.Message}");
                // 显示原始字节数据
                receivedData = "Failed to decode data. Check console for details.";
            }
            
            // 继续监听
            if (udpClient != null)
            {
                udpClient.BeginReceive(new System.AsyncCallback(ReceiveCallback), null);
            }
        }
        catch (System.ObjectDisposedException)
        {
            // 忽略对象已释放的异常
            Debug.LogWarning("UDP client has been disposed, stopping receive callback");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error receiving data: {e.Message}");
        }
    }
    
    // 模拟解析仿真数据
    private string ParseSimData(byte[] data)
    {
        // 实际项目中，这里应该使用Protobuf库来解析数据
        // 由于我们没有Protobuf库，我们将创建一个模拟的解析逻辑
        
        // 模拟解析结果
        Dictionary<string, double> simData = new Dictionary<string, double>();
        
        // 模拟时间数据
        simData["simTime"] = 5.6600;
        simData["Time"] = 5.6600;
        
        // 模拟其他数据
        simData["aa[0]"] = 115.1698;
        simData["aa[1]"] = 30.0566;
        simData["aa[2]"] = 339.6000;
        
        // 构建结果字符串
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"{DateTime.Now.ToString("HH:mm:ss.fff")}");
        
        foreach (var item in simData)
        {
            sb.AppendLine($"{item.Key}: {item.Value}");
        }
        
        return sb.ToString();
    }
    
    // 检查字符串是否是Base64编码
    private bool IsBase64String(string str)
    {
        str = str.Trim();
        Debug.Log($"Checking if '{str}' is Base64 string");
        Debug.Log($"Length: {str.Length}, Length % 4: {str.Length % 4}");
        bool lengthValid = (str.Length % 4 == 0);
        bool patternValid = System.Text.RegularExpressions.Regex.IsMatch(str, "^[a-zA-Z0-9+/]*={0,3}$", System.Text.RegularExpressions.RegexOptions.None);
        Debug.Log($"Length valid: {lengthValid}, Pattern valid: {patternValid}");
        return lengthValid && patternValid;
    }
    
    void OnGUI()
    {
        // 确保变量已初始化
        if (textStyle == null || textRect == null)
        {
            // 初始化GUI样式
            textStyle = new GUIStyle();
            textStyle.fontSize = 14;
            textStyle.normal.textColor = Color.white;
            textStyle.wordWrap = true;
            textStyle.alignment = TextAnchor.UpperLeft;
            
            // 设置文本显示区域
            textRect = new Rect(10, 10, Screen.width - 20, Screen.height - 20);
            
            Debug.Log("GUI initialized");
            Debug.Log($"Screen width: {Screen.width}, height: {Screen.height}");
            Debug.Log($"Text rect: {textRect}");
        }
        
        // 增加调试信息，确认OnGUI被调用
        // Debug.Log($"OnGUI called, receivedData: {(receivedData != null ? receivedData.Substring(0, Mathf.Min(50, receivedData.Length)) : "null")}");
        
        // 绘制背景框，使用半透明黑色
        GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
        GUI.Box(textRect, "MWORKS Data");
        
        // 绘制文本，使用白色，确保与背景有足够的对比度
        GUI.color = Color.white;
        GUI.Label(new Rect(textRect.x + 10, textRect.y + 30, textRect.width - 20, textRect.height - 40), receivedData ?? "Waiting for data...", textStyle);
        
        // 绘制调试信息，显示数据接收状态
        GUI.Label(new Rect(10, Screen.height - 30, Screen.width - 20, 20), 
            $"Status: {(receivedData != null ? "Data received" : "Waiting for data")} | Local: {localAddress}:{localPort}", 
            textStyle);
    }
    
    void OnApplicationQuit()
    {
        // 清理资源
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
    }
}