from PySide6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit, QPushButton, 
    QTableWidget, QTableWidgetItem, QHeaderView, QFrame, QMessageBox, 
    QDialog, QComboBox
)
from PySide6.QtCore import Qt
from database import Database

class UserDialog(QDialog):
    def __init__(self, db: Database, user: dict = None, parent=None):
        super().__init__(parent)
        self.db = db
        self.user = user
        self.setWindowTitle("إنشاء حساب كاشير جديد" if not user else f"تعديل حساب: {user['FullName']}")
        self.setFixedSize(420, 380)
        self.setLayoutDirection(Qt.RightToLeft)

        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 24, 24, 24)
        layout.setSpacing(12)

        title = QLabel("👤 بيانات حساب المستخدم")
        title.setStyleSheet("font-size: 16px; font-weight: bold; color: #38BDF8;")
        layout.addWidget(title)

        layout.addWidget(QLabel("الاسم الكامل: *"))
        self.txt_fullname = QLineEdit(user.get("FullName", "") if user else "")
        layout.addWidget(self.txt_fullname)

        layout.addWidget(QLabel("اسم المستخدم (Username): *"))
        self.txt_username = QLineEdit(user.get("Username", "") if user else "")
        layout.addWidget(self.txt_username)

        layout.addWidget(QLabel("الرمز السري / كلمة المرور (PIN):"))
        self.txt_password = QLineEdit(user.get("PasswordHash", "1234") if user else "1234")
        layout.addWidget(self.txt_password)

        layout.addWidget(QLabel("الصلاحية / الدور:"))
        self.cmb_role = QComboBox()
        self.cmb_role.addItems(["Cashier", "Admin", "Manager"])
        if user and user.get("Role"):
            self.cmb_role.setCurrentText(user["Role"])
        layout.addWidget(self.cmb_role)

        btn_layout = QHBoxLayout()
        btn_save = QPushButton("💾 حفظ الحساب")
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
        full_name = self.txt_fullname.text().strip()
        username = self.txt_username.text().strip()
        if not full_name or not username:
            QMessageBox.warning(self, "تنبيه", "يرجى كتابة الاسم الكامل واسم المستخدم.")
            return

        user_data = {
            "Id": self.user["Id"] if self.user else None,
            "FullName": full_name,
            "Username": username,
            "PasswordHash": self.txt_password.text().strip() or "1234",
            "Role": self.cmb_role.currentText(),
            "IsActive": 1
        }
        self.db.save_user(user_data)
        self.accept()

class UsersView(QWidget):
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
        title = QLabel("👤 إدارة مستخدمي الكاشير والصلاحيات")
        title.setStyleSheet("font-size: 20px; font-weight: bold; color: #F8FAFC;")
        header.addWidget(title)
        header.addStretch()

        btn_add = QPushButton("➕ إنشاء حساب كاشير جديد")
        btn_add.setProperty("class", "success")
        btn_add.setFixedHeight(38)
        btn_add.clicked.connect(self.add_user)
        header.addWidget(btn_add)

        btn_refresh = QPushButton("🔄")
        btn_refresh.setFixedSize(38, 38)
        btn_refresh.clicked.connect(self.load_users)
        header.addWidget(btn_refresh)
        main_layout.addLayout(header)

        # Users Table
        self.table = QTableWidget()
        self.table.setColumnCount(5)
        self.table.setHorizontalHeaderLabels(["الاسم الكامل", "اسم الدخول", "الصلاحية", "الحالة", "تعديل"])
        self.table.horizontalHeader().setSectionResizeMode(0, QHeaderView.Stretch)
        self.table.horizontalHeader().setSectionResizeMode(1, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(4, QHeaderView.ResizeToContents)
        self.table.verticalHeader().setVisible(False)
        main_layout.addWidget(self.table)

        self.load_users()

    def load_users(self):
        users = self.db.get_users()
        self.table.setRowCount(len(users))
        for row, u in enumerate(users):
            self.table.setItem(row, 0, QTableWidgetItem(u["FullName"]))
            self.table.setItem(row, 1, QTableWidgetItem(f"@{u['Username']}"))
            self.table.setItem(row, 2, QTableWidgetItem(u.get("Role", "Cashier")))
            
            status_item = QTableWidgetItem("نشط ✔" if u.get("IsActive", 1) else "معطل")
            status_item.setForeground(Qt.green if u.get("IsActive", 1) else Qt.red)
            self.table.setItem(row, 3, status_item)

            btn_edit = QPushButton("✏️ تعديل")
            btn_edit.clicked.connect(lambda _, user_item=u: self.edit_user(user_item))
            self.table.setCellWidget(row, 4, btn_edit)

    def add_user(self):
        dlg = UserDialog(self.db, parent=self)
        if dlg.exec() == QDialog.Accepted:
            self.load_users()
            QMessageBox.information(self, "نجاح", "✔ تم إنشاء الحساب بنجاح!")

    def edit_user(self, user: dict):
        dlg = UserDialog(self.db, user=user, parent=self)
        if dlg.exec() == QDialog.Accepted:
            self.load_users()
            QMessageBox.information(self, "نجاح", "✔ تم حفظ التعديلات بنجاح!")
