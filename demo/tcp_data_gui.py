"""
数据端联调 Demo — GUI 界面
开启服务等待三维端连接，再连接三维端推送数据
"""

import json
import socket
import threading
import time
import uuid
import bisect
import select
from pathlib import Path
import pandas as pd
import tkinter as tk
from tkinter import ttk, scrolledtext
from datetime import datetime

# ======================== Configuration ========================
SEND_HOST = '127.0.0.1'
SEND_PORT = 8888

LISTEN_HOST = '127.0.0.1'
LISTEN_PORT = 9999

TASK_ID = 'flight_20260604_001'
START_TIME_STR = '2026-06-04 10:00:00.000'
END_TIME_STR = '2026-06-04 10:10:00.000'
FRAME_DT = 1.0

BASE_DIR = Path(__file__).resolve().parent
POSITIONS_CSV = BASE_DIR / 'flight_positions.csv'
TIMETABLE_CSV = BASE_DIR / 'flight_timetable.csv'
CYCLE_CSV = BASE_DIR / 'flight_cycle.csv'


class DataGUI(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title('数据端联调 Demo')
        self.geometry('880x620')
        self.minsize(700, 500)

        # State
        self.listener_srv = None
        self.cmd_conn = None
        self.data_sock = None
        self.running = True
        self.socket_lock = threading.RLock()
        self.disconnecting = False

        self.speed = 1.0
        self.current_idx = 0
        self.run_event = threading.Event()
        self.stop_event = threading.Event()
        self.send_thread = None

        # Load data
        self.pos_map = {}
        self.tt_map = {}
        self.cyc_map = {}
        self.pos_times = []
        self._load_data()

        self._build_ui()

    # ---------- Data Loading ----------
    def _load_data(self):
        try:
            df_pos = pd.read_csv(POSITIONS_CSV)
            df_tt = pd.read_csv(TIMETABLE_CSV)
            df_cyc = pd.read_csv(CYCLE_CSV)

            for ft, grp in df_pos.groupby('flightTime'):
                self.pos_map[ft] = grp.to_dict('records')
            for ft, grp in df_tt.groupby('flightTime'):
                self.tt_map[ft] = grp.to_dict('records')
            for ft, grp in df_cyc.groupby('flightTime'):
                self.cyc_map[ft] = grp.to_dict('records')

            self.pos_times = sorted(self.pos_map.keys())
            self._log('info', f'数据加载完成: {len(self.pos_times)} 帧, '
                      f'{len(self.pos_map)} 位置, {len(self.tt_map)} 时间表, {len(self.cyc_map)} 周期')
        except Exception as e:
            self._log('error', f'数据加载失败: {e}')

    # ---------- UI ----------
    def _build_ui(self):
        main = ttk.Frame(self, padding=8)
        main.pack(fill='both', expand=True)

        # Status
        self.status_var = tk.StringVar(value='● 就绪')
        status_bar = ttk.Label(main, textvariable=self.status_var,
                               font=('Consolas', 10), background='#e8e8e8',
                               anchor='w', padding=(8, 4))
        status_bar.pack(fill='x', pady=(0, 6))

        # Control panel
        ctrl_frame = ttk.LabelFrame(main, text='控制面板', padding=8)
        ctrl_frame.pack(fill='x', pady=(0, 6))

        row1 = ttk.Frame(ctrl_frame)
        row1.pack(fill='x', pady=2)
        self.btn_listen = ttk.Button(row1, text='1. 开启服务', width=14, command=self.cmd_listen)
        self.btn_listen.pack(side='left', padx=2)
        self.btn_connect = ttk.Button(row1, text='2. 连接三维端', width=14, command=self.cmd_connect, state='disabled')
        self.btn_connect.pack(side='left', padx=2)
        self.btn_disconnect = ttk.Button(row1, text='断开', width=8, command=self.cmd_disconnect, state='disabled')
        self.btn_disconnect.pack(side='left', padx=2)
        ttk.Separator(row1, orient='vertical').pack(side='left', fill='y', padx=8)
        self.btn_start = ttk.Button(row1, text='▶ 开始推送', width=12, command=self.cmd_start_push, state='disabled')
        self.btn_start.pack(side='left', padx=2)
        self.btn_resume = ttk.Button(row1, text='▶ 继续仿真', width=12, command=self.cmd_resume_push, state='disabled')
        self.btn_resume.pack(side='left', padx=2)
        self.btn_pause = ttk.Button(row1, text='⏸ 暂停', width=8, command=self.cmd_pause_push, state='disabled')
        self.btn_pause.pack(side='left', padx=2)
        self.btn_stop = ttk.Button(row1, text='⏹ 停止', width=8, command=self.cmd_stop_push, state='disabled')
        self.btn_stop.pack(side='left', padx=2)

        row2 = ttk.Frame(ctrl_frame)
        row2.pack(fill='x', pady=2)
        ttk.Label(row2, text='倍速:').pack(side='left')
        for spd in [0.5, 1.0, 2.0, 4.0, 8.0]:
            ttk.Button(row2, text=f'{spd}x', width=4,
                       command=lambda s=spd: self.cmd_set_speed(s)).pack(side='left', padx=1)
        ttk.Separator(row2, orient='vertical').pack(side='left', fill='y', padx=8)
        ttk.Label(row2, text='跳转:').pack(side='left')
        self.seek_var = tk.StringVar(value='2026-06-04 10:05:00.000')
        ttk.Entry(row2, textvariable=self.seek_var, width=26, font=('Consolas', 10)).pack(side='left', padx=2)
        ttk.Button(row2, text='SEEK', width=6, command=self.cmd_seek_push).pack(side='left')

        # Legend
        row3 = ttk.Frame(ctrl_frame)
        row3.pack(fill='x', pady=(4, 0))
        for tag, color in [('SEND', '#0a6b3a'), ('RECV', '#8b4513'), ('INFO', '#1a5276'), ('ERROR', '#c0392b')]:
            ttk.Label(row3, text=f' ● {tag} ', foreground=color, font=('Consolas', 9)).pack(side='left', padx=4)
        self.frame_label = ttk.Label(row3, text='', font=('Consolas', 9))
        self.frame_label.pack(side='right', padx=4)

        # Log
        log_frame = ttk.LabelFrame(main, text='消息日志', padding=4)
        log_frame.pack(fill='both', expand=True)
        self.log = scrolledtext.ScrolledText(
            log_frame, font=('Consolas', 10), wrap='word',
            state='disabled', bg='#fafafa', relief='flat', borderwidth=1
        )
        self.log.pack(fill='both', expand=True)
        self.log.tag_config('send', foreground='#0a6b3a')
        self.log.tag_config('recv', foreground='#8b4513')
        self.log.tag_config('info', foreground='#1a5276')
        self.log.tag_config('error', foreground='#c0392b')

        self.protocol('WM_DELETE_WINDOW', self._on_close)

    # ---------- Logging ----------
    def _log(self, tag, text):
        if not self.running:
            return
        stamp = datetime.now().strftime('%H:%M:%S')
        self.after(0, self._do_log, stamp, tag, text)

    def _do_log(self, stamp, tag, text):
        if not self.running:
            return
        self.log.config(state='normal')
        self.log.insert('end', f'[{stamp}] ', 'info')
        tag_name = {'send': 'send', 'recv': 'recv', 'info': 'info', 'error': 'error'}.get(tag, '')
        self.log.insert('end', f'[{tag.upper()}] ', tag_name)
        self.log.insert('end', text + '\n')
        self.log.see('end')
        self.log.config(state='disabled')

    def _log_json(self, tag, msg):
        mtype = msg.get('type', '?')
        data = msg.get('data', {})
        preview = json.dumps(data, ensure_ascii=False)
        if len(preview) > 300:
            preview = preview[:300] + '...'
        self._log(tag, f'{mtype}  {preview}')

    def _update_status(self, text):
        self.after(0, lambda: self.status_var.set(text))

    # ---------- 1. 开启服务 (LISTEN_PORT 接收三维端指令) ----------
    def cmd_listen(self):
        self.btn_listen.config(state='disabled', text='启动中...')

        def start():
            srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            try:
                srv.bind((LISTEN_HOST, LISTEN_PORT))
                srv.listen(1)
                srv.setblocking(False)
                self.listener_srv = srv
                self._log('info', f'命令服务已开启 {LISTEN_HOST}:{LISTEN_PORT}')
                self._update_status(f'● 服务:{LISTEN_HOST}:{LISTEN_PORT} 等待指令连接 ...')
                self.after(0, lambda: self.btn_connect.config(state='normal'))
                self.after(0, lambda: self.btn_listen.config(text='✅ 服务已开启'))

                while self.running:
                    r, _, _ = select.select([srv], [], [], 1.0)
                    if not r:
                        continue
                    try:
                        conn, addr = srv.accept()
                        conn.setblocking(True)
                        with self.socket_lock:
                            if self.cmd_conn is not None:
                                try:
                                    self.cmd_conn.close()
                                except Exception:
                                    pass
                            self.cmd_conn = conn
                        self._log('info', f'三维端已连接 [指令通道] {addr}')
                        self._update_status(f'● 服务:{LISTEN_HOST}:{LISTEN_PORT} 指令已连接')
                        self._read_json_lines(conn, 'cmd')
                    except Exception as e:
                        if self.running:
                            self._log('error', f'指令通道错误: {e}')
                        break

                if srv:
                    try:
                        srv.close()
                    except Exception:
                        pass
                self.after(0, lambda: self.btn_listen.config(text='重启服务', state='normal'))
            except Exception as e:
                self._log('error', f'开启服务失败: {e}')
                self.after(0, lambda: self.btn_listen.config(text='开启服务', state='normal'))

        threading.Thread(target=start, daemon=True).start()

    # ---------- 2. 连接三维端 (SEND_PORT 推送数据) ----------
    def cmd_connect(self):
        self.btn_connect.config(state='disabled', text='连接中...')

        def connect():
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(5.0)
            try:
                sock.connect((SEND_HOST, SEND_PORT))
                sock.settimeout(None)
                with self.socket_lock:
                    if self.data_sock is not None:
                        try:
                            self.data_sock.close()
                        except Exception:
                            pass
                    self.data_sock = sock
                self._log('info', f'已连接三维端 {SEND_HOST}:{SEND_PORT}')
                self._update_status(f'● 服务:{LISTEN_HOST}:{LISTEN_PORT} ● 已连三维端:{SEND_HOST}:{SEND_PORT}')

                self.after(0, lambda: self.btn_connect.config(text='✅ 已连接', state='disabled'))
                self.after(0, lambda: self.btn_disconnect.config(state='normal'))

                # 不再自动发送 TASK_TIME_INFO，由三维端点击按钮主动获取
                # self._send_json(sock, self._build_task_time_info())

                # Read commands from this connection too
                threading.Thread(target=self._read_json_lines,
                                 args=(sock, 'sock'), daemon=True).start()
            except Exception as e:
                self._log('error', f'连接三维端失败: {e}')
                self.after(0, lambda: self.btn_connect.config(text='2. 连接三维端', state='normal'))

        threading.Thread(target=connect, daemon=True).start()

    # ---------- Disconnect ----------
    def cmd_disconnect(self):
        with self.socket_lock:
            if self.disconnecting:
                return
            self.disconnecting = True

            data_sock = self.data_sock
            cmd_conn = self.cmd_conn
            self.data_sock = None
            self.cmd_conn = None
            had_connection = data_sock is not None or cmd_conn is not None

        for sock in (data_sock, cmd_conn):
            if sock:
                try:
                    sock.shutdown(socket.SHUT_RDWR)
                except Exception:
                    pass
                try:
                    sock.close()
                except Exception:
                    pass

        self.current_idx = 0
        self.run_event.clear()
        self.stop_event.set()
        if had_connection:
            self._log('info', '已断开三维端连接')
        self._update_status(f'● 服务:{LISTEN_HOST}:{LISTEN_PORT} ● 已断开')
        self.after(0, lambda: self.btn_connect.config(text='2. 连接三维端', state='normal'))
        self.after(0, lambda: self.btn_disconnect.config(state='disabled'))
        self.after(0, lambda: self.btn_start.config(state='disabled'))
        self.after(0, lambda: self.btn_resume.config(state='disabled'))
        self.after(0, lambda: self.frame_label.config(text=''))
        self.disconnecting = False

    # ---------- Push control ----------
    def _start_send_thread(self):
        if not self.send_thread or not self.send_thread.is_alive():
            self.send_thread = threading.Thread(target=self._send_loop, daemon=True)
            self.send_thread.start()

    def cmd_start_push(self):
        if not self.pos_times:
            self._log('error', '无法开始推送：飞行数据未加载，请检查 CSV 文件路径')
            return

        self.stop_event.clear()
        self.current_idx = 0
        self.run_event.set()
        self._log('info', '开始推送数据')
        self.after(0, lambda: self.btn_start.config(state='disabled'))
        self.after(0, lambda: self.btn_resume.config(state='disabled'))
        self.after(0, lambda: self.btn_pause.config(state='normal'))
        self.after(0, lambda: self.btn_stop.config(state='normal'))
        self._start_send_thread()

    def cmd_resume_push(self):
        if not self.pos_times:
            self._log('error', '无法继续仿真：飞行数据未加载，请检查 CSV 文件路径')
            return

        self.stop_event.clear()
        self.run_event.set()
        self._log('info', '继续仿真')
        self.after(0, lambda: self.btn_resume.config(state='disabled'))
        self.after(0, lambda: self.btn_pause.config(state='normal'))
        self.after(0, lambda: self.btn_stop.config(state='normal'))
        self._start_send_thread()

    def cmd_pause_push(self):
        self.run_event.clear()
        self._log('info', '已暂停推送')
        self.after(0, lambda: self.btn_resume.config(state='normal'))
        self.after(0, lambda: self.btn_pause.config(state='disabled'))

    def cmd_stop_push(self):
        self.stop_event.set()
        self.run_event.set()
        self.current_idx = 0
        self._log('info', '已停止推送')
        self.after(0, lambda: self.btn_start.config(state='normal'))
        self.after(0, lambda: self.btn_resume.config(state='disabled'))
        self.after(0, lambda: self.btn_pause.config(state='disabled'))
        self.after(0, lambda: self.btn_stop.config(state='disabled'))
        self.after(0, lambda: self.frame_label.config(text=''))

    def cmd_set_speed(self, spd):
        self.speed = spd
        self._log('info', f'倍速设为 {spd}x')

    def cmd_seek_push(self):
        target = self.seek_var.get().strip()
        if not target:
            self._log('error', '请输入跳转时间')
            return
        i = bisect.bisect_left(self.pos_times, target)
        if i >= len(self.pos_times):
            i = len(self.pos_times) - 1
        self.current_idx = i
        self._log('info', f'跳转到索引 {i} ({self.pos_times[i]})')

    # ---------- Send loop ----------
    def _send_json(self, sock, obj):
        if sock is None:
            raise ConnectionError('socket is not connected')

        data = json.dumps(obj, ensure_ascii=False) + '\n'
        with self.socket_lock:
            if sock is not self.data_sock:
                raise ConnectionError('socket is stale or disconnected')
            sock.sendall(data.encode('utf-8'))

    def _build_envelope(self, msg_type, data):
        return {
            'uuid': str(uuid.uuid4()),
            'timestamp': int(time.time() * 1000),
            'type': msg_type,
            'data': data,
        }

    def _build_task_time_info(self):
        return self._build_envelope('TASK_TIME_INFO', {
            'taskId': TASK_ID,
            'startTime': START_TIME_STR,
            'endTime': END_TIME_STR,
        })

    def _build_track_positions(self, records):
        tracks = {}
        for r in records:
            tid = r['trackId']
            pid = r['planId']
            tracks.setdefault(tid, {}).setdefault(pid, []).append({
                'lng': r['lng'], 'lat': r['lat'], 'alt': r['alt'],
                'heading': r['heading'], 'pitch': r['pitch'], 'roll': r['roll'],
            })
        track_list = [
            {'trackId': tid, 'plans': [{'planId': pid, 'positions': pts} for pid, pts in plans.items()]}
            for tid, plans in tracks.items()
        ]
        return self._build_envelope('TRACK_POSITIONS', {'tracks': track_list})

    def _build_timetable(self, ft, records):
        states = []
        optional_keys = (
            'alarm',
            'detector_type',
            'range_type',
            'detect_range',
            'yaw_angle',
            'pitch_angle',
            'center_point',
        )
        for r in records:
            state = {
                'trackId': r['trackId'],
                'state': r['state'],
                'battery': r['battery'],
                'speed': r['speed'],
            }
            for key in optional_keys:
                if key in r:
                    state[key] = r[key]
            states.append(state)
        return self._build_envelope('TIMETABLE_STATE', {'flightTime': ft, 'flightStates': states})

    def _build_cycle_table(self, ft, record):
        obj = {'lng': record['lng'], 'lat': record['lat'], 'alt': record['alt'], 'cycleId': record['cycleId']}
        for k in ('heading', 'pitch', 'roll'):
            if k in record:
                obj[k] = record[k]
        return self._build_envelope('CYCLE_TABLE_DATA', {'flightTime': ft, 'records': obj})

    def _send_loop(self):
        POLL_INTERVAL = 0.05  # 50ms 轮询间隔，暂停/停止响应更快

        while self.current_idx < len(self.pos_times) and not self.stop_event.is_set():
            self.run_event.wait()
            if self.stop_event.is_set():
                break

            ft = self.pos_times[self.current_idx]
            try:
                if ft in self.pos_map:
                    msg = self._build_track_positions(self.pos_map[ft])
                    self._send_json(self.data_sock, msg)

                if ft in self.tt_map:
                    msg = self._build_timetable(ft, self.tt_map[ft])
                    self._send_json(self.data_sock, msg)

                if ft in self.cyc_map:
                    for record in self.cyc_map[ft]:
                        msg = self._build_cycle_table(ft, record)
                        self._send_json(self.data_sock, msg)

                self.after(0, lambda f=ft, i=self.current_idx, s=self.speed:
                           self.frame_label.config(text=f'[{f}] idx={i}/{len(self.pos_times)-1}  speed={s}x'))
            except (ConnectionError, OSError) as e:
                self._log('error', f'数据通道不可用，推送已停止: {e}')
                self.after(0, self.cmd_disconnect)
                break
            except Exception as e:
                self._log('error', f'发送失败 [{ft}]: {e}')
                break

            # 帧跳跃
            step = int(round(self.speed)) if self.speed >= 1.0 else 1
            self.current_idx += step

            # === 真正的间隔等待（使用 time.sleep 确保实际延时） ===
            total_wait = FRAME_DT if self.speed >= 1.0 else FRAME_DT / self.speed
            n = int(total_wait / POLL_INTERVAL)
            for _ in range(n):
                if self.stop_event.is_set():
                    break
                if not self.run_event.is_set():
                    # PAUSE — 阻塞直到恢复为止
                    self.run_event.wait()
                    if self.stop_event.is_set():
                        break
                    # 恢复后重新计算剩余块数
                    break
                time.sleep(POLL_INTERVAL)  # 真正的 50ms 延时
            else:
                # for 循环正常结束：用微 sleep 补齐最后不足 POLL_INTERVAL 的部分
                remainder = total_wait - n * POLL_INTERVAL
                if remainder > 0 and not self.stop_event.is_set():
                    if self.run_event.is_set():
                        time.sleep(remainder)

            if self.stop_event.is_set():
                break

        self._log('info', '推送结束')
        self.after(0, lambda: self.btn_start.config(text='▶ 开始推送', state='normal'))
        self.after(0, lambda: self.btn_pause.config(state='disabled'))
        self.after(0, lambda: self.btn_stop.config(state='disabled'))

    # ---------- Command reader (like 3D端's listener) ----------
    def _read_json_lines(self, sock, source):
        buf = ''
        sock.settimeout(1.0)
        while self.running:
            try:
                with self.socket_lock:
                    current_sock = self.cmd_conn if source == 'cmd' else self.data_sock
                if sock is not current_sock:
                    break

                data = sock.recv(8192)
                if not data:
                    with self.socket_lock:
                        current_sock = self.cmd_conn if source == 'cmd' else self.data_sock
                    if sock is current_sock:
                        self._log('error', f'[{source}] 连接断开')
                        self.after(0, self.cmd_disconnect)
                    break
                buf += data.decode('utf-8')
                while '\n' in buf:
                    line, buf = buf.split('\n', 1)
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        msg = json.loads(line)
                        self._handle_cmd(msg)
                    except json.JSONDecodeError:
                        self._log('error', f'JSON 解析失败: {line[:120]}')
            except socket.timeout:
                continue
            except OSError as e:
                with self.socket_lock:
                    current_sock = self.cmd_conn if source == 'cmd' else self.data_sock
                if self.running and sock is current_sock:
                    self._log('error', f'[{source}] {e}')
                    self.after(0, self.cmd_disconnect)
                break
            except Exception as e:
                with self.socket_lock:
                    current_sock = self.cmd_conn if source == 'cmd' else self.data_sock
                if self.running and sock is current_sock:
                    self._log('error', f'[{source}] {e}')
                    self.after(0, self.cmd_disconnect)
                break

    def _handle_cmd(self, msg):
        mtype = msg.get('type', '')
        mdata = msg.get('data', {})
        self._log_json('recv', msg)

        if mtype == 'START_DATA_STREAM':
            self.after(0, self.cmd_start_push)

        elif mtype == 'PAUSE_DATA_STREAM':
            self.after(0, self.cmd_pause_push)

        elif mtype == 'RESUME_DATA_STREAM':
            self.after(0, self.cmd_resume_push)

        elif mtype == 'STOP_DATA_STREAM':
            self.after(0, self.cmd_stop_push)

        elif mtype == 'SET_PLAYBACK_SPEED':
            val = mdata.get('speedMultiplier', 1.0)
            self.speed = val
            self._log('info', f'倍速调整 -> {val}x')

        elif mtype == 'SEEK_TO_TIME':
            target = mdata.get('targetTime', '')
            if target:
                i = bisect.bisect_left(self.pos_times, target)
                if i >= len(self.pos_times):
                    i = len(self.pos_times) - 1
                self.current_idx = i
                self._log('info', f'跳转到 {target} (idx={i})')

        elif mtype == 'GET_TASK_TIME_INFO':
            self._log('info', '收到请求: 获取起止时间')
            if self.data_sock:
                self._send_json(self.data_sock, self._build_task_time_info())
                self._log_json('send', self._build_task_time_info())

        elif mtype == 'GET_TRACK_POSITIONS':
            self._log('info', '收到请求: 获取航迹')
            if self.data_sock and self.pos_times:
                ft = self.pos_times[self.current_idx]
                records = self.pos_map.get(ft, [])
                if records:
                    msg = self._build_track_positions(records)
                    self._send_json(self.data_sock, msg)
                    self._log_json('send', msg)
                    self._log('info', f'已回复 TRACK_POSITIONS (frame={ft}, {len(records)} 条记录)')
                else:
                    self._log('error', f'当前帧 {ft} 无航迹数据')

        else:
            self._log('error', f'未知指令类型: {mtype}')

    # ---------- Cleanup ----------
    def _on_close(self):
        self._log('info', '正在关闭 ...')
        self.running = False
        self.stop_event.set()
        self.run_event.set()
        self.cmd_disconnect()
        if self.listener_srv:
            try:
                self.listener_srv.close()
            except Exception:
                pass
        self.after(200, self.destroy)


if __name__ == '__main__':
    app = DataGUI()
    app.mainloop()
