using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private static readonly object _lock = new object();
    private readonly Queue<System.Action> _actions = new Queue<System.Action>();
    private static bool _isInitialized = false;

    // 在Awake中初始化实例
    void Awake()
    {
        lock (_lock)
        {
            if (_instance == null)
            {
                _instance = this;
                _isInitialized = true;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
    }

    public static UnityMainThreadDispatcher Instance()
    {
        lock (_lock)
        {
            if (!_isInitialized)
            {
                // 如果还没有初始化，创建一个GameObject并添加组件
                GameObject obj = new GameObject("UnityMainThreadDispatcher");
                _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                _isInitialized = true;
                DontDestroyOnLoad(obj);
            }
        }
        return _instance;
    }

    void Update()
    {
        if (_actions == null)
        {
            return;
        }
        
        lock (_lock)
        {
            try
            {
                while (_actions.Count > 0)
                {
                    System.Action action = _actions.Dequeue();
                    if (action != null)
                    {
                        try
                        {
                            action.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("Error invoking action: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error processing action queue: " + ex.Message);
            }
        }
    }

    public void Enqueue(System.Action action)
    {
        if (action == null)
        {
            return;
        }
        
        lock (_lock)
        {
            try
            {
                _actions.Enqueue(action);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error enqueuing action: " + ex.Message);
            }
        }
    }
}