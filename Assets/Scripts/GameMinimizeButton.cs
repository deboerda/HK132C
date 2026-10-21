using UnityEngine;

public class GameMinimizeButton : MonoBehaviour
{
    // 最小化游戏
    public void MinimizeGame()
    {
        // 在不同平台上最小化游戏窗口
        #if UNITY_STANDALONE_WIN
        // Windows平台
        ShowWindow(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle, SW_MINIMIZE);
        #elif UNITY_STANDALONE_OSX
        // macOS平台
        System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        // macOS的最小化实现可能需要使用AppleScript或其他方法
        #elif UNITY_STANDALONE_LINUX
        // Linux平台
        System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        // Linux的最小化实现可能需要使用特定的窗口管理器命令
        #endif
        
        Debug.Log("Game minimized");
    }
    
    // Windows API常量
    private const int SW_MINIMIZE = 6;
    
    // 导入Windows API函数
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);
}