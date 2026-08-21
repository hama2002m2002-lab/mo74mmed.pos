from PySide6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit, QPushButton, 
    QTableWidget, QTableWidgetItem, QHeaderView, QFrame, QScrollArea, 
    QGridLayout, QMessageBox, QDialog, QComboBox, QDoubleSpinBox
)
from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QFont, QColor
from database import Database
from config import format_currency

class PaymentDialog(QDialog):
    def __init__(self, total_amount: float, parent=None):
        super().__init__(parent)
        self.setWindowTitle("إتمام عملية البيع والدفع")
        self.setFixedSize(420, 360)
        self.setLayoutDirection(Qt.RightToLeft)
        self.total_amount = total_amount
        self.payment_data = None

        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 24, 24, 24)
        layout.setSpacing(14)

        title = QLabel("💳 إتمام عملية البيع")
        title.setStyleSheet("font-size: 18px; font-weight: bold; color: #38BDF8;")
        layout.addWidget(title)

        total_lbl = QLabel(f"المبلغ المطلوب: {format_currency(total_amount)}")
        total_lbl.setStyleSheet("font-size: 16px; font-weight: 900; color: #10B981; margin-bottom: 6px;")
        layout.addWidget(total_lbl)

        layout.addWidget(QLabel("طريقة الدفع:"))
        self.cmb_method = QComboBox()
        self.cmb_method.addItems(["نقد (Cash)", "بطاقة دفع (Card)", "آجل (Credit)"])
        layout.addWidget(self.cmb_method)

        layout.addWidget(QLabel("المبلغ المدفوع (د.ع):"))
        self.spn_paid = QDoubleSpinBox()
        self.spn_paid.setMaximum(1000000000)
        self.spn_paid.setValue(total_amount)
        self.spn_paid.setStyleSheet("font-size: 16px; font-weight: bold;")
        layout.addWidget(self.spn_paid)

        layout.addWidget(QLabel("اسم العميل / الزبون:"))
        self.txt_customer = QLineEdit("زبون نقدي")
        layout.addWidget(self.txt_customer)

        btn_layout = QHBoxLayout()
        btn_confirm = QPushButton("✔ تأكيد وطباعة الفاتورة")
        btn_confirm.setProperty("class", "success")
        btn_confirm.setFixedHeight(40)
        btn_confirm.clicked.connect(self.confirm)

        btn_cancel = QPushButton("إلغاء")
        btn_cancel.setFixedHeight(40)
        btn_cancel.clicked.connect(self.reject)

        btn_layout.addWidget(btn_confirm)
        btn_layout.addWidget(btn_cancel)
        layout.addLayout(btn_layout)

    def confirm(self):
        paid = self.spn_paid.value()
        remaining = max(0.0, self.total_amount - paid)
        self.payment_data = {
            "PaymentMethod": self.cmb_method.currentText(),
            "PaidAmount": paid,
            "RemainingAmount": remaining,
            "CustomerName": self.txt_customer.text().strip() or "زبون نقدي"
        }
        self.accept()

