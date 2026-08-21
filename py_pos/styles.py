DARK_STYLE = """
QMainWindow, QWidget {
    background-color: #0B0F19;
    color: #F8FAFC;
    font-family: 'Segoe UI', 'Tahoma', 'Arial';
    font-size: 13px;
}

/* Sidebar & Navigation */
#sidebar {
    background-color: #0F172A;
    border-left: 1px solid #1E293B;
    min-width: 240px;
    max-width: 240px;
}

#sidebar QPushButton {
    background-color: transparent;
    color: #94A3B8;
    text-align: right;
    padding: 12px 16px;
    border: none;
    border-radius: 8px;
    font-size: 13px;
    font-weight: bold;
    margin: 2px 8px;
}

#sidebar QPushButton:hover {
    background-color: #1E293B;
    color: #38BDF8;
}

#sidebar QPushButton[active="true"] {
    background-color: #0284C7;
    color: #FFFFFF;
}

/* Top Header Bar */
#topbar {
    background-color: #0F172A;
    border-bottom: 1px solid #1E293B;
    min-height: 54px;
    max-height: 54px;
    padding: 0 16px;
}

/* Modern Cards */
.Card {
    background-color: #111827;
    border: 1px solid #1F2937;
    border-radius: 12px;
    padding: 16px;
}

/* Inputs & Controls */
QLineEdit, QTextEdit, QPlainTextEdit, QSpinBox, QDoubleSpinBox {
    background-color: #0A0F1D;
    color: #F8FAFC;
    border: 1px solid #334155;
    border-radius: 8px;
    padding: 8px 12px;
    font-size: 13px;
    selection-background-color: #0284C7;
}

QLineEdit:focus, QTextEdit:focus, QComboBox:focus {
    border: 1px solid #38BDF8;
}

QComboBox {
    background-color: #0A0F1D;
    color: #F8FAFC;
    border: 1px solid #334155;
    border-radius: 8px;
    padding: 6px 12px;
    font-size: 13px;
    min-height: 24px;
}

QComboBox QAbstractItemView {
    background-color: #111827;
    color: #F8FAFC;
    selection-background-color: #0284C7;
    border: 1px solid #334155;
}

/* Modern Buttons */
QPushButton {
    background-color: #1E293B;
    color: #F8FAFC;
    border: 1px solid #334155;
    border-radius: 8px;
    padding: 8px 16px;
    font-weight: bold;
    cursor: pointer;
}

QPushButton:hover {
    background-color: #334155;
    border-color: #475569;
}

QPushButton:pressed {
    background-color: #0F172A;
}

QPushButton.primary {
    background-color: #0284C7;
    color: #FFFFFF;
    border: none;
}

QPushButton.primary:hover {
    background-color: #0369A1;
}

QPushButton.success {
    background-color: #059669;
    color: #FFFFFF;
    border: none;
}

QPushButton.success:hover {
    background-color: #047857;
}

QPushButton.danger {
    background-color: #DC2626;
    color: #FFFFFF;
    border: none;
}

QPushButton.danger:hover {
    background-color: #B91C1C;
}

QPushButton.warning {
    background-color: #D97706;
    color: #FFFFFF;
    border: none;
}

/* Tables / QTableWidget */
QTableWidget {
    background-color: #111827;
    color: #F8FAFC;
    gridline-color: #1E293B;
    border: 1px solid #1F2937;
    border-radius: 10px;
    selection-background-color: #1E3A8A;
    selection-color: #FFFFFF;
    font-size: 13px;
}

QTableWidget::item {
    padding: 8px;
    border-bottom: 1px solid #1E293B;
}

QHeaderView::section {
    background-color: #0F172A;
    color: #94A3B8;
    font-weight: bold;
    font-size: 12px;
    padding: 10px;
    border: none;
    border-bottom: 2px solid #1E293B;
}

/* ScrollBar */
QScrollBar:vertical {
    border: none;
    background-color: #0B0F19;
    width: 8px;
    border-radius: 4px;
}

QScrollBar::handle:vertical {
    background-color: #334155;
    min-height: 20px;
    border-radius: 4px;
}

QScrollBar::handle:vertical:hover {
    background-color: #475569;
}

QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {
    height: 0px;
}

/* Dialogs */
QDialog {
    background-color: #0F172A;
    border: 1px solid #334155;
    border-radius: 14px;
}
"""
