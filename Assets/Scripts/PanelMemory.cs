using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>数据配置 / 传感器 / 航迹的跨次记忆（persistentDataPath JSON）。</summary>
public static class PanelMemory
{
    private const string FileName = "HK132PanelMemory.json";

    [Serializable]
    public class Color4
    {
        public float r, g, b, a = 1f;
        public static Color4 From(Color c) => new Color4 { r = c.r, g = c.g, b = c.b, a = c.a };
        public Color ToColor() => new Color(r, g, b, a);
    }

    [Serializable]
    public class RuleItem
    {
        public string fieldKey;
        public string rule;
        public bool hideMarker;
        public Color4 color;
    }

    [Serializable]
    public class TrackRules
    {
        public string trackId;
        public List<RuleItem> entries = new List<RuleItem>();
    }

    [Serializable]
    public class SensorItem
    {
        public string key;
        public float range = -1f;
        public Color4 color;
        public bool visible = true;
        public bool hasColor;
    }

    [Serializable]
    public class TrailItem
    {
        public string trackId;
        public Color4 color;
        public bool visible = true;
        public bool hasColor;
    }

    [Serializable]
    public class Store
    {
        public List<TrackRules> tracks = new List<TrackRules>();
        public float defaultDetectRange = -1f;
        public List<SensorItem> sensors = new List<SensorItem>();
        public List<TrailItem> trails = new List<TrailItem>();
    }

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static Store Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new Store();
            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
                return new Store();
            var store = JsonUtility.FromJson<Store>(json);
            return store ?? new Store();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PanelMemory] Load failed: " + e.Message);
            return new Store();
        }
    }

    public static void Save(Store store)
    {
        if (store == null)
            return;
        try
        {
            string dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonUtility.ToJson(store, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PanelMemory] Save failed: " + e.Message);
        }
    }
}
