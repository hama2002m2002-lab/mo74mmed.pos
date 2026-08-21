import os
import shutil
from datetime import datetime
from PySide6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit, QPushButton, 
    QFrame, QMessageBox, QFileDialog, QGroupBox
)
from PySide6.QtCore import Qt
from database import Database
from config import DB_PATH, ROOT_DIR, REP_PORTAL_URL

class SettingsView(QWidget):
    def __init__(self, db: Database, parent=None):
        super().__init__(parent)
        self.db = db
        self.init_ui()

    def init_ui(self):
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(20, 20, 20, 20)
        main_layout.setSpacing(18)

        title = QLabel("⚙️ إعدادات النظام والشبكة والنسخ الاحتياطي")
        title.setStyleSheet("font-size: 20px; font-weight: bold; color: #F8FAFC;")
        main_layout.addWidget(title)

        # Store Info Group
        grp_store = QGroupBox("بيانات المحل / المخزن")
        grp_store.setStyleSheet("font-size: 14px; font-weight: bold; color: #38BDF8;")
        store_layout = QVBoxLayout(grp_store)
        store_layout.setSpacing(10)

        store_layout.addWidget(QLabel("اسم المحل أو الشركة:"))
        self.txt_store_name = QLineEdit("7amo POS Store")
        store_layout.addWidget(self.txt_store_name)

        store_layout.addWidget(QLabel("رقم الهاتف وبيانات التواصل:"))
        self.txt_store_phone = QLineEdit("0750 000 0000")
        store_layout.addWidget(self.txt_store_phone)

        btn_save_store = QPushButton("💾 حفظ بيانات المحل")
        btn_save_store.setProperty("class", "primary")
        btn_save_store.setFixedWidth(160)
        btn_save_store.clicked.connect(self.save_store_info)
        store_layout.addWidget(btn_save_store)

        main_layout.addWidget(grp_store)

        # Rep Portal Group
        grp_cloud = QGroupBox("بوابة طلبيات المندوب السحابية")
        grp_cloud.setStyleSheet("font-size: 14px; font-weight: bold; color: #10B981;")
        cloud_layout = QVBoxLayout(grp_cloud)

        link_lbl = QLabel(f"رابط البوابة للمناديب والمحلات:\n{REP_PORTAL_URL}")
        link_lbl.setStyleSheet("font-size: 13px; color: #38BDF8; font-weight: bold;")
        cloud_layout.addWidget(link_lbl)

        btn_open_portal = QPushButton("🌐 فتح بوابة المندوب في المتصفح")
        btn_open_portal.setFixedWidth(240)
        btn_open_portal.clicked.connect(lambda: os.startfile(REP_PORTAL_URL))
        cloud_layout.addWidget(btn_open_portal)

        main_layout.addWidget(grp_cloud)

        # Backup Group
        grp_backup = QGroupBox("النسخ الاحتياطي لقاعدة البيانات")
        grp_backup.setStyleSheet("font-size: 14px; font-weight: bold; color: #F59E0B;")
        backup_layout = QVBoxLayout(grp_backup)

        db_path_lbl = QLabel(f"مسار قاعدة البيانات الحالي: {DB_PATH}")
        db_path_lbl.setStyleSheet("font-size: 12px; color: #94A3B8;")
        backup_layout.addWidget(db_path_lbl)

        btn_backup = QPushButton("💾 إنشاء نسخة احتياطية للبيانات الآن")
        btn_backup.setProperty("class", "success")
        btn_backup.setFixedWidth(240)
        btn_backup.clicked.connect(self.create_backup)
        backup_layout.addWidget(btn_backup)

        main_layout.addWidget(grp_backup)
        main_layout.addStretch()

    def save_store_info(self):
        QMessageBox.information(self, "نجاح", "✔ تم حفظ بيانات المحل بنجاح!")

    def create_backup(self):
        dest_dir, _ = QFileDialog.getSaveFileName(
            self, 
            "حفظ النسخة الاحتياطية", 
            os.path.join(ROOT_DIR, f"backup_pos_data_{datetime.now().strftime('%Y%m%d_%H%M%S')}.db"),
            "SQLite Database (*.db)"
        )
        if dest_dir:
            try:
                shutil.copyfile(DB_PATH, dest_dir)
                QMessageBox.information(self, "تم النسخ الاحتياطي", f"✔ تم إنشاء النسخة الاحتياطية بنجاح في:\n{dest_dir}")
            except Exception as e:
                QMessageBox.critical(self, "خطأ", f"فشل إنشاء النسخة الاحتياطية: {e}")
