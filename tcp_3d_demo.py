"""
三维端联调 Demo — GUI 界面
开启服务等待数据端连接，再连接数据端发送指令
"""

import codecs
import json
import socket
import threading
import time
import uuid
import select
import tkinter as tk
from tkinter import ttk, scrolledtext
from datetime import datetime

# ======================== Configuration ========================
SEND_HOST = '127.0.0.1'
SEND_PORT = 8888

LISTEN_HOST = '127.0.0.1'
LISTEN_PORT = 9999


class Demo3D(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title('三维端联调 Demo')
        self.geometry('880x620')
        self.minsize(700, 500)

        self.server_srv = None
        self.data_conn = None
        self.cmd_sock = None
        self.running = True
        # 保存最近一次从数据端接收到的数据列映射，便于界面显示和后续联调检查。
        self.data_fields = {}

        self._build_ui()

    # ---------- UI ----------
    def _build_ui(self):
        main = ttk.Frame(self, padding=8)
        main.pack(fill='both', expand=True)

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
        self.btn_connect = ttk.Button(row1, text='2. 连接数据端', width=14, command=self.cmd_connect, state='disabled')
        self.btn_connect.pack(side='left', padx=2)
        self.btn_disconnect = ttk.Button(row1, text='断开', width=8, command=self.cmd_disconnect, state='disabled')
        self.btn_disconnect.pack(side='left', padx=2)
        ttk.Separator(row1, orient='vertical').pack(side='left', fill='y', padx=8)

        self.btn_start = ttk.Button(row1, text='START', width=8, command=self.cmd_start, state='disabled')
        self.btn_start.pack(side='left', padx=2)
        self.btn_resume = ttk.Button(row1, text='RESUME', width=8, command=self.cmd_resume, state='disabled')
        self.btn_resume.pack(side='left', padx=2)
        self.btn_pause = ttk.Button(row1, text='PAUSE', width=8, command=self.cmd_pause, state='disabled')
        self.btn_pause.pack(side='left', padx=2)
        self.btn_stop = ttk.Button(row1, text='STOP', width=8, command=self.cmd_stop, state='disabled')
        self.btn_stop.pack(side='left', padx=2)

        # 新增：数据请求按钮行
        row_req = ttk.Frame(ctrl_frame)
        row_req.pack(fill='x', pady=2)
        self.btn_get_time = ttk.Button(row_req, text='获取起止时间', width=14, command=self.cmd_get_time_info, state='disabled')
        self.btn_get_time.pack(side='left', padx=2)
        self.btn_get_track = ttk.Button(row_req, text='获取航迹', width=14, command=self.cmd_get_track, state='disabled')
        self.btn_get_track.pack(side='left', padx=2)

        row2 = ttk.Frame(ctrl_frame)
        row2.pack(fill='x', pady=2)
        ttk.Label(row2, text='倍速:').pack(side='left')
        for spd in [0.5, 1.0, 2.0, 4.0, 8.0]:
            ttk.Button(row2, text=f'{spd}x', width=4,
                       command=lambda s=spd: self.cmd_speed(s)).pack(side='left', padx=1)
        ttk.Separator(row2, orient='vertical').pack(side='left', fill='y', padx=8)
        ttk.Label(row2, text='跳转:').pack(side='left')
        self.seek_var = tk.StringVar(value='2026-06-04 10:05:00.000')
        ttk.Entry(row2, textvariable=self.seek_var, width=26, font=('Consolas', 10)).pack(side='left', padx=2)
        ttk.Button(row2, text='SEEK', width=6, command=self.cmd_seek).pack(side='left')

        # Legend
        row3 = ttk.Frame(ctrl_frame)
        row3.pack(fill='x', pady=(4, 0))
        for tag, color in [('RECV', '#0a6b3a'), ('SEND', '#8b4513'), ('INFO', '#1a5276'), ('ERROR', '#c0392b')]:
            ttk.Label(row3, text=f' ● {tag} ', foreground=color, font=('Consolas', 9)).pack(side='left', padx=4)
        self.info_label = ttk.Label(row3, text='', font=('Consolas', 9))
        self.info_label.pack(side='right', padx=4)

        # Log
        log_frame = ttk.LabelFrame(main, text='消息日志', padding=4)
        log_frame.pack(fill='both', expand=True)
        self.log = scrolledtext.ScrolledText(
            log_frame, font=('Consolas', 10), wrap='word',
            state='disabled', bg='#fafafa', relief='flat', borderwidth=1
        )
        self.log.pack(fill='both', expand=True)
        self.log.tag_config('recv', foreground='#0a6b3a')
        self.log.tag_config('send', foreground='#8b4513')
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
        tag_name = {'recv': 'recv', 'send': 'send', 'info': 'info', 'error': 'error'}.get(tag, '')
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

    # ---------- 1. 开启服务 (SEND_PORT 接收数据推送) ----------
    def cmd_listen(self):
        self.btn_listen.config(state='disabled', text='启动中...')

        def start():
            srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            try:
                srv.bind(('0.0.0.0', SEND_PORT))
                srv.listen(1)
                srv.setblocking(False)
                self.server_srv = srv
                self._log('info', f'服务已开启 0.0.0.0:{SEND_PORT}')
                self._update_status(f'● 服务:0.0.0.0:{SEND_PORT} 等待数据端 ...')
                self.after(0, lambda: self.btn_connect.config(state='normal'))
                self.after(0, lambda: self.btn_listen.config(text='✅ 服务已开启'))

                while self.running and not self.data_conn:
                    r, _, _ = select.select([srv], [], [], 1.0)
                    if not r:
                        continue
                    try:
                        conn, addr = srv.accept()
                        conn.setblocking(True)
                        self.data_conn = conn
                        self._log('info', f'数据端已连接 {addr}')
                        self._update_status(f'● 服务:0.0.0.0:{SEND_PORT} 已连数据端 {addr[0]}:{addr[1]}')
                        self.after(0, lambda: self.btn_start.config(state='normal'))
                        self.after(0, lambda: self.btn_resume.config(state='normal'))
                        self.after(0, lambda: self.btn_pause.config(state='normal'))
                        self.after(0, lambda: self.btn_stop.config(state='normal'))
                        self._read_data_loop(conn)
                    except Exception as e:
                        if self.running:
                            self._log('error', f'接受连接错误: {e}')
                        break

                if srv:
                    try:
                        srv.close()
                    except Exception:
                        pass
            except Exception as e:
                self._log('error', f'开启服务失败: {e}')
                self.after(0, lambda: self.btn_listen.config(text='开启服务', state='normal'))

        threading.Thread(target=start, daemon=True).start()

    def _read_data_loop(self, conn):
        buf = ''
        # TCP 分片可能截断 UTF-8 多字节字符，增量解码器会把半个字符留到下一次 recv。
        decoder = codecs.getincrementaldecoder('utf-8')()
        conn.settimeout(1.0)
        while self.running and conn:
            try:
                data = conn.recv(8192)
                if not data:
                    self._log('info', '数据端已断开')
                    self._update_status(f'● 服务:0.0.0.0:{SEND_PORT} 等待数据端 ...')
                    self.data_conn = None
                    self.after(0, lambda: self.btn_start.config(state='disabled'))
                    self.after(0, lambda: self.btn_resume.config(state='disabled'))
                    self.after(0, lambda: self.btn_pause.config(state='disabled'))
                    self.after(0, lambda: self.btn_stop.config(state='disabled'))
                    break
                try:
                    buf += decoder.decode(data)
                except UnicodeDecodeError as exc:
                    self._log('error', f'UTF-8 解码错误，已丢弃当前分片: {exc}')
                    decoder = codecs.getincrementaldecoder('utf-8')()
                    buf += data.decode('utf-8', errors='replace')
                while '\n' in buf:
                    line, buf = buf.split('\n', 1)
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        msg = json.loads(line)
                        self.after(0, lambda m=msg: self._on_data_msg(m))
                    except json.JSONDecodeError:
                        self._log('error', f'JSON 解析失败: {line[:120]}')
            except socket.timeout:
                continue
            except OSError:
                break
            except Exception as e:
                if self.running:
                    self._log('error', f'读取错误: {e}')
                break

    @staticmethod
    def _normalize_data_fields(fields):
        """校验并规范化 DATA_FIELDS 消息中的字段映射。

        数据端发送的格式是 ``{JSON key: [CSV 表头或中文别名]}``。这里统一转成
        字符串列表，既兼容单个字符串值，也避免界面层直接持有网络消息对象。
        """

        if not isinstance(fields, dict):
            raise ValueError('fields 必须是对象')

        normalized = {}
        # 遍历数据端发送的每个字段，清理空 key 和空别名，保持原有字段顺序。
        for key, aliases in fields.items():
            field_key = str(key or '').strip()
            if not field_key:
                continue

            if aliases is None:
                alias_values = []
            elif isinstance(aliases, (list, tuple)):
                alias_values = aliases
            else:
                alias_values = [aliases]

            normalized[field_key] = [
                str(alias).strip()
                for alias in alias_values
                if str(alias or '').strip()
            ]
        return normalized

    def _on_data_msg(self, msg):
        """处理数据端推送的消息，并更新三维联调界面的接收摘要。"""

        mtype = msg.get('type', '')
        data = msg.get('data', {})
        # 所有业务消息都要求 data 为对象；格式错误时只记录日志，避免回调线程中断。
        if not isinstance(data, dict):
            self._log('error', f'{mtype or "未知消息"} 的 data 必须是对象')
            self.info_label.config(text=f'{mtype or "未知消息"}: 数据格式错误')
            return

        if mtype == 'TASK_TIME_INFO':
            tid = data.get('taskId', '?')
            st = data.get('startTime', '?')
            et = data.get('endTime', '?')
            self.info_label.config(text=f'航迹: {tid}  {st} ~ {et}')
            self._log('recv', f'起止时间: {st} ~ {et}')
        elif mtype == 'TRACK_POSITIONS':
            tracks = data.get('tracks', [])
            total_pos = sum(len(p.get('positions', []))
                            for t in tracks
                            for p in t.get('plans', []))
            self.info_label.config(text=f'航迹数据: {len(tracks)} 条航迹, {total_pos} 个位置点')
            self._log('recv', f'帧数据: {len(tracks)} 条航迹, {total_pos} 个位置点')

    # ---------- 2. 连接数据端 (LISTEN_PORT 发送指令) ----------
        elif mtype in ('EVENT_TABLE_DATA', 'TIMETABLE_STATE'):
            self._log_json('recv', msg)
            flight_time = data.get('flightTime', '?')
            event_record = data.get('event', data.get('flightStates', {}))
            state_count = 1 if isinstance(event_record, dict) and event_record else len(event_record) if isinstance(event_record, list) else 0
            self.info_label.config(text=f'事件表数据: {flight_time}  records={state_count}')
        elif mtype == 'CYCLE_TABLE_DATA':
            self._log_json('recv', msg)
            flight_time = data.get('flightTime', '?')
            records = data.get('records', {})
            record_count = 1 if isinstance(records, dict) and records else len(records) if isinstance(records, list) else 0
            self.info_label.config(text=f'鍛ㄦ湡琛ㄦ暟鎹? {flight_time}  璁板綍: {record_count}')
            self.info_label.config(text=f'CYCLE_TABLE_DATA: {flight_time}  records={record_count}')
        elif mtype == 'DATA_FIELDS':
            # DATA_FIELDS 只描述字段映射，不参与逐帧播放，因此单独保存并展示摘要。
            try:
                fields = self._normalize_data_fields(data.get('fields'))
            except (TypeError, ValueError) as exc:
                self._log('error', f'DATA_FIELDS 格式错误: {exc}')
                self.info_label.config(text='数据列: 格式错误')
                return

            self.data_fields = fields
            self._log_json('recv', msg)
            self.info_label.config(text=f'数据列: {len(fields)} 个字段')
            field_names = ', '.join(fields.keys()) or '无'
            self._log('recv', f'已接收数据列字段说明: {field_names}')
        else:
            self._log_json('recv', msg)
            self.info_label.config(text=f'鏀跺埌鏁版嵁: {mtype or "?"}')

    def cmd_connect(self):
        self.btn_connect.config(state='disabled', text='连接中...')

        def connect():
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(5.0)
            try:
                sock.connect((LISTEN_HOST, LISTEN_PORT))
                sock.settimeout(None)
                self.cmd_sock = sock
                self._log('info', f'已连接数据端指令通道 {LISTEN_HOST}:{LISTEN_PORT}')
                self._update_status(self.status_var.get().replace('等待数据端', '已连数据端').replace(
                    '就绪', f'已连指令通道 {LISTEN_HOST}:{LISTEN_PORT}'))
                self.after(0, lambda: self.btn_connect.config(text='✅ 已连接', state='disabled'))
                self.after(0, lambda: self.btn_disconnect.config(state='normal'))
                self.after(0, lambda: self.btn_get_time.config(state='normal'))
                self.after(0, lambda: self.btn_get_track.config(state='normal'))
            except Exception as e:
                self._log('error', f'连接数据端指令通道失败: {e}')
                self.after(0, lambda: self.btn_connect.config(text='2. 连接数据端', state='normal'))

        threading.Thread(target=connect, daemon=True).start()

    # ---------- Disconnect ----------
    def cmd_disconnect(self):
        if self.cmd_sock:
            try:
                self.cmd_sock.close()
            except Exception:
                pass
            self.cmd_sock = None
        self._log('info', '已断开指令通道')
        self._update_status(self.status_var.get().replace('已连指令通道', '指令通道已断开'))
        self.after(0, lambda: self.btn_connect.config(text='2. 连接数据端', state='normal'))
        self.after(0, lambda: self.btn_disconnect.config(state='disabled'))
        self.after(0, lambda: self.btn_get_time.config(state='disabled'))
        self.after(0, lambda: self.btn_get_track.config(state='disabled'))

    # ---------- Command send ----------
    def _send_cmd(self, msg_type, data):
        if not self.cmd_sock:
            self._log('error', '未连接数据端指令通道')
            return
        msg = {
            'uuid': str(uuid.uuid4()),
            'timestamp': int(time.time() * 1000),
            'type': msg_type,
            'data': data,
        }
        try:
            payload = json.dumps(msg, ensure_ascii=False) + '\n'
            self.cmd_sock.sendall(payload.encode('utf-8'))
            self._log_json('send', msg)
        except Exception as e:
            self._log('error', f'发送失败 [{msg_type}]: {e}')
            self.cmd_disconnect()

    def cmd_resume(self):
        self._send_cmd('RESUME_DATA_STREAM', {})
    
    def cmd_start(self):
        self._send_cmd('START_DATA_STREAM', {'taskId': 'flight_20260604_001'})

    def cmd_pause(self):
        self._send_cmd('PAUSE_DATA_STREAM', {})

    def cmd_stop(self):
        self._send_cmd('STOP_DATA_STREAM', {})

    def cmd_speed(self, multiplier):
        self._send_cmd('SET_PLAYBACK_SPEED', {'speedMultiplier': multiplier})

    def cmd_seek(self):
        t = self.seek_var.get().strip()
        if not t:
            self._log('error', '跳转时间不能为空')
            return
        self._send_cmd('SEEK_TO_TIME', {'targetTime': t})

    def cmd_get_time_info(self):
        self._send_cmd('GET_TASK_TIME_INFO', {})

    def cmd_get_track(self):
        self._send_cmd('GET_TRACK_POSITIONS', {})

    # ---------- Cleanup ----------
    def _on_close(self):
        self._log('info', '正在关闭 ...')
        self.running = False
        for s in (self.data_conn, self.cmd_sock, self.server_srv):
            if s:
                try:
                    s.close()
                except Exception:
                    pass
        self.after(200, self.destroy)


if __name__ == '__main__':
    app = Demo3D()
    app.mainloop()
