from PySide6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit, QPushButton, 
    QTableWidget, QTableWidgetItem, QHeaderView, QFrame, QMessageBox, 
    QDialog, QDoubleSpinBox, QComboBox
)
from PySide6.QtCore import Qt
from database import Database
from config import format_currency

class ProductDialog(QDialog):
    def __init__(self, db: Database, product: dict = None, parent=None):
        super().__init__(parent)
        self.db = db
        self.product = product
        self.setWindowTitle("إضافة مادة جديدة" if not product else f"تعديل مادة: {product['Name']}")
        self.setFixedSize(460, 480)
        self.setLayoutDirection(Qt.RightToLeft)

        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 24, 24, 24)
        layout.setSpacing(12)

        title = QLabel("📦 بيانات المادة والمخزون")
        title.setStyleSheet("font-size: 16px; font-weight: bold; color: #38BDF8;")
        layout.addWidget(title)

        layout.addWidget(QLabel("اسم المادة: *"))
        self.txt_name = QLineEdit(product.get("Name", "") if product else "")
        layout.addWidget(self.txt_name)

        layout.addWidget(QLabel("الباركود:"))
        self.txt_barcode = QLineEdit(product.get("Barcode", "") if product else "")
        layout.addWidget(self.txt_barcode)

        # Price & Cost Row
        price_row = QHBoxLayout()
        vbox_price = QVBoxLayout()
        vbox_price.addWidget(QLabel("سعر البيع (د.ع): *"))
        self.spn_price = QDoubleSpinBox()
        self.spn_price.setMaximum(1000000000)
        self.spn_price.setValue(float(product.get("Price", 0)) if product else 0)
        vbox_price.addWidget(self.spn_price)
        price_row.addLayout(vbox_price)

        vbox_cost = QVBoxLayout()
        vbox_cost.addWidget(QLabel("سعر التكلفة (د.ع):"))
        self.spn_cost = QDoubleSpinBox()
        self.spn_cost.setMaximum(1000000000)
        self.spn_cost.setValue(float(product.get("Cost", 0)) if product else 0)
        vbox_cost.addWidget(self.spn_cost)
        price_row.addLayout(vbox_cost)
        layout.addLayout(price_row)

        # Stock & Alert Row
        stock_row = QHBoxLayout()
        vbox_stock = QVBoxLayout()
        vbox_stock.addWidget(QLabel("الكمية المتوفرة بالمخزن:"))
        self.spn_stock = QDoubleSpinBox()
        self.spn_stock.setMaximum(1000000)
        self.spn_stock.setValue(float(product.get("StockQuantity", 0)) if product else 0)
        vbox_stock.addWidget(self.spn_stock)
        stock_row.addLayout(vbox_stock)

        vbox_alert = QVBoxLayout()
        vbox_alert.addWidget(QLabel("حد تنبيه نقص المخزون:"))
        self.spn_alert = QDoubleSpinBox()
        self.spn_alert.setMaximum(100000)
        self.spn_alert.setValue(float(product.get("MinStockAlert", 5)) if product else 5)
        vbox_alert.addWidget(self.spn_alert)
        stock_row.addLayout(vbox_alert)
        layout.addLayout(stock_row)

        # Action Buttons
        btn_layout = QHBoxLayout()
        btn_save = QPushButton("💾 حفظ بيانات المادة")
        btn_save.setProperty("class", "success")
        btn_save.setFixedHeight(40)
        btn_save.clicked.connect(self.save)

        btn_cancel = QPushButton("إلغاء")
        btn_cancel.setFixedHeight(40)
        btn_cancel.clicked.connect(self.reject)

        btn_layout.addWidget(btn_save)
        btn_layout.addWidget(btn_cancel)
        layout.addLayout(btn_layout)

    def save(self):
        name = self.txt_name.text().strip()
        if not name:
            QMessageBox.warning(self, "تنبيه", "يرجى كتابة اسم المادة.")
            return

        prod_data = {
            "Id": self.product["Id"] if self.product else None,
            "Name": name,
            "Barcode": self.txt_barcode.text().strip(),
            "Price": self.spn_price.value(),
            "Cost": self.spn_cost.value(),
            "StockQuantity": self.spn_stock.value(),
            "MinStockAlert": self.spn_alert.value(),
        }
        self.db.save_product(prod_data)
        self.accept()

