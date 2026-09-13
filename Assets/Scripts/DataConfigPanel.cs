using UnityEngine;
using UnityEngine.UI;

public class DataConfigPanel : MonoBehaviour
{
    [Header("雷达按钮配置")]
    public Button circleRadarButton;
    public Button sectorRadarButton;
    public Button coneRadarButton;

    [Header("雷达状态")]
    [SerializeField] private string _currentRadarShape = "Circle";

    public static DataConfigPanel Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (circleRadarButton != null)
            circleRadarButton.onClick.AddListener(OnCircleRadarButtonClick);

        if (sectorRadarButton != null)
            sectorRadarButton.onClick.AddListener(OnSectorRadarButtonClick);

        if (coneRadarButton != null)
            coneRadarButton.onClick.AddListener(OnConeRadarButtonClick);
    }

    public void OnCircleRadarButtonClick()
    {
        SetRadarShape("Circle");
    }

    public void OnSectorRadarButtonClick()
    {
        SetRadarShape("Sector");
    }

    public void OnConeRadarButtonClick()
    {
        SetRadarShape("Cone");
    }

    public void SetRadarShape(string shapeName)
    {
        _currentRadarShape = shapeName;
        Debug.Log($"Radar shape set to: {_currentRadarShape}");
    }
}
