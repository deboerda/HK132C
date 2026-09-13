

## Codely Structured Memories

### User

### Feedback

### Project
- [2026-08-07 09:01:15] Unity 6000.5.7f1 project using Plastic SCM (not Git). `cm.exe` not on PATH. Package versions required for compatibility: ai.navigation 2.0.14+, collab-proxy 2.13.5+, timeline 1.8.12+, inputsystem 1.20.0+, visualscripting 1.9.12+.
- [2026-09-04 17:58:35] 雷达扇区、扫描线和飞行轨迹必须严格限制在地图边界内，不能因修复UI遮挡而允许越界；遮挡修复应只调整渲染层级/深度，不移除地图边界裁剪。
- [2026-09-14 03:10:00] （自 dsh 会话同步，完整版见 AGENTS.md）UI遮挡已实测修复：根因是 World Space MainCanvas + 飞机 Image 用 ZWrite On 的 2D Lit 材质，调 sortingOrder 无效；最终用 RadarPolylineGraphic(MaskableGraphic) + UI/OverlayAlways(ZTest Always) shader。
- [2026-09-14 03:10:00] 地图缩放/平移不用第二摄像机：MainCanvas/MapViewport(RectMask2D) 固定窗口不动，缩放平移作用于内部`地图`整层（minimapcamera/RawImage 方案已废弃、inactive）。
- [2026-09-14 03:10:00] 回放约定：停止按钮→ClearAllTrails+清异常打点+清超限状态；打点用 slider.normalizedValue、挂 handleRect.parent、宽约1px、不设上限；刻度尺首末=数据 startTime/endTime；拖动进度条瞬移期间(约0.45s)不追加实时轨迹。
- [2026-09-14 03:10:00] 数据端新增 DATA_FIELDS 传表头，约定 Unity 端不再做字段别名匹配，直接用数据端 JSON key（trackId/planId/flightTime/detector_type 等）；FlightDataStreamReceiver.cs 的该适配在 dsh 会话中未完成验证。
- [2026-09-14 03:10:00] 纪律：不得把"编译通过"或无真实数据的运行检查当作遮挡/UI问题已修复；必须在真实雷达/轨迹数据下验证可见结果。Play Mode 下禁止场景写操作。

### Reference

