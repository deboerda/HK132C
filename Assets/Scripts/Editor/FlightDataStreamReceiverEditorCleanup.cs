#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FlightDataStreamReceiverEditorCleanup
{
    static FlightDataStreamReceiverEditorCleanup()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

        EditorApplication.quitting -= OnEditorQuitting;
        EditorApplication.quitting += OnEditorQuitting;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
        {
            CleanupReceiver($"play mode state: {state}");
        }
    }

    private static void OnBeforeAssemblyReload()
    {
        CleanupReceiver("assembly reload");
    }

    private static void OnEditorQuitting()
    {
        CleanupReceiver("editor quitting");
    }

    private static void CleanupReceiver(string reason)
    {
        try
        {
            FlightDataStreamReceiver.DisconnectAllReceivers();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FlightDataStreamReceiverEditorCleanup] Cleanup failed before {reason}: {ex.Message}");
        }
    }
}
#endif