class InventoryView(QWidget):
    def __init__(self, db: Database, parent=None):
        super().__init__(parent)
        self.db = db
        self.products = []
        self.init_ui()

    def init_ui(self):
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(20, 20, 20, 20)
        main_layout.setSpacing(14)

        # Header Bar
        header = QHBoxLayout()
        title = QLabel("📦 إدارة المخزن والمنتجات")
        title.setStyleSheet("font-size: 20px; font-weight: bold; color: #F8FAFC;")
        header.addWidget(title)
        header.addStretch()

        self.txt_search = QLineEdit()
        self.txt_search.setPlaceholderText("🔍 ابحث بالاسم أو الباركود...")
        self.txt_search.setFixedWidth(280)
        self.txt_search.textChanged.connect(self.search)
        header.addWidget(self.txt_search)

        btn_add = QPushButton("➕ إضافة مادة جديدة")
        btn_add.setProperty("class", "success")
        btn_add.setFixedHeight(38)
        btn_add.clicked.connect(self.add_product)
        header.addWidget(btn_add)

        btn_refresh = QPushButton("🔄")
        btn_refresh.setFixedSize(38, 38)
        btn_refresh.clicked.connect(self.load_products)
        header.addWidget(btn_refresh)
        main_layout.addLayout(header)

        # Products Table
        self.table = QTableWidget()
        self.table.setColumnCount(7)
        self.table.setHorizontalHeaderLabels(["اسم المادة", "الباركود", "سعر التكلفة", "سعر البيع", "الكمية بالمخزن", "تعديل", "حذف"])
        self.table.horizontalHeader().setSectionResizeMode(0, QHeaderView.Stretch)
        self.table.horizontalHeader().setSectionResizeMode(1, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(4, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(5, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(6, QHeaderView.ResizeToContents)
        self.table.verticalHeader().setVisible(False)
        main_layout.addWidget(self.table)

        # Initial Load
        self.load_products()

    def load_products(self):
        self.products = self.db.get_products()
        self.display_products(self.products)

    def search(self, query: str):
        filtered = [p for p in self.products if query.lower() in p["Name"].lower() or query in (p["Barcode"] or "")]
        self.display_products(filtered)

    def display_products(self, prods: list):
        self.table.setRowCount(len(prods))
        for row, p in enumerate(prods):
            self.table.setItem(row, 0, QTableWidgetItem(p["Name"]))
            self.table.setItem(row, 1, QTableWidgetItem(p["Barcode"] or "---"))
            self.table.setItem(row, 2, QTableWidgetItem(format_currency(p["Cost"])))
            self.table.setItem(row, 3, QTableWidgetItem(format_currency(p["Price"])))
            
            # Stock Quantity
            stock_item = QTableWidgetItem(f"{p['StockQuantity']:,.0f}")
            if p["StockQuantity"] <= p.get("MinStockAlert", 5):
                stock_item.setForeground(Qt.red)
            self.table.setItem(row, 4, stock_item)

            # Edit Button
            btn_edit = QPushButton("✏️ تعديل")
            btn_edit.clicked.connect(lambda _, prod=p: self.edit_product(prod))
            self.table.setCellWidget(row, 5, btn_edit)

            # Delete Button
            btn_del = QPushButton("🗑 حذف")
            btn_del.setProperty("class", "danger")
            btn_del.clicked.connect(lambda _, prod=p: self.delete_product(prod))
            self.table.setCellWidget(row, 6, btn_del)

    def add_product(self):
        dlg = ProductDialog(self.db, parent=self)
        if dlg.exec() == QDialog.Accepted:
            self.load_products()
            QMessageBox.information(self, "نجاح", "✔ تم إضافة المادة بنجاح!")

    def edit_product(self, prod: dict):
        dlg = ProductDialog(self.db, product=prod, parent=self)
        if dlg.exec() == QDialog.Accepted:
            self.load_products()
            QMessageBox.information(self, "نجاح", "✔ تم حفظ التعديلات بنجاح!")

    def delete_product(self, prod: dict):
        res = QMessageBox.question(self, "تأكيد الحذف", f"هل أنت متأكد من حذف المادة '{prod['Name']}'؟")
        if res == QMessageBox.Yes:
            self.db.delete_product(prod["Id"])
            self.load_products()
            QMessageBox.information(self, "تم الحذف", "تم حذف المادة بنجاح.")
