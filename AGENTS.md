# HK132C 项目记忆（自 dsh 会话同步，2026-09-14）

来源：`~/.dsh/sessions/--E-UNITYproject-HK132C--/session-0f42fbd8…`（2026-09-06 ~ 09-10，25 个工作轮次）。

## 项目概况

- Unity(团结) 项目，Unity 6000.5.7f1 / URP；主场景 `Assets/Scenes/HK132.unity`。
- 定位：飞行数据三维可视化端。数据端应用在 `E:\UNITYproject\HK132C\FlyParamData`（含位置表/时刻表/周期表 CSV 与配置），演示发送脚本 `tcp_3d_demo.py`，协议文档 `三维端通讯协议.docx`。
- UI 主体：World Space `MainCanvas`（RenderMode=2）；`飞机UI/PlaneCanvas`（扇形/圆形/锥形雷达、面板）默认 inactive，实际显示的飞机图标是 `MainCanvas/地图/飞机` 及运行时复制对象。

## 关键模块与文件

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/PlaneDisplayController.cs` | 飞机图标、轨迹 UI、地图坐标映射（`ConvertToAnchoredPosition`）、`ClearAllTrails()`、启动时创建 MapOverlay 覆盖层 |
| `Assets/Scripts/RadarDetectionSystem.cs` / `RadarPolylineGraphic.cs` / `Assets/Shaders/UIOverlayAlways.shader` | 雷达图形与 UI 折线（ZTest Always + `_ClipRect` 裁剪） |
| `Assets/Scripts/MapZoomPanController.cs` | 地图缩放/平移（图片查看器式），zoom≥1 并钳制 |
| `Assets/Scripts/FlightDataStreamReceiver.cs` | 数据接收与解析（TCP/UDP JSON），字段→变量映射 |
| `Assets/Scripts/FlightPlaybackPanel.cs` / `FlightProgressController.cs` | 回放控制、进度条/滑块、异常打点 |
| `Assets/Scripts/DataRuleConfigPanel.cs` / `DataConfigPanel.cs` | 数据规则配置与实时值显示 |
| `Assets/StreamingAssets/FieldKeyMapping.json` + `FieldKeyMappingConfig.cs` | 接收字段→内部变量映射 |

## 已解决的结论（勿回退）

1. **UI 遮挡（雷达/轨迹被飞机图标盖住）**：根因是 World Space MainCanvas + 飞机 Image 用 `ZWrite On` 的 2D Lit 材质（`飞行轨迹.mat`，z=-2），提高 Canvas sortingOrder 无效（LineRenderer 也吃不到 UI 排序）。最终方案：轨迹/航迹改为 `RadarPolylineGraphic`（MaskableGraphic）+ `UI/OverlayAlways`（ZTest Always）shader，覆盖层画在飞机之上；已实测确认修复。
2. **地图缩放/平移**：不用第二台摄像机 + RenderTexture（minimapcamera 方案作废，minimapcamera/RawImage 已 inactive）——因为 `地图` 挂在 UI 层 Canvas 下，剔 UI 会连地图一起剔掉。方案：`MainCanvas/MapViewport`（固定窗口 + RectMask2D 裁剪）不动，缩放/平移作用于内部 `地图` 整层（底图+飞机+雷达+轨迹一起动），经纬度↔游戏坐标映射不受影响。
3. **地图越界裁剪**：裁剪由 MapViewport 统一负责（RectMask2D，不能再用 Stencil Mask + 自带 UISprite——四角透明会裁出缺口）。
4. **停止按钮**：`FlightPlaybackPanel.OnStopClicked()` → `ClearAllTrails()` + 清异常打点 + 清超限状态；开始播放不再清打点。
5. **规则判断**：`EvaluateRule` 真实比较（`=0`/`==0`/`0`/`>0`/`a~b`），未知规则不再默认返回 true；超限按"上升沿"打点，持续超限按用户要求改为持续打点（可连成长块）。
6. **异常打点**：坐标用 `progressSlider.normalizedValue`，挂在 `handleRect.parent`（不挂会变长的 Fill），宽约 1 像素，不设 FIFO 上限，只有停止才清除；每条规则可选打点颜色（8 色）与显/隐，显隐即时作用于已打的点。
7. **时间轴**：滑块/进度/时间按飞行数据真实时间计算；刻度尺左端=数据 `startTime`、右端=`endTime`（来自数据端任务时间尺度），不再是固定 `0s`。
8. **轨迹断线**：拖动进度条瞬移期间（约 0.45s）完全不追加实时轨迹，位移过大也断开；只绘制接收到的连续位置轨迹。

## 进行中 / 未完事项

- **DATA_FIELDS 适配**（09-10 最后一个话题）：数据端新增 `DATA_FIELDS` 字段传输表头；约定 Unity 端不再做字段别名匹配，直接按数据端发来的 JSON key（`trackId`、`planId`、`flightTime`、`detector_type` 等）取值。`FlightDataStreamReceiver.cs` 改造在 dsh 会话结束时进行中，需验证。
- 两个长期存在的序列化警告（`UDPDataReceiver.cs`、`FlightDataStreamClient.cs`），与功能无关。

## 工作纪律（dsh 会话中确立的偏好）

- **不得把"编译通过"或"无真实数据时的运行检查"当作 UI/遮挡问题已修复**；必须在真实雷达/轨迹数据下验证实际可见结果后才报告完成。
- Play Mode 下禁止场景写操作（`ensure_scene_saved` 会被拒绝）；C# 改动需退出 Play Mode 才能生效，注意编译时机。
- 实测发现问题后，优先排查真实数据链路（`FlightDataStreamReceiver` / `LatestTaskTimeInfo`）而非 UI 层表象。
