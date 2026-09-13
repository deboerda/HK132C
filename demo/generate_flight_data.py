"""
Generate flight data for 3D visualization testing.
Outputs: flight_positions.csv, flight_timetable.csv, flight_cycle.csv
Protocol: data端与三维端通讯协议文档.md
"""

import numpy as np
import pandas as pd
import os
from datetime import datetime, timedelta

SEED = 42
np.random.seed(SEED)

START_TIME = datetime(2026, 6, 4, 10, 0, 0)
END_TIME = datetime(2026, 6, 4, 10, 10, 0)
TOTAL_SECONDS = (END_TIME - START_TIME).total_seconds()

POS_DT = 0.5
TT_DT = 1.0
CYCLE_DT = 5.0

M_PER_DEG = 111320.0
G = 9.8

OUT_DIR = os.path.dirname(__file__) or '.'

TRACKS = [
    {
        'trackId': 'drone_001',
        'start_lng': 104.07, 'start_lat': 30.57, 'start_alt': 500.0,
        'start_heading': 90.0, 'speed': 200.0,
        'battery_start': 100.0, 'battery_end': 60.0,
        'plans': [
            {'planId': 'PLAN1', 'duration': 180, 'dlng': 0.08, 'dlat': 0.04, 'end_alt': 3000, 'dheading': 10.0},
            {'planId': 'PLAN2', 'duration': 240, 'dlng': 0.15, 'dlat': -0.05, 'end_alt': 2800, 'dheading': 360.0},
            {'planId': 'PLAN3', 'duration': 180, 'dlng': 0.05, 'dlat': -0.08, 'end_alt': 600, 'dheading': -30.0},
        ],
    },
    {
        'trackId': 'drone_002',
        'start_lng': 103.90, 'start_lat': 30.70, 'start_alt': 500.0,
        'start_heading': 45.0, 'speed': 180.0,
        'battery_start': 95.0, 'battery_end': 55.0,
        'plans': [
            {'planId': 'PLAN1', 'duration': 180, 'dlng': 0.06, 'dlat': 0.05, 'end_alt': 2500, 'dheading': -5.0},
            {'planId': 'PLAN2', 'duration': 240, 'dlng': -0.10, 'dlat': 0.08, 'end_alt': 2600, 'dheading': -360.0},
            {'planId': 'PLAN3', 'duration': 180, 'dlng': 0.03, 'dlat': -0.06, 'end_alt': 500, 'dheading': 20.0},
        ],
    },
]


def fmt_time(t):
    return t.strftime('%Y-%m-%d %H:%M:%S.') + f'{t.microsecond // 1000:03d}'


def get_state(plan_id, plan_elapsed, plan_duration):
    if plan_id == 'PLAN1' and plan_elapsed < 30:
        return 'TAKEOFF'
    if plan_id == 'PLAN3' and plan_duration - plan_elapsed < 30:
        return 'LANDING'
    if plan_id == 'PLAN2' and np.random.random() < 0.02:
        return 'HOVER'
    return 'CRUISING'


