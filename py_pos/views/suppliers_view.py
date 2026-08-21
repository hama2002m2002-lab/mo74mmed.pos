from PySide6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit, QPushButton, 
    QTableWidget, QTableWidgetItem, QHeaderView, QFrame, QMessageBox, 
    QDialog, QDoubleSpinBox
)
from PySide6.QtCore import Qt
from database import Database
from config import format_currency

class AddSupplierDialog(QDialog):
    def __init__(self, db: Database, parent=None):
        super().__init__(parent)
        self.db = db
        self.setWindowTitle("إضافة مندوب أو مورد جديد")
        self.setFixedSize(440, 420)
        self.setLayoutDirection(Qt.RightToLeft)

        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 24, 24, 24)
        layout.setSpacing(12)

        title = QLabel("🤝 إضافة مندوب جديد")
        title.setStyleSheet("font-size: 16px; font-weight: bold; color: #38BDF8;")
        layout.addWidget(title)

        layout.addWidget(QLabel("اسم المندوب: *"))
        self.txt_name = QLineEdit()
        layout.addWidget(self.txt_name)

        layout.addWidget(QLabel("اسم الشركة / الجهة:"))
        self.txt_company = QLineEdit()
        layout.addWidget(self.txt_company)

        layout.addWidget(QLabel("رقم الهاتف:"))
        self.txt_phone = QLineEdit()
        layout.addWidget(self.txt_phone)

        layout.addWidget(QLabel("الرصيد الافتتاحي (د.ع):"))
        self.spn_balance = QDoubleSpinBox()
        self.spn_balance.setMaximum(1000000000)
        layout.addWidget(self.spn_balance)

        btn_layout = QHBoxLayout()
        btn_save = QPushButton("💾 حفظ المندوب")
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
            QMessageBox.warning(self, "تنبيه", "يرجى كتابة اسم المندوب.")
            return

        sup_data = {
            "Name": name,
            "Company": self.txt_company.text().strip(),
            "Phone": self.txt_phone.text().strip(),
            "Balance": self.spn_balance.value(),
            "OpeningBalance": self.spn_balance.value(),
        }
        self.db.save_supplier(sup_data)
        self.accept()

class SupplierPaymentDialog(QDialog):
    def __init__(self, db: Database, supplier: dict, parent=None):
        super().__init__(parent)
        self.db = db
        self.supplier = supplier
        self.setWindowTitle(f"تسجيل دفعة نقدية: {supplier['Name']}")
        self.setFixedSize(420, 320)
        self.setLayoutDirection(Qt.RightToLeft)

        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 24, 24, 24)
        layout.setSpacing(14)

        title = QLabel(f"💳 تسجيل دفعة للمندوب: {supplier['Name']}")
        title.setStyleSheet("font-size: 16px; font-weight: bold; color: #10B981;")
        layout.addWidget(title)

        bal_lbl = QLabel(f"الرصيد الحالي المستحق: {format_currency(supplier['Balance'])}")
        bal_lbl.setStyleSheet("font-size: 13px; color: #F59E0B; font-weight: bold;")
        layout.addWidget(bal_lbl)

        layout.addWidget(QLabel("المبلغ المدفوع (د.ع): *"))
        self.spn_amount = QDoubleSpinBox()
        self.spn_amount.setMaximum(1000000000)
        self.spn_amount.setValue(supplier["Balance"])
        self.spn_amount.setStyleSheet("font-size: 16px; font-weight: bold;")
        layout.addWidget(self.spn_amount)

        layout.addWidget(QLabel("ملاحظات الدفعة:"))
        self.txt_notes = QLineEdit(f"سداد دفعة نقدية للمندوب {supplier['Name']}")
        layout.addWidget(self.txt_notes)

        btn_layout = QHBoxLayout()
        btn_confirm = QPushButton("✔ تأكيد السداد")
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
        amount = self.spn_amount.value()
        if amount <= 0:
            QMessageBox.warning(self, "تنبيه", "يرجى كتابة مبلغ صحيح للدفعة.")
            return

        self.db.add_supplier_payment(self.supplier["Id"], amount, self.txt_notes.text().strip())
        self.accept()

