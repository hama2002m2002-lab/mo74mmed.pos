import sys
import os
from datetime import datetime
from PySide6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QHBoxLayout, QVBoxLayout, 
    QStackedWidget, QLabel, QPushButton, QFrame
)
from PySide6.QtCore import Qt, QTimer
from PySide6.QtGui import QFont, QIcon

# Ensure local imports work
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from database import Database
from config import APP_TITLE, APP_NAME, APP_VERSION
from styles import DARK_STYLE
from views.sidebar import Sidebar
from views.cashier_view import CashierView
from views.dashboard_view import DashboardView
from views.inventory_view import InventoryView
from views.suppliers_view import SuppliersView
from views.rep_orders_view import RepOrdersView
from views.users_view import UsersView
from views.reports_view import ReportsView
from views.settings_view import SettingsView

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle(APP_TITLE)
        self.resize(1300, 850)
        self.setMinimumSize(1100, 700)
        self.setLayoutDirection(Qt.RightToLeft)

        # Initialize Database
        self.db = Database()

        # Central Layout
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        main_layout = QHBoxLayout(central_widget)
        main_layout.setContentsMargins(0, 0, 0, 0)
        main_layout.setSpacing(0)

        # 1. Sidebar Navigation
        self.sidebar = Sidebar()
        self.sidebar.page_changed.connect(self.switch_page)
        main_layout.addWidget(self.sidebar)

        # 2. Main Content Area (Topbar + Stacked Pages)
        content_container = QWidget()
        content_layout = QVBoxLayout(content_container)
        content_layout.setContentsMargins(0, 0, 0, 0)
        content_layout.setSpacing(0)

        # Topbar
        topbar = QFrame()
        topbar.setObjectName("topbar")
        topbar_layout = QHBoxLayout(topbar)
        topbar_layout.setContentsMargins(16, 0, 16, 0)

        # Clock
        self.lbl_clock = QLabel()
        self.lbl_clock.setStyleSheet("color: #94A3B8; font-size: 12.5px; font-weight: bold;")
        self.update_clock()
        topbar_layout.addWidget(self.lbl_clock)

        topbar_layout.addStretch()

        # Language & Theme Badges
        badge_currency = QLabel("العملة: الدنانير العراقي (د.ع)")
        badge_currency.setStyleSheet("background-color: #0F2D20; color: #34D399; font-size: 11.5px; font-weight: bold; padding: 4px 10px; border-radius: 6px;")
        topbar_layout.addWidget(badge_currency)

        badge_lang = QLabel("🌐 العربية")
        badge_lang.setStyleSheet("background-color: #0369A1; color: #FFFFFF; font-size: 11.5px; font-weight: bold; padding: 4px 10px; border-radius: 6px; margin: 0 8px;")
        topbar_layout.addWidget(badge_lang)

        content_layout.addWidget(topbar)

        # Stacked Pages
        self.stack = QStackedWidget()
        self.views = {
            "cashier": CashierView(self.db),
            "dashboard": DashboardView(self.db),
            "inventory": InventoryView(self.db),
            "suppliers": SuppliersView(self.db),
            "rep_orders": RepOrdersView(self.db),
            "users": UsersView(self.db),
            "reports": ReportsView(self.db),
            "settings": SettingsView(self.db),
        }

        for view in self.views.values():
            self.stack.addWidget(view)

        content_layout.addWidget(self.stack)
        main_layout.addWidget(content_container)

        # Clock Timer
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.update_clock)
        self.timer.start(1000)

    def update_clock(self):
        now = datetime.now().strftime("%Y/%m/%d - %I:%M:%S %p")
        self.lbl_clock.setText(f"🕒 {now}")

    def switch_page(self, page_id: str):
        if page_id in self.views:
            target_view = self.views[page_id]
            self.stack.setCurrentWidget(target_view)
            # Trigger auto-refresh on page activation if method exists
            if hasattr(target_view, "load_data"):
                target_view.load_data()
            elif hasattr(target_view, "load_products"):
                target_view.load_products()
            elif hasattr(target_view, "load_suppliers"):
                target_view.load_suppliers()
            elif hasattr(target_view, "load_orders"):
                target_view.load_orders()
            elif hasattr(target_view, "load_users"):
                target_view.load_users()
            elif hasattr(target_view, "load_reports"):
                target_view.load_reports()

def main():
    app = QApplication(sys.argv)
    app.setStyleSheet(DARK_STYLE)
    
    # Set default font
    font = QFont("Segoe UI", 10)
    app.setFont(font)

    window = MainWindow()
    window.show()
    sys.exit(app.exec())

if __name__ == "__main__":
    main()
