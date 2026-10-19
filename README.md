# HK132C — Industrial Digital Twin · Core 3D Runtime & Simulation Integration

> Unity / URP **core runtime** for **multi-track flight-parameter reconstruction & joint simulation**. Portfolio showcase of architecture, data protocols, simulation integration, and 3D/runtime I owned end-to-end as project lead — not a UI-only slice.

[![Unity](https://img.shields.io/badge/Unity-6000%2FURP-black?logo=unity)](#)
[![C#](https://img.shields.io/badge/C%23-Scripts-239120)](#)
[![Protocol](https://img.shields.io/badge/TCP%2FUDP-JSON%20%7C%20Protobuf-blue)](#)

## Why this project

Industrial digital-twin work often fails where **data protocol, simulation timing, and runtime scene state** meet. HK132C is the **core 3D / map / instrument runtime** of a flight-data pipeline — ingest streaming track data, map it to geo space, drive multi-aircraft situational display, and expose operator tools (playback, rules, sensors, alarms) as part of the same system.

Delivered **27 functional capabilities** meeting **11 performance targets** (team of 6; I owned PM + **core digital twin / simulation stack** including architecture, protocols, and 3D runtime). Lab multi-track runs hit **~55–60 FPS**, **~35 Hz** data drive, and **~15 ms** LAN WebSocket latency.

## Highlights

- **Real-time multi-track runtime** — aircraft icons, trails, geo ↔ scene coordinate mapping, difference ranking across tracks
- **Sensor / scan geometry** — 10-variable attitude + scan model (heading / pitch / roll + yaw/pitch scan angles) with projected wedges and detection overlays
- **Playback & timeline** — scrubber bound to real task time, anomaly markers on rule rising-edges, stop clears trails cleanly
- **Configurable data rules** — runtime `DATA_FIELDS` dictionary drives variable panels (autocomplete, legacy-key migration, invalid-key guards)
- **Map / viewport** — zoom/pan with RectMask2D clipping; custom overlay shader so trails/radar draw above aircraft sprites
- **Integration sample** — `tcp_3d_demo.py` + protocol docs for end-to-end send → render demos

## Architecture (core runtime)

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
        ├── SensorPanel / AlarmPanel (operator tools)
        └── Instruments / info grids
```

## Tech stack

| Layer | Choice |
|-------|--------|
| Engine | Unity (URP), C# |
| Runtime UI | World-space Canvas, UGUI, custom `MaskableGraphic` polylines |
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

**Project lead & core digital twin / simulation engineer** — architecture, data protocols, simulation integration, and 3D/runtime as one system (full-stack twin, not a front-end-only role).

---

*English preferred for overseas reviewers. Chinese one-liner: 工业数字孪生核心三维运行时与仿真集成 — 多机飞参重构与联合仿真态势。*
