# -*- coding: utf-8 -*-
import sys, time
from PyQt5.QtWidgets import (
    QApplication, QWidget, QHBoxLayout, QVBoxLayout, QPushButton, QLabel, QSpacerItem, QSizePolicy
)
from PyQt5.QtCore import Qt, QTimer

def pct(n, d): 
    return 0.0 if d == 0 else (n / d) * 100.0

def per_minute(total, seconds):
    if seconds <= 0:
        return 0.0
    return total * 60.0 / seconds

def fmt_hms(seconds):
    seconds = int(max(0, seconds))
    h = seconds // 3600
    m = (seconds % 3600) // 60
    s = seconds % 60
    return f"{h:02d}:{m:02d}:{s:02d}"

class SuccessRateUI(QWidget):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Picking Rate")
        self.resize(520, 260)

        # --- 상태 ---
        self.success = 0
        self.fail = 0
        self.running = False
        self.t0 = None     # 세션 시작 epoch(sec)
        self.t1 = None     # 세션 종료 epoch(sec) (finish 시 고정)
        self.timer = QTimer(self)
        self.timer.setInterval(1000)  # 1초마다 갱신
        self.timer.timeout.connect(self.update_time_stats)

        # --- 레이아웃 / 위젯 ---
        root = QVBoxLayout(self); root.setSpacing(12)

        # 상단: 성공률/카운트
        self.lbl_rate = QLabel("성공률: 0.0% (0/0)")
        self.lbl_rate.setAlignment(Qt.AlignCenter)
        self.lbl_rate.setStyleSheet("font-size: 22px; font-weight: 700;")
        root.addWidget(self.lbl_rate)

        # 중간: Fail / Success 버튼
        row = QHBoxLayout(); root.addLayout(row)

        self.btn_fail = QPushButton("Fail")
        self.btn_fail.setStyleSheet(
            "QPushButton { background:#ef4444; color:white; font-size:18px; "
            "padding:18px; border-radius:12px; }"
            "QPushButton:hover { background:#dc2626; }"
        )
        self.btn_fail.clicked.connect(self.on_fail)

        self.btn_success = QPushButton("Success")
        self.btn_success.setStyleSheet(
            "QPushButton { background:#22c55e; color:white; font-size:18px; "
            "padding:18px; border-radius:12px; }"
            "QPushButton:hover { background:#16a34a; }"
        )
        self.btn_success.clicked.connect(self.on_success)

        row.addWidget(self.btn_fail)
        row.addWidget(self.btn_success)

        # 하단: 시간/처리량 + 제어(Start/Finish/Reset)
        info = QHBoxLayout(); root.addLayout(info)

        self.lbl_time = QLabel("경과시간 00:00:00")
        self.lbl_time.setStyleSheet("font-size:14px;")
        self.lbl_throughput = QLabel("분당 처리량 0.0 items/min")
        self.lbl_throughput.setStyleSheet("font-size:14px;")

        info.addWidget(self.lbl_time)
        info.addItem(QSpacerItem(20, 0, QSizePolicy.Expanding, QSizePolicy.Minimum))
        info.addWidget(self.lbl_throughput)

        controls = QHBoxLayout(); root.addLayout(controls)

        self.btn_start = QPushButton("Start")
        self.btn_start.setStyleSheet(
            "QPushButton { background:#2563eb; color:white; font-size:16px; padding:10px 16px; border-radius:10px;}"
            "QPushButton:hover { background:#1d4ed8; }"
        )
        self.btn_start.clicked.connect(self.start_session)

        self.btn_finish = QPushButton("Finish")
        self.btn_finish.setStyleSheet(
            "QPushButton { background:#6b7280; color:white; font-size:16px; padding:10px 16px; border-radius:10px;}"
            "QPushButton:hover { background:#4b5563; }"
        )
        self.btn_finish.clicked.connect(self.finish_session)

        self.btn_reset = QPushButton("Reset")
        self.btn_reset.setStyleSheet(
            "QPushButton { background:#e5e7eb; color:#111827; font-size:16px; padding:10px 16px; border-radius:10px;}"
            "QPushButton:hover { background:#d1d5db; }"
        )
        self.btn_reset.clicked.connect(self.reset_session)

        controls.addStretch(1)
        controls.addWidget(self.btn_start)
        controls.addSpacing(8)
        controls.addWidget(self.btn_finish)
        controls.addSpacing(8)
        controls.addWidget(self.btn_reset)
        controls.addStretch(1)

        # --- 포커스/키 설정 ---
        self.setFocusPolicy(Qt.StrongFocus)
        self.btn_fail.setFocusPolicy(Qt.NoFocus)
        self.btn_success.setFocusPolicy(Qt.NoFocus)
        self.btn_start.setFocusPolicy(Qt.NoFocus)
        self.btn_finish.setFocusPolicy(Qt.NoFocus)
        self.btn_reset.setFocusPolicy(Qt.NoFocus)

        self.update_stats()      # 초기 텍스트
        self.update_enabled()    # 버튼 활성/비활성 갱신

    def showEvent(self, e):
        super().showEvent(e)
        self.activateWindow()
        self.setFocus()

    # --- 세션 제어 ---
    def start_session(self):
        if self.running:
            return
        self.running = True
        self.t0 = time.time()
        self.t1 = None
        self.timer.start()
        self.update_enabled()
        self.update_time_stats()  # 즉시 1회 갱신

    def finish_session(self):
        if not self.running:
            return
        self.running = False
        self.t1 = time.time()
        self.timer.stop()
        self.update_enabled()
        self.update_time_stats()  # 종료 시점으로 고정 계산

    def reset_session(self):
        # 카운트/시간 모두 초기화
        self.success = 0
        self.fail = 0
        self.running = False
        self.t0 = None
        self.t1 = None
        self.timer.stop()
        self.update_stats()
        self.update_time_stats()
        self.update_enabled()

    def update_enabled(self):
        # Start/Finish/카운트 가능한 상태 제어
        self.btn_start.setEnabled(not self.running)
        self.btn_finish.setEnabled(self.running)
        # 카운트는 실행 중에만 가능
        self.btn_success.setEnabled(self.running)
        self.btn_fail.setEnabled(self.running)

    # --- 로직(카운트) ---
    def on_success(self):
        if not self.running:
            return
        self.success += 1
        self.update_stats()
        self.update_time_stats()

    def on_fail(self):
        if not self.running:
            return
        self.fail += 1
        self.update_stats()
        self.update_time_stats()

    def update_stats(self):
        total = self.success + self.fail
        self.lbl_rate.setText(f"성공률: {pct(self.success, total):.1f}% ({self.success}/{total})")

    def update_time_stats(self):
        # 시간/처리량 갱신
        if self.t0 is None:
            elapsed = 0
        else:
            now = self.t1 if (self.t1 is not None) else time.time()
            elapsed = now - self.t0

        total = self.success + self.fail
        ipm = per_minute(total, elapsed)

        self.lbl_time.setText(f"경과시간 {fmt_hms(elapsed)}")
        self.lbl_throughput.setText(f"분당 처리량 {ipm:.1f} items/min")

    # --- 키보드 처리: 방향키는 실행 중에만 카운트 ---
    def keyPressEvent(self, e):
        k = e.key()
        if not self.running:
            # 실행 전엔 방향키 입력 무시
            return super().keyPressEvent(e)
        if k == Qt.Key_Left:     # 왼쪽 = 실패
            self.on_fail(); e.accept(); return
        if k == Qt.Key_Right:    # 오른쪽 = 성공
            self.on_success(); e.accept(); return
        super().keyPressEvent(e)

if __name__ == "__main__":
    app = QApplication(sys.argv)
    w = SuccessRateUI()
    w.show()
    sys.exit(app.exec_())
#Hello World