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
| `Assets/Scripts/RadarDetectionSystem.cs` / `RadarPolylineGraphic.cs` / `Assets/Shaders/UIOverlayAlways.shader` | 传感器扫描检测（10 变量模型，见结论 11）与 UI 折线（ZTest Always + `_ClipRect` 裁剪） |
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
9. **DATA_FIELDS 统一 key 模式（2026-09-19 完成并验证）**：数据端开场人工点击发送 `DATA_FIELDS {"fields":{key:[别名...]}}`；`FlightDataStreamReceiver.ParseDataFields` 解析进 `DataFields`（key 大小写不敏感，`DataFieldsVersion` 供 UI 变更检测）。`DataRuleConfigPanel`（数据配置面板）以收到的 JSON key 为唯一变量字典：未收到时内置 `DefaultFields` 兜底展示常用变量；常用变量排前（`CommonVariableKeys`）；"+" 添加变量弹选择器只列合法 key（`key（展示名）`）；行内变量名输入自动补全限定字典范围（key/展示名/别名均可解析，旧内部名经 `LegacyKeyMap` 迁移）；字典外 key 显示"变量无效"且不参与规则判定。编辑模式反射实测：新版 21 个 key 全部解析通过。
10. **编译警告清零（2026-09-19）**：删除 `PlaneDisplayController.trackLineSortingOrder`（CS0414，排序方案已被 MapOverlay 取代）；`UDPDataReceiver.planeData` 加 `[NonSerialized]`（UAC1009）；`FlightDataStreamClient.CommandEnvelope` 去掉 `[Serializable]`（UAC1001，纯 Json.NET DTO，不能给 data 字段标 NonSerialized）。
11. **传感器检测 10 变量模型（2026-09-19 完成替换，几何数值验证 13/13 通过）**：`RadarDetectionSystem.cs` 完全重写。扫描锥由 10 变量叠加决定——位置 lng/lat/alt；姿态 heading/pitch(上正)/roll(默认右压为正，`invertRollSign` 可翻转)；传感器 yaw_angle(左右偏转，左正右负)、pitch_angle(上下偏转，上正下负)、yaw_scan_angle/pitch_scan_angle(扫描总范围，40=>±20)。波束指向：方位=heading−yaw_angle、俯仰=pitch+pitch_angle，横滚绕波束轴扭转锥体。判定=角度空间矩形（方位偏角≤yaw_scan/2 且俯仰偏角≤pitch_scan/2，asin/atan2 计算，非切空间）。**10 变量不含距离**，投影半径与检测门限统一用探测距离配置：`RadarDetectionSystem.MaxDetectRangeMeters`/`SetMaxDetectRangeMeters()`（Inspector 默认 50000，`SensorPanel` 探测距离配置栏运行时可调并 PlayerPrefs 持久化，键 `SensorPanel.MaxDetectRangeMeters`；面板行内"探测范围"列显示该配置值）。二维投影=锥内方向按世界方位角分箱取 maxRange·cos(世界仰角) 最大值→外弧随横滚/俯仰不对称收缩。**已删除旧概念**：detectRange/rangeType(扇形锥形)/centerPoint(机头机尾偏移)/detectorType 白名单；传感器有效性=yaw_scan>0 且 pitch_scan>0，radar_status(无效/关闭/故障)仍作开关。SensorPanel 依赖的 GetSensorColor/SetSensorVisible 等 API 按 trackId|detectorType 键保留。`DataConfigPanel` 的圆形/扇形/锥形按钮已与检测逻辑无关。
12. **EVENT_TABLE_DATA / CYCLE_TABLE_DATA（2026-09-19）**：事件表走 `EVENT_TABLE_DATA`（AlarmPanel 读 state/alarm/flightTime 字符串，所以能刷）。周期表实测：`CYCLE_TABLE_DATA {flightTime, trackId:"UAV-00x", records:{lng, lat, **hight**, heading, pitch, roll, ...}}`。DATA_FIELDS 以运行时手动发送为准。`hight` 是独立 json key，飞机高度内部映射到 `alt`。面板走 `TryGetFieldValue` 字段袋。
    **【对照实测截图】** 告警时间已到 `08:00:00.205566`（6 位小数），进度条仍 `0.0%`：旧 `TryParseExact(..., fff)` 解析失败，时间轴不走；外层 `data.flightTime` 有时停在任务起点，必须优先用 `records`/`flightStates` 内层时间。事件表后续帧常只带 roll/alarm，增量合并会只更新横滚——周期表 `hight/lng/heading` 必须每帧写回 TrackPosition。

## 进行中 / 未完事项

- **数据端 data_fields.json 曾是旧版（已同步）**：`0918flydata\config\data_fields.json` 是新版（21 字段，含 `center_point1`/`yaw_scan_angle`/`pitch_scan_angle`，另有 `center_point` 与 `center_point1` 两个"中心点"）；`FlyParamData\config\data_fields.json` 原为旧版（含 range_type/detect_range/cycleId、缺三个新字段），已用新版覆盖，旧文件备份为 `data_fields.json.bak-20260919`。若数据端 exe 实际从其他目录读配置，需再确认。
- **数据端表头解析缺陷**：data_fields.json 中 `yaw_angle`/`pitch_angle`/`center_point` 的别名含断行碎片（"yaw_a"、"gle"、"ce"、"ter_poi"、"t"），是数据端列名换行解析产物；Unity 端仅做精确别名匹配、展示名取首个可读中文别名，不受影响，但建议数据端修正源表头。
- **旧字段淘汰**：`range_type`/`detect_range`/`cycleId` 不在新版 DATA_FIELDS 中，收到后会从面板字典消失（相关行显示"变量无效"）；若业务仍需要，让数据端把这三项加回 data_fields.json。
- `Assets/StreamingAssets/FlightDataKeyMapping.json`、`FieldKeyMapping.json` + `FieldKeyMappingConfig.cs` 已无代码消费者（遗留），可删可留。
- 待实测收尾：数据端点击发送 DATA_FIELDS 后，确认控制台出现 "DATA_FIELDS received: 21 json keys (...)"，且面板"+"选择器列出 21 个变量、行内展示名切换为数据端中文别名。
- **传感器 10 变量模型待真实数据验证**：几何判定已数值验证（13/13），探测距离配置栏链路已验证（11/11：输入→雷达生效→PlayerPrefs→无效回显），还需 Play Mode 下用真实多机数据确认：扫描投影楔形随 heading/yaw_angle/pitch/roll 变化、扫描到其他飞机时出现波束线与状态环分弧、面板配置栏实际显示与调参体验。另：数据端横滚角若为"左压为正"需勾选 `invertRollSign`。

## 工作纪律（dsh 会话中确立的偏好）

- **不得把"编译通过"或"无真实数据时的运行检查"当作 UI/遮挡问题已修复**；必须在真实雷达/轨迹数据下验证实际可见结果后才报告完成。
- Play Mode 下禁止场景写操作（`ensure_scene_saved` 会被拒绝）；C# 改动需退出 Play Mode 才能生效，注意编译时机。
- 实测发现问题后，优先排查真实数据链路（`FlightDataStreamReceiver` / `LatestTaskTimeInfo`）而非 UI 层表象。
