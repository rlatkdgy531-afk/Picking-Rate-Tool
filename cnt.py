# -*- coding: utf-8 -*-
import sys
from PyQt5.QtWidgets import (
    QApplication, QWidget, QHBoxLayout, QVBoxLayout, QPushButton, QLabel
)
from PyQt5.QtCore import Qt

def pct(n, d): return 0.0 if d == 0 else (n / d) * 100.0

class SuccessRateUI(QWidget):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Picking Rate")
        self.resize(420, 200)

        self.success = 0
        self.fail = 0

        # --- 레이아웃 / 위젯 ---
        main = QVBoxLayout(self); main.setSpacing(18)

        self.lbl = QLabel("성공률: 0.0% (0/0)")
        self.lbl.setAlignment(Qt.AlignCenter)
        self.lbl.setStyleSheet("font-size: 20px; font-weight: 700;")
        main.addWidget(self.lbl)

        row = QHBoxLayout(); main.addLayout(row)

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

        # --- 포커스/키 설정 ---
        # 창이 키 입력을 받도록
        self.setFocusPolicy(Qt.StrongFocus)
        # 버튼이 포커스를 받지 못하게 (방향키로 포커스 이동 방지)
        self.btn_fail.setFocusPolicy(Qt.NoFocus)
        self.btn_success.setFocusPolicy(Qt.NoFocus)

    def showEvent(self, e):
        super().showEvent(e)
        # 창 표시 후 바로 포커스 확보 (키 입력 바로 수신)
        self.activateWindow()
        self.setFocus()

    # --- 로직 ---
    def on_success(self):
        self.success += 1
        self.update_rate()

    def on_fail(self):
        self.fail += 1
        self.update_rate()

    def update_rate(self):
        total = self.success + self.fail
        self.lbl.setText(f"성공률: {pct(self.success, total):.1f}% ({self.success}/{total})")

    # --- 키보드 처리: 방향키만 카운트, 포커스 이동 차단 ---
    def keyPressEvent(self, e):
        k = e.key()
        if k == Qt.Key_Left:     # 왼쪽 = 실패
            self.on_fail(); e.accept(); return
        if k == Qt.Key_Right:    # 오른쪽 = 성공
            self.on_success(); e.accept(); return
        # Enter로 카운트되지 않도록 기본 처리만
        super().keyPressEvent(e)

if __name__ == "__main__":
    app = QApplication(sys.argv)
    w = SuccessRateUI()
    w.show()
    sys.exit(app.exec_())
