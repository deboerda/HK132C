# HK132C — Industrial Digital Twin · 3D Visualization Front-End

> Unity / URP real-time front-end for **multi-track flight-parameter reconstruction & joint simulation visualization**. Portfolio showcase of architecture, data integration, and operator UX I built and shipped as project lead.

[![Unity](https://img.shields.io/badge/Unity-6000%2FURP-black?logo=unity)](#)
[![C#](https://img.shields.io/badge/C%23-Scripts-239120)](#)
[![Protocol](https://img.shields.io/badge/TCP%2FUDP-JSON%20%7C%20Protobuf-blue)](#)

## Why this project

Industrial digital-twin work often fails at the last mile: live telemetry arrives faster than the UI can stay correct. HK132C is the **3D / map / instrument visualization end** of a flight-data pipeline — ingest streaming track data, map it to geo space, drive multi-aircraft situational display, and expose operator tools (playback, rules, sensors, alarms).

Delivered **27 functional capabilities** meeting **11 performance targets** (team of 6; I owned PM + core Unity front-end). Lab multi-track runs hit **~55–60 FPS**, **~35 Hz** data drive, and **~15 ms** LAN WebSocket latency.

## Highlights

- **Real-time multi-track viz** — aircraft icons, trails, geo ↔ UI coordinate mapping, difference ranking across tracks
- **Sensor / scan geometry** — 10-variable attitude + scan model (heading / pitch / roll + yaw/pitch scan angles) with projected wedges and detection overlays
- **Playback & timeline** — scrubber bound to real task time, anomaly markers on rule rising-edges, stop clears trails cleanly
- **Configurable data rules** — runtime `DATA_FIELDS` dictionary drives variable panels (autocomplete, legacy-key migration, invalid-key guards)
- **Map UX** — zoom/pan viewport with RectMask2D clipping; custom UI overlay shader so trails/radar draw above aircraft sprites
- **Integration sample** — `tcp_3d_demo.py` + protocol docs for end-to-end send → render demos

## Architecture (front-end)

```
Data source (CSV / stream / demo sender)
        │  TCP / UDP  (JSON envelopes + optional Protobuf)
        ▼
FlightDataStreamReceiver  ── field-key mapping / DATA_FIELDS
        │
        ├── PlaneDisplayController   (icons, trails, geo map)
        ├── RadarDetectionSystem     (scan cones / detection)
        ├── FlightPlaybackPanel      (timeline / scrub / stop)
        ├── DataRuleConfigPanel      (rules + anomaly paint)
        ├── SensorPanel / AlarmPanel (operator UX)
        └── Instruments / info grids
```

## Tech stack

| Layer | Choice |
|-------|--------|
| Engine | Unity (URP), C# |
| UI | World-space Canvas, UGUI, custom `MaskableGraphic` polylines |
| Networking | TCP/UDP JSON streams, Google Protobuf samples |
| Data | StreamingAssets field maps, runtime DATA_FIELDS protocol |
| Tooling | Editor utilities for panel scene rows; Python demo sender |

## Repo layout

```
Assets/Scripts/     # Core controllers (display, radar, playback, rules, sensors…)
FlyParamData/       # Sample / demo flight-parameter tables
demo/ · screenshots/
tcp_3d_demo.py      # End-to-end data-send demo
```

## Role

**Project manager & Unity front-end owner** — requirements through acceptance for the visualization end of an industrial digital-twin flight-data system.

---

*English preferred for overseas reviewers. Chinese one-liner: 工业数字孪生三维可视化前端 — 多机飞参重构与联合仿真态势展示。*
