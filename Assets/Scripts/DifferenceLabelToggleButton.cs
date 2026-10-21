using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 三维标签显隐按钮：放在「轨迹按钮」正下方，只在飞行视图显示。
/// </summary>
public class DifferenceLabelToggleButton : MonoBehaviour
{
    private static DifferenceLabelToggleButton instance;

    [SerializeField] private Text buttonText;
    [SerializeField] private FlightSceneView sceneView;

    private void Awake()
    {
        instance = this;
        if (buttonText == null)
            buttonText = GetComponentInChildren<Text>(true);
        BindClick();
        if (Application.isPlaying)
            SyncVisibility();
        RefreshLabel();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void ToggleLabels()
    {
        var view = ResolveSceneView();
        if (view != null)
            view.ToggleLabels();
        RefreshLabel();
    }

    public static void SyncVisibility()
    {
        if (!Application.isPlaying)
            return;
        if (instance == null)
            instance = FindAnyObjectByType<DifferenceLabelToggleButton>(FindObjectsInactive.Include);
        if (instance == null)
            return;
        var switcher = ViewModeSwitcher.Instance;
        bool show = switcher != null && switcher.Current == ViewModeSwitcher.ViewMode.Flight3D;
        instance.gameObject.SetActive(show);
        if (show)
            instance.RefreshLabel();
    }

    private void BindClick()
    {
        var button = GetComponent<Button>();
        if (button == null)
            return;
        // 场景里的持久化 OnClick 不会被 RemoveAllListeners 清掉，会和运行时监听叠成点一次开关两次。
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(ToggleLabels);
        if (buttonText != null)
            buttonText.raycastTarget = false;
    }

    private FlightSceneView ResolveSceneView()
    {
        if (sceneView == null)
            sceneView = FlightSceneView.Active;
        if (sceneView == null)
            sceneView = FindAnyObjectByType<FlightSceneView>(FindObjectsInactive.Include);
        return sceneView;
    }

    private void RefreshLabel()
    {
        if (buttonText == null)
            buttonText = GetComponentInChildren<Text>(true);
        var view = ResolveSceneView();
        if (buttonText == null)
            return;
        bool visible = view == null || view.LabelsVisible;
        buttonText.text = visible ? "隐藏标签" : "显示标签";
    }

}
