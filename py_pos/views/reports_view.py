from PySide6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit, QPushButton, 
    QTableWidget, QTableWidgetItem, QHeaderView, QFrame, QGridLayout
)
from PySide6.QtCore import Qt
from database import Database
from config import format_currency

class ReportsView(QWidget):
    def __init__(self, db: Database, parent=None):
        super().__init__(parent)
        self.db = db
        self.init_ui()

    def init_ui(self):
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(20, 20, 20, 20)
        main_layout.setSpacing(16)

        # Header Bar
        header = QHBoxLayout()
        title = QLabel("📑 مركز التقارير الشامل والمبيعات")
        title.setStyleSheet("font-size: 20px; font-weight: bold; color: #F8FAFC;")
        header.addWidget(title)
        header.addStretch()

        self.txt_search = QLineEdit()
        self.txt_search.setPlaceholderText("🔍 ابحث برقم الفاتورة أو العميل...")
        self.txt_search.setFixedWidth(280)
        self.txt_search.textChanged.connect(self.search)
        header.addWidget(self.txt_search)

        btn_refresh = QPushButton("🔄 تحديث")
        btn_refresh.setFixedHeight(38)
        btn_refresh.clicked.connect(self.load_reports)
        header.addWidget(btn_refresh)
        main_layout.addLayout(header)

        # KPI Summary Row
        kpi_box = QFrame()
        kpi_box.setProperty("class", "Card")
        kpi_layout = QGridLayout(kpi_box)

        self.lbl_total_sales = QLabel("إجمالي المبيعات: 0 د.ع")
        self.lbl_total_sales.setStyleSheet("font-size: 16px; font-weight: bold; color: #10B981;")
        kpi_layout.addWidget(self.lbl_total_sales, 0, 0)

        self.lbl_sales_count = QLabel("عدد العمليات: 0")
        self.lbl_sales_count.setStyleSheet("font-size: 16px; font-weight: bold; color: #38BDF8;")
        kpi_layout.addWidget(self.lbl_sales_count, 0, 1)

        main_layout.addWidget(kpi_box)

        # Sales Table
        self.table = QTableWidget()
        self.table.setColumnCount(6)
        self.table.setHorizontalHeaderLabels(["رقم الفاتورة", "العميل", "الكاشير", "المبلغ", "طريقة الدفع", "التاريخ"])
        self.table.horizontalHeader().setSectionResizeMode(0, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(1, QHeaderView.Stretch)
        self.table.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(4, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(5, QHeaderView.ResizeToContents)
        self.table.verticalHeader().setVisible(False)
        main_layout.addWidget(self.table)

        self.load_reports()

    def load_reports(self):
        self.sales = self.db.get_sales(limit=500)
        self.display_sales(self.sales)

    def search(self, query: str):
        filtered = [s for s in self.sales if query.lower() in s["InvoiceNumber"].lower() or query.lower() in (s.get("CustomerName") or "").lower() or query.lower() in (s.get("CashierName") or "").lower()]
        self.display_sales(filtered)

    def display_sales(self, sales_list: list):
        self.table.setRowCount(len(sales_list))
        total_sum = sum(s["TotalAmount"] for s in sales_list)

        self.lbl_total_sales.setText(f"إجمالي المبيعات: {format_currency(total_sum)}")
        self.lbl_sales_count.setText(f"عدد العمليات: {len(sales_list)} عملية")

        for row, s in enumerate(sales_list):
            self.table.setItem(row, 0, QTableWidgetItem(s["InvoiceNumber"]))
            self.table.setItem(row, 1, QTableWidgetItem(s["CustomerName"] or "زبون نقدي"))
            self.table.setItem(row, 2, QTableWidgetItem(s["CashierName"] or "---"))
            self.table.setItem(row, 3, QTableWidgetItem(format_currency(s["TotalAmount"])))
            self.table.setItem(row, 4, QTableWidgetItem(s.get("PaymentMethod", "Cash")))
            self.table.setItem(row, 5, QTableWidgetItem(s["CreatedAt"][:16].replace("T", " ")))
