using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 经纬度到游戏坐标的映射配置，支持从外部JSON 文件加载。/// 配置文件路径：Assets/StreamingAssets/GeoMapping.json
/// </summary>
public class GeoMappingConfig : MonoBehaviour
{
    [Serializable]
    public class MappingData
    {
        public float lonMin = 103.6f;
        public float lonMax = 104.65f;
        public float latMin = 30.5f;
        public float latMax = 30.9f;
        public float xMin = -570f;
        public float xMax = 580f;
        public float yMin = -285f;
        public float yMax = 290f;
    }

    [Header("Current Mapping")]
    [SerializeField] private MappingData mapping = new MappingData();

    [Header("Auto Apply")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool autoReloadOnChange = true;

    [Header("Config File")]
    [SerializeField] private string configFileName = "GeoMapping.json";

    private string configPath;
    private DateTime lastFileWrite;
    private PlaneDisplayController planeDisplayController;

    public MappingData Mapping => mapping;

    private void Start()
    {
        configPath = Path.Combine(Application.streamingAssetsPath, configFileName);
        LoadFromFile();
        if (applyOnStart) Apply();
    }

    private void Update()
    {
        if (autoReloadOnChange && File.Exists(configPath))
        {
            var writeTime = File.GetLastWriteTime(configPath);
            if (writeTime != lastFileWrite)
            {
                LoadFromFile();
                Apply();
            }
        }
    }

    public void LoadFromFile()
    {
        if (string.IsNullOrEmpty(configPath))
            configPath = Path.Combine(Application.streamingAssetsPath, configFileName);

        if (!File.Exists(configPath))
        {
            // Create default file
            SaveToFile();
            return;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var loaded = JsonUtility.FromJson<MappingData>(json);
            if (loaded != null)
            {
                mapping = loaded;
                lastFileWrite = File.GetLastWriteTime(configPath);
                Debug.Log($"[GeoMappingConfig] Loaded from {configPath}: lon[{mapping.lonMin},{mapping.lonMax}] lat[{mapping.latMin},{mapping.latMax}] →x[{mapping.xMin},{mapping.xMax}] y[{mapping.yMin},{mapping.yMax}]");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[GeoMappingConfig] Failed to load: {e.Message}");
        }
    }

    public void SaveToFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonUtility.ToJson(mapping, true);
            File.WriteAllText(configPath, json);
            lastFileWrite = File.GetLastWriteTime(configPath);
            Debug.Log($"[GeoMappingConfig] Saved to {configPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GeoMappingConfig] Failed to save: {e.Message}");
        }
    }

    public void Apply()
    {
        if (planeDisplayController == null)
            planeDisplayController = FindAnyObjectByType<PlaneDisplayController>();

        if (planeDisplayController != null)
        {
            planeDisplayController.SetLongitudeRange(mapping.lonMin, mapping.lonMax);
            planeDisplayController.SetLatitudeRange(mapping.latMin, mapping.latMax);
            planeDisplayController.SetGameRange(mapping.xMin, mapping.xMax, mapping.yMin, mapping.yMax);
            Debug.Log("[GeoMappingConfig] Applied to PlaneDisplayController");
        }
        else
        {
            Debug.LogWarning("[GeoMappingConfig] PlaneDisplayController not found");
        }
    }

    [ContextMenu("Reload Config")]
    public void Reload()
    {
        LoadFromFile();
        Apply();
    }

    [ContextMenu("Save Current as Default")]
    public void SaveCurrent()
    {
        SaveToFile();
    }
}
