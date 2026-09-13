using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PanelManager : MonoBehaviour
{
    [Header("面板配置")]
    public GameObject dataConfigPanel; // 数据配置面板
    public GameObject dashboardPanel; // 仪表盘和姿态面板
    public GameObject planeParamsPanel; // 飞机参数面板
    public GameObject sensorPanel; // 传感器面板

    [Header("按钮配置")]
    public Button dataConfigButton; // 数据配置面板按钮
    public Button dashboardButton; // 仪表盘和姿态面板按钮
    public Button planeParamsButton; // 飞机参数面板按钮
    public Button sensorButton; // 传感器面板按钮

    [Header("初始设置")]
    public bool showDataConfigPanel = true; // 是否默认显示数据配置面板
    public bool showDashboardPanel = false; // 是否默认显示仪表盘和姿态面板
    public bool showPlaneParamsPanel = false; // 是否默认显示飞机参数面板
    public bool showSensorPanel = false; // 是否默认显示传感器面板

    private void Start()
    {
        // 初始化按钮事件
        if (dataConfigButton != null)
            dataConfigButton.onClick.AddListener(ShowDataConfigPanel);
        
        if (dashboardButton != null)
            dashboardButton.onClick.AddListener(ShowDashboardPanel);
        
        if (planeParamsButton != null)
            planeParamsButton.onClick.AddListener(ShowPlaneParamsPanel);

        if (sensorButton != null)
            sensorButton.onClick.AddListener(ShowSensorPanel);

        // 初始化面板显示状态
        UpdatePanelVisibility();
    }

    /// <summary>
    /// 显示数据配置面板，隐藏其他面板
    /// </summary>
    public void ShowDataConfigPanel()
    {
        showDataConfigPanel = true;
        showDashboardPanel = false;
        showPlaneParamsPanel = false;
        showSensorPanel = false;
        UpdatePanelVisibility();
    }

    /// <summary>
    /// 显示仪表盘和姿态面板，隐藏其他面板
    /// </summary>
    public void ShowDashboardPanel()
    {
        showDataConfigPanel = false;
        showDashboardPanel = true;
        showPlaneParamsPanel = false;
        showSensorPanel = false;
        UpdatePanelVisibility();
    }

    /// <summary>
    /// 显示飞机参数面板，隐藏其他面板
    /// </summary>
    public void ShowPlaneParamsPanel()
    {
        showDataConfigPanel = false;
        showDashboardPanel = false;
        showPlaneParamsPanel = true;
        showSensorPanel = false;
        UpdatePanelVisibility();
    }

    /// <summary>
    /// 显示传感器面板，隐藏其他面板
    /// </summary>
    public void ShowSensorPanel()
    {
        showDataConfigPanel = false;
        showDashboardPanel = false;
        showPlaneParamsPanel = false;
        showSensorPanel = true;
        UpdatePanelVisibility();
    }

    /// <summary>
    /// 更新面板的可见性
    /// </summary>
    private void UpdatePanelVisibility()
    {
        if (dataConfigPanel != null)
            dataConfigPanel.SetActive(showDataConfigPanel);
        
        if (dashboardPanel != null)
            dashboardPanel.SetActive(showDashboardPanel);
        
        if (planeParamsPanel != null)
            planeParamsPanel.SetActive(showPlaneParamsPanel);

        if (sensorPanel != null)
            sensorPanel.SetActive(showSensorPanel);
    }
}
