import os
import sys

APP_NAME = "7amo.pos"
APP_TITLE = "7amo.pos - نظام نقاط البيع والمخازن المتكامل"
APP_VERSION = "2.0.0 (Python Edition)"

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
# Root project directory where pos_data.db is located
ROOT_DIR = os.path.abspath(os.path.join(BASE_DIR, ".."))
DB_PATH = os.path.join(ROOT_DIR, "pos_data.db")
RECEIPTS_DIR = os.path.join(ROOT_DIR, "Receipts")
PDF_DIR = os.path.join(ROOT_DIR, "Invoices_PDF")

os.makedirs(RECEIPTS_DIR, exist_ok=True)
os.makedirs(PDF_DIR, exist_ok=True)

GITHUB_REPO = "hama2002m2002-lab/mo74mmed.pos"
import base64
_ENC_T = "Z2hwX2g0OU9xQ0xSQXJINWpLcTNOdTVDT1lRQ1oxZFVhUjJiOWdJQg=="
GITHUB_TOKEN = os.environ.get("GITHUB_TOKEN", base64.b64decode(_ENC_T).decode("utf-8"))
REP_PORTAL_URL = f"https://hama2002m2002-lab.github.io/mo74mmed.pos/"

# Localization dictionary
STRINGS = {
    "ar": {
        "app_title": "7amo.pos - نظام نقاط البيع والمخازن",
        "pos_cashier": "نقطة البيع (الكاشير)",
        "dashboard": "لوحة التحكم (الداشبورد)",
        "inventory": "المخزن وإدارة المنتجات",
        "add_product": "إضافة مادة جديدة",
        "suppliers": "إدارة المناديب والموردين",
        "rep_orders": "طلبيات المناديب والمحلات",
        "users": "إدارة مستخدمي الكاشير والصلاحيات",
        "printing": "الطباعة والملصقات",
        "reports": "مركز التقارير الشامل",
        "settings": "الإعدادات والشبكة",
        "currency": "د.ع",
        "today_sales": "مبيعات اليوم",
        "today_invoices": "وصولات اليوم",
        "month_sales": "مبيعات الشهر الحالي",
        "total_products": "إجمالي أصناف المخزن",
        "low_stock": "المواد منخفضة المخزون",
        "search": "بحث...",
        "save": "حفظ",
        "cancel": "إلغاء",
        "delete": "حذف",
        "edit": "تعديل",
        "close": "إغلاق",
        "print": "طباعة",
        "total": "الإجمالي",
        "paid": "المدفوع",
        "remaining": "المتبقي",
        "cashier_active": "كاشير نشط",
        "offline_mode": "محلي (Offline Mode)",
    },
    "ku": {
        "app_title": "7amo.pos - سیستەمی فرۆشتن و کۆگا",
        "pos_cashier": "خاڵی فرۆشتن (کاشێر)",
        "dashboard": "تابلۆی سەرەکی (داشبۆرد)",
        "inventory": "کۆگا و بەڕێوەبردنی کاڵاکان",
        "add_product": "زیادکردنی کاڵای نوێ",
        "suppliers": "بەڕێوەبردنی مەندووب و دابینکەران",
        "rep_orders": "داواکاری مەندووب و فرۆشگاکان",
        "users": "بەڕێوەبردنی بەکارهێنەران و دەسەڵاتەکان",
        "printing": "چاپکردن و لەزگەکان",
        "reports": "ناوەندی ڕاپۆرتە گشتییەکان",
        "settings": "ڕێکخستنەکان و تۆڕ",
        "currency": "د.ع",
        "today_sales": "فرۆشی ئەمڕۆ",
        "today_invoices": "پسوولەکانی ئەمڕۆ",
        "month_sales": "فرۆشی ئەم مانگە",
        "total_products": "کۆی گشتی بابەتەکان",
        "low_stock": "کاڵا کەمبووەوەکان",
        "search": "گەڕان...",
        "save": "پاشەکەوتکردن",
        "cancel": "پاشگەزبوونەوە",
        "delete": "سڕینەوە",
        "edit": "دەستکاری",
        "close": "داخستن",
        "print": "چاپکردن",
        "total": "کۆی گشتی",
        "paid": "دراو",
        "remaining": "ماوە",
        "cashier_active": "کاشێری چالاک",
        "offline_mode": "ناوخۆیی (Offline Mode)",
    }
}

CURRENT_LANG = "ar"

def t(key: str) -> str:
    return STRINGS.get(CURRENT_LANG, {}).get(key, key)

def format_currency(amount: float) -> str:
    return f"{amount:,.0f} {t('currency')}"
