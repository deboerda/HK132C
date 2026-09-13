using UnityEngine;
using UnityEngine.UI;

public class TrailToggleButton : MonoBehaviour
{
    [SerializeField] private PlaneDisplayController _planeDisplayController;
    [SerializeField] private Text _buttonText;
    
    private void Start()
    {
        if (_planeDisplayController == null)
        {
            _planeDisplayController = Object.FindAnyObjectByType<PlaneDisplayController>();
        }

        // 更新按钮文本
        UpdateButtonText();
    }
    
    // 切换轨迹显示状态
    public void ToggleTrails()
    {
        if (_planeDisplayController == null)
        {
            _planeDisplayController = Object.FindAnyObjectByType<PlaneDisplayController>();
        }

        if (_planeDisplayController != null)
        {
            _planeDisplayController.ToggleTrails();
            UpdateButtonText();
        }
        else
        {
            Debug.LogWarning("PlaneDisplayController not found.");
        }
    }
    
    // 更新按钮文本
    private void UpdateButtonText()
    {
        if (_buttonText != null && _planeDisplayController != null)
        {
            _buttonText.text = _planeDisplayController.IsTrailVisible ? "隐藏轨迹" : "显示轨迹";
        }
    }
}
