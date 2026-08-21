from PySide6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel, QPushButton, QFrame
)
from PySide6.QtCore import Qt, Signal
from config import APP_NAME

class Sidebar(QFrame):
    page_changed = Signal(str)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setObjectName("sidebar")
        self.buttons = {}
        self.init_ui()

    def init_ui(self):
        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 16, 10, 16)
        layout.setSpacing(6)

        # Brand / Logo Header
        brand_box = QFrame()
        brand_layout = QHBoxLayout(brand_box)
        brand_layout.setContentsMargins(8, 6, 8, 14)

        logo_icon = QLabel("⚡")
        logo_icon.setStyleSheet("font-size: 22px; background-color: #0284C7; border-radius: 8px; padding: 4px 8px;")
        brand_layout.addWidget(logo_icon)

        brand_text = QVBoxLayout()
        title = QLabel(APP_NAME)
        title.setStyleSheet("font-size: 17px; font-weight: 900; color: #38BDF8;")
        sub = QLabel("نظام الكاشير والمخازن")
        sub.setStyleSheet("font-size: 11px; color: #94A3B8;")
        brand_text.addWidget(title)
        brand_text.addWidget(sub)
        brand_layout.addLayout(brand_text)
        layout.addWidget(brand_box)

        # Menu Items
        self.add_nav_btn("cashier", "🛒 نقطة البيع (الكاشير)", layout)
        self.add_nav_btn("dashboard", "📊 لوحة التحكم (الداشبورد)", layout)
        self.add_nav_btn("inventory", "📦 المخزن وإدارة المنتجات", layout)
        self.add_nav_btn("suppliers", "🤝 إدارة المناديب والموردين", layout)
        self.add_nav_btn("rep_orders", "📦 طلبيات المناديب والمحلات", layout)
        self.add_nav_btn("users", "👤 إدارة مستخدمي الكاشير", layout)
        self.add_nav_btn("reports", "📑 مركز التقارير الشامل", layout)
        self.add_nav_btn("settings", "⚙️ الإعدادات والشبكة", layout)

        layout.addStretch()

        # Bottom Cashier Info Card
        user_card = QFrame()
        user_card.setStyleSheet("background-color: #0B0F19; border-radius: 10px; padding: 10px; border: 1px solid #1E293B;")
        user_layout = QVBoxLayout(user_card)
        
        user_row = QHBoxLayout()
        u_icon = QLabel("👤")
        u_icon.setStyleSheet("font-size: 16px; background-color: #0284C7; border-radius: 6px; padding: 2px 6px;")
        user_row.addWidget(u_icon)
        
        u_info = QVBoxLayout()
        u_name = QLabel("محمد الكاشير")
        u_name.setStyleSheet("font-size: 12.5px; font-weight: bold; color: #F8FAFC;")
        u_role = QLabel("كاشير نشط")
        u_role.setStyleSheet("font-size: 10.5px; color: #94A3B8;")
        u_info.addWidget(u_name)
        u_info.addWidget(u_role)
        user_row.addLayout(u_info)
        user_layout.addLayout(user_row)

        offline_tag = QLabel("🟢 محلي (Offline Mode)")
        offline_tag.setStyleSheet("font-size: 10px; color: #34D399; font-weight: bold; margin-top: 4px;")
        user_layout.addWidget(offline_tag)

        layout.addWidget(user_card)

        # Set default active
        self.set_active("cashier")

    def add_nav_btn(self, page_id: str, label: str, layout: QVBoxLayout):
        btn = QPushButton(label)
        btn.setCursor(Qt.PointingHandCursor)
        btn.clicked.connect(lambda: self.on_btn_clicked(page_id))
        self.buttons[page_id] = btn
        layout.addWidget(btn)

    def on_btn_clicked(self, page_id: str):
        self.set_active(page_id)
        self.page_changed.emit(page_id)

    def set_active(self, page_id: str):
        for pid, btn in self.buttons.items():
            btn.setProperty("active", "true" if pid == page_id else "false")
            btn.style().unpolish(btn)
            btn.style().polish(btn)
