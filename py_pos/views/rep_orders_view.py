from PySide6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit, QPushButton, 
    QTableWidget, QTableWidgetItem, QHeaderView, QFrame, QMessageBox, 
    QDialog, QComboBox
)
from PySide6.QtCore import Qt, QThread, Signal
from database import Database
from cloud_sync import CloudSyncService
from printer import InvoicePrinter
from config import format_currency

class SyncWorker(QThread):
    sync_finished = Signal(int)

    def __init__(self, sync_service: CloudSyncService):
        super().__init__()
        self.sync_service = sync_service

    def run(self):
        new_count = self.sync_service.sync_orders()
        self.sync_finished.emit(new_count)

class OrderDetailsDialog(QDialog):
    def __init__(self, db: Database, order: dict, parent=None):
        super().__init__(parent)
        self.db = db
        self.order = order
        self.setWindowTitle(f"تفاصيل الطلبية: {order['OrderNumber']}")
        self.setFixedSize(650, 520)
        self.setLayoutDirection(Qt.RightToLeft)

        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 24, 24, 24)
        layout.setSpacing(14)

        title = QLabel(f"📦 تفاصيل الطلبية: {order['OrderNumber']}")
        title.setStyleSheet("font-size: 16px; font-weight: bold; color: #38BDF8;")
        layout.addWidget(title)

        # Meta Info
        meta_box = QFrame()
        meta_box.setStyleSheet("background-color: #0A0F1D; border-radius: 8px; padding: 10px;")
        meta_layout = QGridLayout(meta_box)
        meta_layout.addWidget(QLabel(f"<b>المحل:</b> {order.get('StoreName') or order.get('CustomerName', '---')}"), 0, 0)
        meta_layout.addWidget(QLabel(f"<b>المندوب:</b> {order.get('RepName', '---')}"), 0, 1)
        meta_layout.addWidget(QLabel(f"<b>الهاتف:</b> {order.get('RepPhone', '---')}"), 1, 0)
        meta_layout.addWidget(QLabel(f"<b>العنوان:</b> {order.get('StoreAddress', '---')}"), 1, 1)
        meta_layout.addWidget(QLabel(f"<b>التاريخ:</b> {order.get('CreatedAt', '')[:16]}"), 2, 0)
        meta_layout.addWidget(QLabel(f"<b>الحالة:</b> {order.get('Status', 'Pending')}"), 2, 1)
        layout.addWidget(meta_box)

        # Items Table
        items_lbl = QLabel("📋 المواد المطلوبة في هذه الطلبية:")
        items_lbl.setStyleSheet("font-weight: bold; color: #F8FAFC;")
        layout.addWidget(items_lbl)

        self.table_items = QTableWidget()
        self.table_items.setColumnCount(4)
        self.table_items.setHorizontalHeaderLabels(["المادة", "سعر الوحدة", "الكمية", "الإجمالي"])
        self.table_items.horizontalHeader().setSectionResizeMode(0, QHeaderView.Stretch)
        self.table_items.horizontalHeader().setSectionResizeMode(1, QHeaderView.ResizeToContents)
        self.table_items.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeToContents)
        self.table_items.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeToContents)
        self.table_items.verticalHeader().setVisible(False)
        layout.addWidget(self.table_items)

        items = order.get("Items", [])
        self.table_items.setRowCount(len(items))
        for row, item in enumerate(items):
            self.table_items.setItem(row, 0, QTableWidgetItem(item.get("ProductName", "")))
            self.table_items.setItem(row, 1, QTableWidgetItem(format_currency(item.get("UnitPrice", 0))))
            self.table_items.setItem(row, 2, QTableWidgetItem(f"{item.get('Quantity', 1):,.0f}"))
            self.table_items.setItem(row, 3, QTableWidgetItem(format_currency(item.get("TotalPrice", 0))))

        # Actions
        btn_layout = QHBoxLayout()
        btn_print = QPushButton("🖨 طباعة وتصدير وصل A4 (PDF)")
        btn_print.setProperty("class", "primary")
        btn_print.setFixedHeight(40)
        btn_print.clicked.connect(self.print_a4)

        btn_deliver = QPushButton("✔ تسليم وتجهيز الطلبية")
        btn_deliver.setProperty("class", "success")
        btn_deliver.setFixedHeight(40)
        btn_deliver.clicked.connect(self.mark_delivered)

        btn_close = QPushButton("إغلاق")
        btn_close.setFixedHeight(40)
        btn_close.clicked.connect(self.accept)

        btn_layout.addWidget(btn_print)
        btn_layout.addWidget(btn_deliver)
        btn_layout.addWidget(btn_close)
        layout.addLayout(btn_layout)

    def print_a4(self):
        pdf_path = InvoicePrinter.generate_order_a4_pdf(self.order, self.order.get("Items", []))
        InvoicePrinter.open_pdf(pdf_path)
        QMessageBox.information(self, "تم إنشاء الوصل", f"✔ تم إنشاء وصل A4 PDF وحفظه بنجاح!\nالمسار: {pdf_path}")

    def mark_delivered(self):
        self.db.update_order_status(self.order["Id"], "Delivered")
        self.order["Status"] = "Delivered"
        QMessageBox.information(self, "تم التسليم", "✔ تم تحديث حالة الطلبية إلى 'تم التسليم'!")
        self.accept()

