using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 经纬度 ↔ 游戏坐标映射。打包后改 JSON 即可，不必重编。
/// 查找顺序：
/// 1. 可执行文件同目录 GeoMapping.json（打包后优先放这里）
/// 2. persistentDataPath/GeoMapping.json
/// 3. StreamingAssets/GeoMapping.json（工程内默认，打包进 *_Data/StreamingAssets）
/// 运行中改文件会自动热加载。
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
    public string ActiveConfigPath => configPath;

    private void Start()
    {
        ResolveConfigPath();
        LoadFromFile();
        if (applyOnStart)
            Apply();
    }

    private void Update()
    {
        if (!autoReloadOnChange || string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            return;

        var writeTime = File.GetLastWriteTime(configPath);
        if (writeTime != lastFileWrite)
        {
            LoadFromFile();
            Apply();
        }
    }

    private void ResolveConfigPath()
    {
        string besideExe = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, configFileName);
        string persistent = Path.Combine(Application.persistentDataPath, configFileName);
        string streaming = Path.Combine(Application.streamingAssetsPath, configFileName);

        if (File.Exists(besideExe))
            configPath = besideExe;
        else if (File.Exists(persistent))
            configPath = persistent;
        else
            configPath = streaming;
    }

    public void LoadFromFile()
    {
        if (string.IsNullOrEmpty(configPath))
            ResolveConfigPath();

        if (!File.Exists(configPath))
        {
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
                Debug.Log($"[GeoMappingConfig] Loaded from {configPath}: lon[{mapping.lonMin},{mapping.lonMax}] lat[{mapping.latMin},{mapping.latMax}] → x[{mapping.xMin},{mapping.xMax}] y[{mapping.yMin},{mapping.yMax}]");
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
            if (string.IsNullOrEmpty(configPath))
                ResolveConfigPath();

            var dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(configPath, FormatMappingJson(mapping), new UTF8Encoding(false));
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
            Debug.Log($"[GeoMappingConfig] Applied to PlaneDisplayController from {configPath}");
        }
        else
        {
            Debug.LogWarning("[GeoMappingConfig] PlaneDisplayController not found");
        }
    }

    [ContextMenu("Reload Config")]
    public void Reload()
    {
        ResolveConfigPath();
        LoadFromFile();
        Apply();
    }

    [ContextMenu("Save Current as Default")]
    public void SaveCurrent()
    {
        SaveToFile();
    }

    private static string FormatMappingJson(MappingData data)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"lonMin\": " + data.lonMin.ToString("0.####", inv) + ",");
        sb.AppendLine("  \"lonMax\": " + data.lonMax.ToString("0.####", inv) + ",");
        sb.AppendLine("  \"latMin\": " + data.latMin.ToString("0.####", inv) + ",");
        sb.AppendLine("  \"latMax\": " + data.latMax.ToString("0.####", inv) + ",");
        sb.AppendLine("  \"xMin\": " + data.xMin.ToString("0.###", inv) + ",");
        sb.AppendLine("  \"xMax\": " + data.xMax.ToString("0.###", inv) + ",");
        sb.AppendLine("  \"yMin\": " + data.yMin.ToString("0.###", inv) + ",");
        sb.AppendLine("  \"yMax\": " + data.yMax.ToString("0.###", inv));
        sb.AppendLine("}");
        return sb.ToString();
    }
}
