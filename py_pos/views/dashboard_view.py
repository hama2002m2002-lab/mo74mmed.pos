from PySide6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel, QPushButton, 
    QTableWidget, QTableWidgetItem, QHeaderView, QFrame, QScrollArea, QGridLayout
)
from PySide6.QtCore import Qt
from database import Database
from config import format_currency

class DashboardView(QWidget):
    def __init__(self, db: Database, parent=None):
        super().__init__(parent)
        self.db = db
        self.init_ui()

    def init_ui(self):
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(20, 20, 20, 20)
        main_layout.setSpacing(16)

        # Header Title & Refresh
        header = QHBoxLayout()
        title_box = QVBoxLayout()
        title = QLabel("📊 لوحة التحكم والإحصائيات العامة")
        title.setStyleSheet("font-size: 20px; font-weight: bold; color: #F8FAFC;")
        sub = QLabel("نظرة شاملة وسريعة على المبيعات والمخزون وحركة اليوم")
        sub.setStyleSheet("font-size: 12px; color: #94A3B8;")
        title_box.addWidget(title)
        title_box.addWidget(sub)
        header.addLayout(title_box)
        header.addStretch()

        btn_refresh = QPushButton("🔄 تحديث البيانات")
        btn_refresh.setFixedHeight(38)
        btn_refresh.clicked.connect(self.load_data)
        header.addWidget(btn_refresh)
        main_layout.addLayout(header)

        # KPI Cards Grid
        kpi_grid = QGridLayout()
        kpi_grid.setSpacing(14)

        # 1. Today Revenue
        self.card_today_rev = self.create_kpi_card("💵", "مبيعات اليوم", "0 د.ع", "#064E3B", "#34D399")
        kpi_grid.addWidget(self.card_today_rev, 0, 0)

        # 2. Today Invoices
        self.card_today_inv = self.create_kpi_card("🧾", "عدد وصولات اليوم", "0 وصل", "#1E3A8A", "#38BDF8")
        kpi_grid.addWidget(self.card_today_inv, 0, 1)

        # 3. Monthly Revenue
        self.card_month_rev = self.create_kpi_card("📈", "مبيعات الشهر الحالي", "0 د.ع", "#312E81", "#818CF8")
        kpi_grid.addWidget(self.card_month_rev, 0, 2)

        # 4. Low Stock Alert
        self.card_low_stock = self.create_kpi_card("⚠️", "أصناف منخفضة المخزون", "0 صنف", "#450A0A", "#F87171")
        kpi_grid.addWidget(self.card_low_stock, 0, 3)

        main_layout.addLayout(kpi_grid)

        # Content Split: Recent Sales & Low Stock List
        tables_layout = QHBoxLayout()
        tables_layout.setSpacing(16)

        # Recent Sales (Left 60%)
        sales_panel = QFrame()
        sales_panel.setProperty("class", "Card")
        sales_vbox = QVBoxLayout(sales_panel)
        sales_lbl = QLabel("📑 أحدث فواتير البيع المسجلة")
        sales_lbl.setStyleSheet("font-size: 15px; font-weight: bold; color: #38BDF8;")
        sales_vbox.addWidget(sales_lbl)

        self.table_recent_sales = QTableWidget()
        self.table_recent_sales.setColumnCount(4)
        self.table_recent_sales.setHorizontalHeaderLabels(["رقم الفاتورة", "العميل", "المبلغ", "التاريخ"])
        self.table_recent_sales.horizontalHeader().setSectionResizeMode(0, QHeaderView.ResizeToContents)
        self.table_recent_sales.horizontalHeader().setSectionResizeMode(1, QHeaderView.Stretch)
        self.table_recent_sales.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeToContents)
        self.table_recent_sales.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeToContents)
        self.table_recent_sales.verticalHeader().setVisible(False)
        sales_vbox.addWidget(self.table_recent_sales)
        tables_layout.addWidget(sales_panel, 6)

        # Low Stock (Right 40%)
        stock_panel = QFrame()
        stock_panel.setProperty("class", "Card")
        stock_vbox = QVBoxLayout(stock_panel)
        stock_lbl = QLabel("⚠️ تنبيهات نقص المخزون")
        stock_lbl.setStyleSheet("font-size: 15px; font-weight: bold; color: #F87171;")
        stock_vbox.addWidget(stock_lbl)

        self.table_low_stock = QTableWidget()
        self.table_low_stock.setColumnCount(3)
        self.table_low_stock.setHorizontalHeaderLabels(["المادة", "المتبقي", "سعر البيع"])
        self.table_low_stock.horizontalHeader().setSectionResizeMode(0, QHeaderView.Stretch)
        self.table_low_stock.horizontalHeader().setSectionResizeMode(1, QHeaderView.ResizeToContents)
        self.table_low_stock.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeToContents)
        self.table_low_stock.verticalHeader().setVisible(False)
        stock_vbox.addWidget(self.table_low_stock)
        tables_layout.addWidget(stock_panel, 4)

        main_layout.addLayout(tables_layout)

        # Initial Load
        self.load_data()

    def create_kpi_card(self, icon: str, title: str, value: str, bg_color: str, text_color: str) -> QFrame:
        card = QFrame()
        card.setProperty("class", "Card")
        card_layout = QVBoxLayout(card)
        card_layout.setContentsMargins(14, 14, 14, 14)

        top_layout = QHBoxLayout()
        icon_box = QLabel(icon)
        icon_box.setStyleSheet(f"background-color: {bg_color}; font-size: 18px; padding: 6px 10px; border-radius: 8px;")
        top_layout.addWidget(icon_box)
        
        t_lbl = QLabel(title)
        t_lbl.setStyleSheet("font-size: 12.5px; color: #94A3B8; font-weight: bold;")
        top_layout.addWidget(t_lbl)
        top_layout.addStretch()
        card_layout.addLayout(top_layout)

        val_lbl = QLabel(value)
        val_lbl.setObjectName("val_label")
        val_lbl.setStyleSheet(f"font-size: 22px; font-weight: 900; color: {text_color}; margin-top: 10px;")
        card_layout.addWidget(val_lbl)

        return card

    def load_data(self):
        stats = self.db.get_dashboard_stats()

        # Update KPI values
        self.card_today_rev.findChild(QLabel, "val_label").setText(format_currency(stats["TodayRevenue"]))
        self.card_today_inv.findChild(QLabel, "val_label").setText(f"{stats['TodayInvoicesCount']} وصل")
        self.card_month_rev.findChild(QLabel, "val_label").setText(format_currency(stats["MonthlyRevenue"]))
        self.card_low_stock.findChild(QLabel, "val_label").setText(f"{stats['LowStockCount']} صنف")

        # Update Recent Sales Table
        sales = stats["RecentSales"]
        self.table_recent_sales.setRowCount(len(sales))
        for row, s in enumerate(sales):
            self.table_recent_sales.setItem(row, 0, QTableWidgetItem(s["InvoiceNumber"]))
            self.table_recent_sales.setItem(row, 1, QTableWidgetItem(s["CustomerName"] or "زبون نقدي"))
            self.table_recent_sales.setItem(row, 2, QTableWidgetItem(format_currency(s["TotalAmount"])))
            self.table_recent_sales.setItem(row, 3, QTableWidgetItem(s["CreatedAt"][:16].replace("T", " ")))

        # Update Low Stock Table
        low_stock = stats["LowStockProducts"]
        self.table_low_stock.setRowCount(len(low_stock))
        for row, p in enumerate(low_stock):
            self.table_low_stock.setItem(row, 0, QTableWidgetItem(p["Name"]))
            self.table_low_stock.setItem(row, 1, QTableWidgetItem(f"{p['StockQuantity']:,.0f}"))
            self.table_low_stock.setItem(row, 2, QTableWidgetItem(format_currency(p["Price"])))
