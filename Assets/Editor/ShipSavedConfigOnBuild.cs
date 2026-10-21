using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 打包前把调试时保存的配置拷进 StreamingAssets，随包发出。
/// </summary>
public class ShipSavedConfigOnBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        string persistent = Application.persistentDataPath;
        string streaming = Application.streamingAssetsPath;
        if (!Directory.Exists(streaming))
            Directory.CreateDirectory(streaming);

        CopyIfNewer(Path.Combine(persistent, "HK132PanelMemory.json"), Path.Combine(streaming, "HK132PanelMemory.json"));
        CopyIfNewer(Path.Combine(persistent, "GeoMapping.json"), Path.Combine(streaming, "GeoMapping.json"));
        CopyIfNewer(Path.Combine(persistent, "FieldKeyMapping.json"), Path.Combine(streaming, "FieldKeyMapping.json"));
        AssetDatabase.Refresh();
        Debug.Log("[ShipSavedConfig] Copied saved debug configs into StreamingAssets before build.");
    }

    private static void CopyIfNewer(string source, string dest)
    {
        if (!File.Exists(source))
            return;
        if (File.Exists(dest) && File.GetLastWriteTimeUtc(dest) >= File.GetLastWriteTimeUtc(source))
            return;
        File.Copy(source, dest, true);
    }
}
