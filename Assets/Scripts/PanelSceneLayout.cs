using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 面板标题/数据列在 Scene 里拖位置：关掉行内 HorizontalLayoutGroup，
/// 运行时只改 Text，克隆场景里的行模板来复制列坐标。
/// </summary>
public static class PanelSceneLayout
{
    public static void FreezeRowLayout(RectTransform row)
    {
        if (row == null)
            return;
        var horizontal = row.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
            horizontal.enabled = false;
        var grid = row.GetComponent<GridLayoutGroup>();
        if (grid != null)
            grid.enabled = false;
        var fitter = row.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;
    }

    public static RectTransform FindNamed(RectTransform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
            return null;
        Transform existing = parent.Find(childName);
        return existing as RectTransform;
    }

    public static RectTransform FindRowTemplate(RectTransform contentRoot, RectTransform explicitTemplate)
    {
        if (explicitTemplate != null)
            return explicitTemplate;
        if (contentRoot == null)
            return null;

        RectTransform named = FindNamed(contentRoot, "RowTemplate") ?? FindNamed(contentRoot, "DataRowTemplate");
        if (named != null)
            return named;

        for (int i = 0; i < contentRoot.childCount; i++)
        {
            Transform child = contentRoot.GetChild(i);
            if (child == null)
                continue;
            string name = child.name;
            if (name.IndexOf("Header", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (name.IndexOf("Row", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return child as RectTransform;
        }

        return contentRoot.childCount > 0 ? contentRoot.GetChild(0) as RectTransform : null;
    }

    public static RectTransform GetOrCloneRow(RectTransform contentRoot, string rowName, RectTransform template)
    {
        RectTransform existing = FindNamed(contentRoot, rowName);
        if (existing != null)
        {
            FreezeRowLayout(existing);
            return existing;
        }

        if (template == null || contentRoot == null)
            return null;

        GameObject clone = Object.Instantiate(template.gameObject, contentRoot);
        clone.name = rowName;
        clone.SetActive(true);
        var rect = clone.GetComponent<RectTransform>();
        FreezeRowLayout(rect);
        return rect;
    }

    public static bool IsTemplateName(string name)
    {
        return name == "RowTemplate" || name == "DataRowTemplate";
    }
}