class SuppliersView(QWidget):
    def __init__(self, db: Database, parent=None):
        super().__init__(parent)
        self.db = db
        self.init_ui()

    def init_ui(self):
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(20, 20, 20, 20)
        main_layout.setSpacing(14)

        # Header Bar
        header = QHBoxLayout()
        title = QLabel("🤝 إدارة المناديب والشركات الموردة")
        title.setStyleSheet("font-size: 20px; font-weight: bold; color: #F8FAFC;")
        header.addWidget(title)
        header.addStretch()

        self.txt_search = QLineEdit()
        self.txt_search.setPlaceholderText("🔍 ابحث باسم المندوب أو الشركة...")
        self.txt_search.setFixedWidth(280)
        self.txt_search.textChanged.connect(self.search)
        header.addWidget(self.txt_search)

        btn_add = QPushButton("➕ إضافة مندوب جديد")
        btn_add.setProperty("class", "success")
        btn_add.setFixedHeight(38)
        btn_add.clicked.connect(self.add_supplier)
        header.addWidget(btn_add)

        btn_refresh = QPushButton("🔄")
        btn_refresh.setFixedSize(38, 38)
        btn_refresh.clicked.connect(self.load_suppliers)
        header.addWidget(btn_refresh)
        main_layout.addLayout(header)

        # Suppliers Table
        self.table = QTableWidget()
        self.table.setColumnCount(6)
        self.table.setHorizontalHeaderLabels(["اسم المندوب", "الشركة", "رقم الهاتف", "رصيد الديون المستحق", "تسجيل دفعة", "سجل الوصولات"])
        self.table.horizontalHeader().setSectionResizeMode(0, QHeaderView.Stretch)
        self.table.horizontalHeader().setSectionResizeMode(1, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(4, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(5, QHeaderView.ResizeToContents)
        self.table.verticalHeader().setVisible(False)
        main_layout.addWidget(self.table)

        # Initial Load
        self.load_suppliers()

    def load_suppliers(self):
        self.suppliers = self.db.get_suppliers()
        self.display_suppliers(self.suppliers)

    def search(self, query: str):
        filtered = [s for s in self.suppliers if query.lower() in s["Name"].lower() or query in (s["Company"] or "") or query in (s["Phone"] or "")]
        self.display_suppliers(filtered)

    def display_suppliers(self, sups: list):
        self.table.setRowCount(len(sups))
        for row, s in enumerate(sups):
            self.table.setItem(row, 0, QTableWidgetItem(s["Name"]))
            self.table.setItem(row, 1, QTableWidgetItem(s["Company"] or "---"))
            self.table.setItem(row, 2, QTableWidgetItem(s["Phone"] or "---"))
            
            bal_item = QTableWidgetItem(format_currency(s["Balance"]))
            bal_item.setForeground(Qt.yellow if s["Balance"] > 0 else Qt.green)
            self.table.setItem(row, 3, bal_item)

            # Payment Button
            btn_pay = QPushButton("💳 سداد دفعة")
            btn_pay.setProperty("class", "success")
            btn_pay.clicked.connect(lambda _, sup=s: self.record_payment(sup))
            self.table.setCellWidget(row, 4, btn_pay)

            # Invoices count info
            invoices_info = QLabel(f"{s.get('InvoicesCount', 0)} وصولات")
            invoices_info.setAlignment(Qt.AlignCenter)
            self.table.setCellWidget(row, 5, invoices_info)

    def add_supplier(self):
        dlg = AddSupplierDialog(self.db, parent=self)
        if dlg.exec() == QDialog.Accepted:
            self.load_suppliers()
            QMessageBox.information(self, "نجاح", "✔ تم إضافة المندوب بنجاح!")

    def record_payment(self, sup: dict):
        dlg = SupplierPaymentDialog(self.db, sup, parent=self)
        if dlg.exec() == QDialog.Accepted:
            self.load_suppliers()
            QMessageBox.information(self, "تم السداد", "✔ تم تسجيل الدفعة وتحديث الرصيد بنجاح!")