def generate_track(tc):
    clng, clat, calt, chdg = tc['start_lng'], tc['start_lat'], tc['start_alt'], tc['start_heading']
    speed = tc['speed']
    plan_start_time = START_TIME
    battery_rate = (tc['battery_start'] - tc['battery_end']) / TOTAL_SECONDS
    cycle_counter = 1

    pos_records = []
    tt_records = []
    cyc_records = []

    tt_step = int(round(TT_DT / POS_DT))
    cycle_step = int(round(CYCLE_DT / POS_DT))

    for pi, plan in enumerate(tc['plans']):
        is_last = (pi == len(tc['plans']) - 1)
        end_lng = clng + plan['dlng']
        end_lat = clat + plan['dlat']
        end_alt = plan['end_alt']
        end_hdg = chdg + plan['dheading']
        duration = plan['duration']

        n = int(duration / POS_DT)
        if is_last:
            n += 1

        heading_rate = plan['dheading'] / duration

        dx = plan['dlng']
        dy = plan['dlat']
        norm = np.hypot(dx, dy)
        n_cycles = abs(plan['dheading']) / 360.0 if plan['dheading'] != 0 else 0
        hdg_rate_rad = abs(np.radians(plan['dheading'])) / duration if plan['dheading'] != 0 else 0
        dir_sign = 1 if plan['dheading'] > 0 else -1

        turn_radius = 0
        amp_deg = 0
        px, py = 0, 0
        if hdg_rate_rad > 0.001:
            turn_radius = speed / hdg_rate_rad
            amp_deg = turn_radius / M_PER_DEG
            if norm > 0:
                px = dy / norm * dir_sign
                py = -dx / norm * dir_sign
            else:
                px, py = dir_sign, 0

        for i in range(n):
            frac = i / max(n - 1, 1)
            t_smooth = 3 * frac * frac - 2 * frac * frac * frac

            lng_linear = clng + dx * frac
            lat_linear = clat + dy * frac
            alt = calt + (end_alt - calt) * t_smooth
            hdg = chdg + plan['dheading'] * frac

            lng, lat = lng_linear, lat_linear
            if plan['dheading'] != 0:
                offset = amp_deg * np.sin(2 * np.pi * n_cycles * frac)
                lng = lng_linear + offset * px
                lat = lat_linear + offset * py

            climb_rate = (end_alt - calt) / duration
            pitch = np.degrees(np.arctan2(climb_rate, speed)) if abs(climb_rate) > 0.1 else 0

            roll = 0
            if abs(hdg_rate_rad) > 0.001:
                roll = np.degrees(np.arctan2(speed * hdg_rate_rad, G))
                roll = np.clip(roll, -45, 45)

            lng += np.random.normal(0, 3e-5)
            lat += np.random.normal(0, 3e-5)
            alt += np.random.normal(0, 1)
            pitch += np.random.normal(0, 0.5)
            roll += np.random.normal(0, 0.5)
            hdg_out = (hdg + np.random.normal(0, 0.3)) % 360

            ft = plan_start_time + timedelta(seconds=i * POS_DT)
            ft_str = fmt_time(ft)

            pos_records.append({
                'flightTime': ft_str,
                'trackId': tc['trackId'],
                'planId': plan['planId'],
                'lng': round(lng, 6),
                'lat': round(lat, 6),
                'alt': round(alt, 1),
                'heading': round(hdg_out, 2),
                'pitch': round(pitch, 2),
                'roll': round(roll, 2),
            })

            if i % tt_step == 0:
                plan_elapsed = i * POS_DT
                elapsed = (ft - START_TIME).total_seconds()
                bat = tc['battery_start'] - battery_rate * elapsed
                sv = speed * (0.95 + 0.1 * np.random.random())
                tt_records.append({
                    'flightTime': ft_str,
                    'trackId': tc['trackId'],
                    'state': get_state(plan['planId'], plan_elapsed, duration),
                    'battery': round(bat, 1),
                    'speed': round(sv, 2),
                })

            if i % cycle_step == 0:
                cyc_records.append({
                    'flightTime': ft_str,
                    'cycleId': cycle_counter,
                    'lng': round(lng, 6),
                    'lat': round(lat, 6),
                    'alt': round(alt, 1),
                    'heading': round(hdg_out, 2),
                    'pitch': round(pitch, 2),
                    'roll': round(roll, 2),
                })
                cycle_counter += 1

        clng, clat, calt, chdg = end_lng, end_lat, end_alt, end_hdg
        plan_start_time += timedelta(seconds=duration)

    return pos_records, tt_records, cyc_records


def main():
    all_pos = []
    all_tt = []
    all_cyc = []
    for tc in TRACKS:
        pos, tt, cyc = generate_track(tc)
        all_pos.extend(pos)
        all_tt.extend(tt)
        all_cyc.extend(cyc)

    df_pos = pd.DataFrame(all_pos)
    df_tt = pd.DataFrame(all_tt)
    df_cyc = pd.DataFrame(all_cyc)

    df_pos = df_pos.sort_values(['flightTime', 'trackId']).reset_index(drop=True)
    df_tt = df_tt.sort_values(['flightTime', 'trackId']).reset_index(drop=True)
    df_cyc = df_cyc.sort_values(['flightTime', 'cycleId']).reset_index(drop=True)

    df_pos.to_csv(os.path.join(OUT_DIR, 'flight_positions.csv'), index=False, encoding='utf-8-sig')
    df_tt.to_csv(os.path.join(OUT_DIR, 'flight_timetable.csv'), index=False, encoding='utf-8-sig')
    df_cyc.to_csv(os.path.join(OUT_DIR, 'flight_cycle.csv'), index=False, encoding='utf-8-sig')

    print(f"Positions: {len(df_pos)} rows")
    print(f"Timetable: {len(df_tt)} rows")
    print(f"Cycle: {len(df_cyc)} rows")
    print("Done.")


if __name__ == '__main__':
    main()
