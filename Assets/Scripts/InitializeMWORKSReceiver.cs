using UnityEngine;

public class InitializeMWORKSReceiver : MonoBehaviour
{
    void Awake()
    {
        // 检查是否已存在MWORKSDataReceiver实例
        if (UnityEngine.Object.FindAnyObjectByType<MWORKSDataReceiver>() == null)
        {
            // 创建新的GameObject
            GameObject receiverObj = new GameObject("MWORKSDataReceiver");
            
            // 添加MWORKSDataReceiver脚本
            receiverObj.AddComponent<MWORKSDataReceiver>();
            
            // 确保对象在场景切换时不被销毁            DontDestroyOnLoad(receiverObj);
            
            Debug.Log("MWORKSDataReceiver GameObject created and initialized");
        }
    }
}