class CashierView(QWidget):
    def __init__(self, db: Database, parent=None):
        super().__init__(parent)
        self.db = db
        self.cart_items = []
        self.init_ui()

    def init_ui(self):
        main_layout = QHBoxLayout(self)
        main_layout.setContentsMargins(16, 16, 16, 16)
        main_layout.setSpacing(16)

        # -------------------------------------------------------------
        # LEFT AREA: Cart & Payment (40%)
        # -------------------------------------------------------------
        cart_panel = QFrame()
        cart_panel.setProperty("class", "Card")
        cart_layout = QVBoxLayout(cart_panel)
        cart_layout.setContentsMargins(14, 14, 14, 14)
        cart_layout.setSpacing(10)

        cart_title = QLabel("🛒 سلة المشتريات الحالية")
        cart_title.setStyleSheet("font-size: 16px; font-weight: bold; color: #38BDF8;")
        cart_layout.addWidget(cart_title)

        # Cart Table
        self.table_cart = QTableWidget()
        self.table_cart.setColumnCount(5)
        self.table_cart.setHorizontalHeaderLabels(["المادة", "السعر", "الكمية", "الإجمالي", "حذف"])
        self.table_cart.horizontalHeader().setSectionResizeMode(0, QHeaderView.Stretch)
        self.table_cart.horizontalHeader().setSectionResizeMode(1, QHeaderView.ResizeToContents)
        self.table_cart.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeToContents)
        self.table_cart.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeToContents)
        self.table_cart.horizontalHeader().setSectionResizeMode(4, QHeaderView.ResizeToContents)
        self.table_cart.verticalHeader().setVisible(False)
        cart_layout.addWidget(self.table_cart)

        # Total Summary Box
        summary_box = QFrame()
        summary_box.setStyleSheet("background-color: #0A0F1D; border-radius: 10px; padding: 12px; border: 1px solid #1E293B;")
        summary_layout = QVBoxLayout(summary_box)

        self.lbl_subtotal = QLabel("المجموع: 0 د.ع")
        self.lbl_subtotal.setStyleSheet("font-size: 14px; color: #94A3B8;")
        summary_layout.addWidget(self.lbl_subtotal)

        self.lbl_total = QLabel("الإجمالي الكلي: 0 د.ع")
        self.lbl_total.setStyleSheet("font-size: 20px; font-weight: 900; color: #10B981;")
        summary_layout.addWidget(self.lbl_total)

        cart_layout.addWidget(summary_box)

        # Action Buttons
        btn_pay = QPushButton("💳 دفع وإنهاء الفاتورة")
        btn_pay.setProperty("class", "success")
        btn_pay.setFixedHeight(44)
        btn_pay.setStyleSheet("font-size: 15px; font-weight: bold;")
        btn_pay.clicked.connect(self.checkout)
        cart_layout.addWidget(btn_pay)

        btn_clear = QPushButton("🗑 إفراغ السلة")
        btn_clear.setProperty("class", "danger")
        btn_clear.setFixedHeight(36)
        btn_clear.clicked.connect(self.clear_cart)
        cart_layout.addWidget(btn_clear)

        main_layout.addWidget(cart_panel, 4)

        # -------------------------------------------------------------
        # RIGHT AREA: Search & Products Grid (60%)
        # -------------------------------------------------------------
        prod_panel = QFrame()
        prod_panel.setProperty("class", "Card")
        prod_layout = QVBoxLayout(prod_panel)
        prod_layout.setContentsMargins(14, 14, 14, 14)
        prod_layout.setSpacing(12)

        # Search Bar
        search_bar = QHBoxLayout()
        self.txt_search = QLineEdit()
        self.txt_search.setPlaceholderText("🔍 ابحث باسم المادة أو امسح الباركود مباشرة...")
        self.txt_search.setFixedHeight(40)
        self.txt_search.textChanged.connect(self.search_products)
        self.txt_search.returnPressed.connect(self.handle_barcode_enter)
        search_bar.addWidget(self.txt_search)

        btn_refresh = QPushButton("🔄")
        btn_refresh.setFixedSize(40, 40)
        btn_refresh.clicked.connect(self.load_products)
        search_bar.addWidget(btn_refresh)
        prod_layout.addLayout(search_bar)

        # Products Scroll Grid
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setStyleSheet("border: none; background: transparent;")
        
        self.grid_widget = QWidget()
        self.grid_layout = QGridLayout(self.grid_widget)
        self.grid_layout.setSpacing(10)
        scroll.setWidget(self.grid_widget)
        prod_layout.addWidget(scroll)

        main_layout.addWidget(prod_panel, 6)

        # Initial Load
        self.load_products()

    def load_products(self):
        products = self.db.get_products()
        self.display_product_cards(products)

    def search_products(self, query: str):
        products = self.db.get_products(search=query.strip())
        self.display_product_cards(products)

    def handle_barcode_enter(self):
        code = self.txt_search.text().strip()
        if code:
            prod = self.db.get_product_by_barcode(code)
            if prod:
                self.add_to_cart(prod)
                self.txt_search.clear()
            else:
                prods = self.db.get_products(search=code)
                if len(prods) == 1:
                    self.add_to_cart(prods[0])
                    self.txt_search.clear()

    def display_product_cards(self, products: list):
        # Clear grid
        while self.grid_layout.count():
            item = self.grid_layout.takeAt(0)
            if item.widget():
                item.widget().deleteLater()

        cols = 3
        for i, p in enumerate(products):
            card = QPushButton()
            card.setCursor(Qt.PointingHandCursor)
            card.setFixedHeight(95)
            card.setStyleSheet("""
                QPushButton {
                    background-color: #0F172A;
                    border: 1px solid #1E293B;
                    border-radius: 10px;
                    text-align: right;
                    padding: 10px;
                }
                QPushButton:hover {
                    background-color: #1E293B;
                    border-color: #38BDF8;
                }
            """)
            
            card_layout = QVBoxLayout(card)
            card_layout.setContentsMargins(6, 6, 6, 6)

            name_lbl = QLabel(p["Name"])
            name_lbl.setStyleSheet("font-size: 13.5px; font-weight: bold; color: #F8FAFC;")
            name_lbl.setWordWrap(True)
            card_layout.addWidget(name_lbl)

            bot_layout = QHBoxLayout()
            price_lbl = QLabel(format_currency(p["Price"]))
            price_lbl.setStyleSheet("font-size: 13px; font-weight: 900; color: #34D399;")
            
            stock_lbl = QLabel(f"المخزون: {p['StockQuantity']:,.0f}")
            stock_lbl.setStyleSheet("font-size: 11px; color: #94A3B8;")

            bot_layout.addWidget(price_lbl)
            bot_layout.addStretch()
            bot_layout.addWidget(stock_lbl)
            card_layout.addLayout(bot_layout)

            card.clicked.connect(lambda _, prod=p: self.add_to_cart(prod))
            self.grid_layout.addWidget(card, i // cols, i % cols)

    def add_to_cart(self, prod: dict):
        for item in self.cart_items:
            if item["ProductId"] == prod["Id"]:
                item["Quantity"] += 1
                item["TotalPrice"] = item["Quantity"] * item["UnitPrice"]
                self.update_cart_table()
                return

        self.cart_items.append({
            "ProductId": prod["Id"],
            "ProductName": prod["Name"],
            "UnitPrice": float(prod["Price"]),
            "CostPrice": float(prod.get("Cost", 0)),
            "Quantity": 1,
            "TotalPrice": float(prod["Price"])
        })
        self.update_cart_table()

    def update_cart_table(self):
        self.table_cart.setRowCount(len(self.cart_items))
        total = 0.0

        for row, item in enumerate(self.cart_items):
            total += item["TotalPrice"]

            # Name
            self.table_cart.setItem(row, 0, QTableWidgetItem(item["ProductName"]))
            # Price
            self.table_cart.setItem(row, 1, QTableWidgetItem(f"{item['UnitPrice']:,.0f}"))
            
            # Quantity Spin
            spn_qty = QDoubleSpinBox()
            spn_qty.setValue(item["Quantity"])
            spn_qty.setMaximum(10000)
            spn_qty.valueChanged.connect(lambda val, r=row: self.update_qty(r, val))
            self.table_cart.setCellWidget(row, 2, spn_qty)

            # Total
            self.table_cart.setItem(row, 3, QTableWidgetItem(f"{item['TotalPrice']:,.0f}"))

            # Delete Button
            btn_del = QPushButton("✕")
            btn_del.setProperty("class", "danger")
            btn_del.setFixedSize(26, 26)
            btn_del.clicked.connect(lambda _, r=row: self.remove_cart_item(r))
            self.table_cart.setCellWidget(row, 4, btn_del)

        self.lbl_subtotal.setText(f"المجموع: {total:,.0f} د.ع")
        self.lbl_total.setText(f"الإجمالي الكلي: {total:,.0f} د.ع")

    def update_qty(self, row: int, val: float):
        if 0 <= row < len(self.cart_items):
            self.cart_items[row]["Quantity"] = val
            self.cart_items[row]["TotalPrice"] = val * self.cart_items[row]["UnitPrice"]
            self.update_cart_table()

    def remove_cart_item(self, row: int):
        if 0 <= row < len(self.cart_items):
            self.cart_items.pop(row)
            self.update_cart_table()

    def clear_cart(self):
        self.cart_items.clear()
        self.update_cart_table()

    def checkout(self):
        if not self.cart_items:
            QMessageBox.warning(self, "تنبيه", "سلة المشتريات فارغة!")
            return

        total = sum(i["TotalPrice"] for i in self.cart_items)
        dlg = PaymentDialog(total, self)
        if dlg.exec() == QDialog.Accepted and dlg.payment_data:
            sale_data = {
                "TotalAmount": total,
                "DiscountAmount": 0,
                "PaidAmount": dlg.payment_data["PaidAmount"],
                "RemainingAmount": dlg.payment_data["RemainingAmount"],
                "PaymentMethod": dlg.payment_data["PaymentMethod"],
                "CustomerName": dlg.payment_data["CustomerName"]
            }
            sale_id = self.db.create_sale(sale_data, self.cart_items)
            self.clear_cart()
            self.load_products()
            QMessageBox.information(self, "نجاح البيع", f"✔ تم حفظ الفاتورة بنجاح!\nالمبلغ: {total:,.0f} د.ع")