class RepOrdersView(QWidget):
    def __init__(self, db: Database, parent=None):
        super().__init__(parent)
        self.db = db
        self.cloud_sync = CloudSyncService(db)
        self.orders = []
        self.init_ui()

    def init_ui(self):
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(20, 20, 20, 20)
        main_layout.setSpacing(14)

        # Header Bar
        header = QHBoxLayout()
        title = QLabel("📦 طلبيات المناديب والمحلات السحابية")
        title.setStyleSheet("font-size: 20px; font-weight: bold; color: #F8FAFC;")
        header.addWidget(title)
        header.addStretch()

        self.txt_search = QLineEdit()
        self.txt_search.setPlaceholderText("🔍 ابحث برقم الطلبية أو المحل أو المندوب...")
        self.txt_search.setFixedWidth(280)
        self.txt_search.textChanged.connect(self.search)
        header.addWidget(self.txt_search)

        self.btn_sync = QPushButton("☁️ مزامنة وسحب الطلبيات")
        self.btn_sync.setProperty("class", "primary")
        self.btn_sync.setFixedHeight(38)
        self.btn_sync.clicked.connect(self.start_sync)
        header.addWidget(self.btn_sync)

        btn_refresh = QPushButton("🔄")
        btn_refresh.setFixedSize(38, 38)
        btn_refresh.clicked.connect(self.load_orders)
        header.addWidget(btn_refresh)
        main_layout.addLayout(header)

        # Orders Table
        self.table = QTableWidget()
        self.table.setColumnCount(7)
        self.table.setHorizontalHeaderLabels(["رقم الطلبية", "المحل / العميل", "المندوب", "المبلغ الكلي", "الحالة", "التاريخ", "الإجراء"])
        self.table.horizontalHeader().setSectionResizeMode(0, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(1, QHeaderView.Stretch)
        self.table.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(4, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(5, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(6, QHeaderView.ResizeToContents)
        self.table.verticalHeader().setVisible(False)
        main_layout.addWidget(self.table)

        # Initial Load & Background Sync
        self.load_orders()
        self.start_sync()

    def load_orders(self):
        self.orders = self.db.get_rep_orders()
        self.display_orders(self.orders)

    def search(self, query: str):
        filtered = [o for o in self.orders if query.lower() in o["OrderNumber"].lower() or query.lower() in (o.get("StoreName") or "").lower() or query.lower() in (o.get("RepName") or "").lower()]
        self.display_orders(filtered)

    def display_orders(self, orders_list: list):
        self.table.setRowCount(len(orders_list))
        for row, o in enumerate(orders_list):
            self.table.setItem(row, 0, QTableWidgetItem(o["OrderNumber"]))
            self.table.setItem(row, 1, QTableWidgetItem(o.get("StoreName") or o.get("CustomerName", "---")))
            self.table.setItem(row, 2, QTableWidgetItem(o.get("RepName", "---")))
            self.table.setItem(row, 3, QTableWidgetItem(format_currency(o.get("TotalAmount", 0))))

            # Status Badge
            status = o.get("Status", "Pending")
            st_item = QTableWidgetItem("مكتمل ✔" if status == "Delivered" else "معلق ⏳")
            st_item.setForeground(Qt.green if status == "Delivered" else Qt.yellow)
            self.table.setItem(row, 4, st_item)

            self.table.setItem(row, 5, QTableWidgetItem(o.get("CreatedAt", "")[:16].replace("T", " ")))

            # Action Button
            btn_view = QPushButton("👁 عرض الوصل والتسليم")
            btn_view.setProperty("class", "primary")
            btn_view.clicked.connect(lambda _, ord_item=o: self.view_order(ord_item))
            self.table.setCellWidget(row, 6, btn_view)

    def view_order(self, order: dict):
        dlg = OrderDetailsDialog(self.db, order, parent=self)
        if dlg.exec() == QDialog.Accepted:
            self.load_orders()

    def start_sync(self):
        self.btn_sync.setText("⏳ جاري المزامنة...")
        self.btn_sync.setEnabled(False)
        self.worker = SyncWorker(self.cloud_sync)
        self.worker.sync_finished.connect(self.on_sync_finished)
        self.worker.start()

    def on_sync_finished(self, new_count: int):
        self.btn_sync.setText("☁️ مزامنة وسحب الطلبيات")
        self.btn_sync.setEnabled(True)
        if new_count > 0:
            self.load_orders()
            QMessageBox.information(self, "مزامنة سحابية", f"🔔 تم سحب {new_count} طلبيات جديدة من السحابة بنجاح!")
        else:
            self.load_orders()
