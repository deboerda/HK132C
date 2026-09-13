using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameCloseButton : MonoBehaviour
{
    // 关闭游戏
    public void CloseGame()
    {
        UnityEngine.Debug.Log("Game closing...");

        // 在编辑器模式下，退出播放模式
        #if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
        #else
        // 在构建版本中，退出应用
        Application.Quit();
        #endif

        // 立即返回，避免执行后续代码
        return;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.isPressed)
        {
            Application.Quit();
        }
    }

    void OnApplicationQuit() //退出时的执行函数
    {
        //只在构建版本中杀死进程，编辑器模式下不执行
        #if !UNITY_EDITOR
        //杀死当前进程
        System.Diagnostics.Process.GetCurrentProcess().Kill();
        #endif
    }
}