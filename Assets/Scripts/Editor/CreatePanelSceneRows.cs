#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CreatePanelSceneRows
{
    [MenuItem("HK132/Create Panel Header And Sample Rows")]
    public static void CreateAll()
    {
        int n = 0;
        n += SetupAllOfType<PlaneInfoGrid>(new[]
        {
            Col("Rank", "#", 36f),
            Col("PlaneId", "飞机编号", 88f),
            Col("Longitude", "经度", 86f),
            Col("Latitude", "纬度", 86f),
            Col("Altitude", "高度", 70f),
        });
        n += SetupAllOfType<AlarmPanel>(new[]
        {
            Col("PlaneId", "飞机编号", 88f),
            Col("Alarm", "告警信息", 70f),
            Col("Time", "时间", 120f),
            Col("State", "状态", 70f),
        });
        n += SetupAllOfType<SensorPanel>(new[]
        {
            Col("Rank", "#", 22f),
            Col("PlaneId", "编号", 62f),
            Col("Detector", "类型", 40f),
            Col("Range", "距离", 56f),
            Col("Yaw", "左右", 40f),
            Col("Pitch", "上下", 40f),
            Col("Center", "中心", 44f),
            Col("SensorColor", "颜色", 32f),
            Col("SensorShow", "显示", 40f),
        });
        n += SetupAllOfType<PlaneDifferenceRankingPanel>(new[]
        {
            Col("Rank", "#", 36f),
            Col("PlaneId", "飞机编号", 88f),
            Col("DistanceDelta", "距离差", 78f),
            Col("AltitudeDelta", "高度差", 70f),
            Col("HeadingDelta", "方位角差", 78f),
        });
        n += SetupAllOfType<DataRuleConfigPanel>(new[]
        {
            Col("Rank", "#", 22f),
            Col("TrackId", "编号", 58f),
            Col("VarName", "变量名", 78f),
            Col("Value", "当前值", 70f),
            Col("Rule", "规则", 50f),
            Col("Status", "状态", 36f),
            Col("MarkerColor", "颜色", 28f),
            Col("MarkerShow", "显示", 32f),
        });

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[CreatePanelSceneRows] Prepared header + RowTemplate on {n} panel(s). Drag cells in Scene before Play.");
    }

    private static (string name, string title, float width) Col(string name, string title, float width)
    {
        return (name, title, width);
    }

    private static int SetupAllOfType<T>( (string name, string title, float width)[] columns) where T : MonoBehaviour
    {
        int count = 0;
        var panels = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        for (int i = 0; i < panels.Length; i++)
        {
            if (SetupPanel(panels[i], columns))
                count++;
        }
        return count;
    }

    private static bool SetupPanel(MonoBehaviour panel, (string name, string title, float width)[] columns)
    {
        if (panel == null)
            return false;

        var so = new SerializedObject(panel);
        var headerRoot = so.FindProperty("headerRoot")?.objectReferenceValue as RectTransform;
        var contentRoot = so.FindProperty("contentRoot")?.objectReferenceValue as RectTransform;
        var panelRect = panel.transform as RectTransform;
        if (panelRect == null)
            return false;

        if (headerRoot == null)
            headerRoot = FindOrCreateRect(panelRect, "HeaderRoot");
        if (contentRoot == null)
        {
            var scroll = panelRect.Find("Scroll View");
            contentRoot = scroll != null
                ? (scroll.Find("Viewport/ContentRoot") as RectTransform) ?? (scroll.Find("ContentRoot") as RectTransform)
                : null;
            if (contentRoot == null)
                contentRoot = FindOrCreateRect(panelRect, "ContentRoot");
        }

        Font font = panel.GetType().GetField("font", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(panel) as Font;
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Undo.RecordObject(panel, "Create panel sample rows");
        var header = BuildRow(headerRoot, "Header", columns, true, font);
        var template = BuildRow(contentRoot, "RowTemplate", columns, false, font);
        FillSampleTexts(template, columns);

        so.FindProperty("headerRoot").objectReferenceValue = headerRoot;
        so.FindProperty("contentRoot").objectReferenceValue = contentRoot;
        var templateProp = so.FindProperty("rowTemplate");
        if (templateProp != null)
            templateProp.objectReferenceValue = template;
        var preserve = so.FindProperty("preserveSceneLayout");
        if (preserve != null)
            preserve.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(panel);
        return true;
    }

    private static RectTransform FindOrCreateRect(RectTransform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null)
            return existing;
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 32f);
        return rt;
    }

    private static RectTransform BuildRow(RectTransform parent, string rowName, (string name, string title, float width)[] columns, bool header, Font font)
    {
        var row = parent.Find(rowName) as RectTransform;
        if (row == null)
        {
            var go = new GameObject(rowName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(go, "Create " + rowName);
            go.transform.SetParent(parent, false);
            row = go.GetComponent<RectTransform>();
        }

        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = Vector2.zero;
        row.sizeDelta = new Vector2(0f, header ? 32f : 30f);

        var img = row.GetComponent<Image>();
        if (img == null)
            img = Undo.AddComponent<Image>(row.gameObject);
        img.color = header
            ? new Color(0.08f, 0.1f, 0.13f, 0.95f)
            : new Color(0.12f, 0.14f, 0.18f, 0.82f);
        img.raycastTarget = false;

        var le = row.GetComponent<LayoutElement>();
        if (le == null)
            le = Undo.AddComponent<LayoutElement>(row.gameObject);
        le.ignoreLayout = header;
        le.minHeight = header ? 0f : 30f;
        le.preferredHeight = header ? 0f : 30f;

        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
            hlg.enabled = false;

        float x = 8f;
        float h = header ? 28f : 26f;
        for (int i = 0; i < columns.Length; i++)
        {
            EnsureCell(row, columns[i].name, header ? columns[i].title : SampleValue(columns[i].name), x, columns[i].width, h, font, header);
            x += columns[i].width + 8f;
        }

        EditorUtility.SetDirty(row.gameObject);
        return row;
    }

    private static void FillSampleTexts(RectTransform row, (string name, string title, float width)[] columns)
    {
        if (row == null)
            return;
        for (int i = 0; i < columns.Length; i++)
        {
            var t = row.Find(columns[i].name)?.GetComponent<Text>();
            if (t != null)
                t.text = SampleValue(columns[i].name);
        }
    }

    private static string SampleValue(string cellName)
    {
        switch (cellName)
        {
            case "Rank": return "1";
            case "PlaneId":
            case "TrackId": return "UAV-001";
            case "Longitude": return "103.946975";
            case "Latitude": return "30.578026";
            case "Altitude": return "300.0";
            case "Alarm": return "无";
            case "Time": return "08:00:00.000";
            case "State":
            case "Status": return "上升";
            case "Detector": return "光电";
            case "RangeType": return "扇形";
            case "Range": return "50000m";
            case "Yaw": return "0.0";
            case "Pitch": return "0.0";
            case "Center": return "-";
            case "DistanceDelta": return "15.70 km";
            case "AltitudeDelta": return "0.0 m";
            case "HeadingDelta": return "+180.0 deg";
            case "VarName": return "lng";
            case "Value": return "103.946975";
            case "Rule": return "";
            default: return cellName;
        }
    }

    private static void EnsureCell(RectTransform row, string name, string text, float x, float width, float height, Font font, bool header)
    {
        Transform existing = row.Find(name);
        GameObject go = existing != null ? existing.gameObject : null;
        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(Text));
            Undo.RegisterCreatedObjectUndo(go, "Create cell " + name);
            go.transform.SetParent(row, false);
        }

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(width, height);

        var ui = go.GetComponent<Text>();
        if (ui == null)
            ui = Undo.AddComponent<Text>(go);
        if (font != null)
            ui.font = font;
        ui.fontSize = header ? 13 : 12;
        ui.color = header ? new Color(0.66f, 0.74f, 0.82f, 1f) : new Color(0.92f, 0.96f, 1f, 1f);
        ui.alignment = name == "Time" ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
        ui.horizontalOverflow = HorizontalWrapMode.Overflow;
        ui.verticalOverflow = VerticalWrapMode.Truncate;
        ui.raycastTarget = false;
        ui.text = text;

        var cellLe = go.GetComponent<LayoutElement>();
        if (cellLe != null)
            Object.DestroyImmediate(cellLe);
    }
}
#endif
