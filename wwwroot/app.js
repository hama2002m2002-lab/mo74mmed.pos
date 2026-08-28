// ========================================================
// 7amo Market - Modern Supermarket & POS Core Logic
// ========================================================

const state = {
  activeTab: 'dashboard',
  theme: localStorage.getItem('pos_theme') || 'light',
  language: 'ar',
  invoiceTabs: [
    { id: 1, title: 'فاتورة 1', items: [], discount: 0, paid: 0, paymentMethod: 'Cash' }
  ],
  selectedInvoiceTabId: 1,
  products: [],
  suppliers: [],
  attendanceChart: null,
  performanceChart: null,
  activityChart: null
};

// C# Native Bridge Interface
async function callBackend(action, payload = {}) {
  return new Promise((resolve) => {
    if (window.chrome && window.chrome.webview) {
      const callbackId = 'cb_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
      
      const handler = (event) => {
        try {
          const data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
          if (data && data._callbackId === callbackId) {
            window.chrome.webview.removeEventListener('message', handler);
            resolve(data.result);
          }
        } catch (e) {
          resolve({ success: false, error: e.message });
        }
      };

      window.chrome.webview.addEventListener('message', handler);
      window.chrome.webview.postMessage({ action, payload: JSON.stringify(payload), _callbackId: callbackId });
    } else {
      console.log(`[C# Bridge] ${action}`, payload);
      resolve({ success: true, message: "Browser Mode" });
    }
  });
}

// ========================================================
// INITIALIZATION
// ========================================================
document.addEventListener('DOMContentLoaded', async () => {
  applyTheme(state.theme);
  lucide.createIcons();
  setupGlobalKeyboardShortcuts();
  
  await loadProducts();
  await loadSuppliersList();
  await loadCategoriesList();
  renderInvoiceTabs();
  renderCashierCart();
  await loadDashboard();
  await loadRepOrders(false); // Initial load without sound alert

  // Setup C# Push Event Listener
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (event) => {
      try {
        const data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
        if (data && data._event === 'new_order_received') {
          loadRepOrders(true);
        }
      } catch (e) {}
    });
  }

  // Periodic automatic sync for incoming rep orders every 6 seconds
  setInterval(() => {
    loadRepOrders(true);
  }, 6000);

  recalcAddProduct();
});

function setupGlobalKeyboardShortcuts() {
  window.addEventListener('keydown', (e) => {
    if (e.key === 'F12') {
      e.preventDefault();
      submitCashierSale();
    } else if (e.key === 'F1') {
      e.preventDefault();
      switchTab('dashboard');
    } else if (e.key === 'F2') {
      e.preventDefault();
      switchTab('cashier');
    } else if (e.key === 'F3') {
      e.preventDefault();
      switchTab('addProduct');
    }
  });
}

// ========================================================
// I18N LOCALIZATION DICTIONARY (ARABIC & KURDISH)
// ========================================================
// ========================================================
// I18N LOCALIZATION DICTIONARY (ARABIC, KURDISH & ENGLISH)
// ========================================================
const i18n = {
  ar: {
    // POS & Cashier Top & Header
    pos_user_title: "الكاشير: المدير العام (Admin)",
    pos_subtitle: "7amo.pos • نظام السوبرماركت والمبيعات",
    pos_home_btn: "الرئيسية",
    pos_tabs_title: "نوافذ البيع:",
    pos_new_tab_btn: "+ نافذة جديدة F1",
    pos_btn_invoices: "الوصولات المباعة والمرجوعة",
    pos_btn_warehouse: "المخزن F4",
    pos_btn_return: "إرجاع مادة",
    pos_empty_cart: "السلة فارغة، امسح الباركود أو اختر مادة من المخزن",
    pos_search_ph: "[F3] امسح الباركود هنا (نشط)...",

    // Cart Table Headers
    cart_th_num: "#",
    cart_th_name: "اسم المادة",
    cart_th_cost: "سعر الشراء",
    cart_th_price: "سعر البيع",
    cart_th_type: "نوعية البيع",
    cart_th_qty: "العدد / الكمية",
    cart_th_total: "المجموع",
    cart_th_del: "حذف",
    sale_type_retail: "مفرد",
    sale_type_wholesale: "جملة",
    sale_type_carton: "كرتون",

    // Right Summary & Payment Panel
    pos_total_due_title: "المبلغ الكلي المطلوب للدفع",
    pos_items_in_cart: "مواد في السلة",
    pos_payment_details_title: "طريقة وتفاصيل الدفع",
    pos_payment_method_lbl: "شێوازی پارەدان:",
    pos_pm_cash: "نقد (كاش)",
    pos_pm_debt: "آجل / في الحساب",
    pos_pm_card: "بطاقة / ماستر",
    pos_pm_nfc: "NFC / دفع ذكي",
    pos_paid_lbl: "المبلغ المستلم:",
    pos_subtotal_lbl: "المجموع الأولي:",
    pos_discount_lbl: "خصم خاص:",
    pos_tax_lbl: "الضريبة (%0):",
    pos_change_lbl: "المتبقي للزبون (الباقي):",
    pos_btn_sell_no_receipt: "✔ بيع (بدون وصل)",
    pos_btn_sell_print: "🖨️ بيع وطباعة وصل",
    pos_btn_smart_print: "⚡ طباعة ذكية",
    pos_btn_clear_cart: "🔄 تفريغ السلة [F8]",

    // Modals
    qpm_title: "اختيار سريع من المخزن (قائمة المواد السريعة)",
    inv_modal_title: "الوصولات المباعة والمرتجعة (فواتير المبيعات)",
    inv_filter_all: "جميع الفواتير (الكل)",
    inv_filter_completed: "المباعة (Completed)",
    inv_filter_returned: "المرتجعة (Returned)",
    refresh_btn: "تحديث",
    close_btn: "إغلاق",

    // Add Product Form
    ap_back: "العودة للرئيسية",
    ap_title: "إضافة وتعديل مادة جديدة بالمخزن",
    ap_subtitle: "انقر مرتين على حقل الباركود للتوليد التلقائي (200245) بدون تكرار",
    ap_clear: "مسح الحقول",
    ap_save: "حفظ المادة",
    ap_sec1_title: "1. بيانات المادة والباركود والتعبئة",
    ap_sec1_tag: "نقر مزدوج للباركود ⚡",
    ap_lbl_barcode: "1. حقل الباركود (انقر مرتين للتوليد 200245) *",
    ap_btn_gen: "توليد",
    ap_lbl_name: "2. اسم المادة *",
    ap_name_ph: "اسم المادة",
    ap_lbl_cat: "3. تصنيف المادة",
    ap_lbl_sup: "4. اسم المندوب / الشركة الموردة",
    ap_lbl_cartons: "عدد الكراتين",
    ap_lbl_pieces: "المواد بالكرتون",
    ap_lbl_total: "مجموع كل المواد",
    ap_lbl_alert: "تنبيه النواقص عند وصول الرصيد إلى (قطع):",
    ap_sec2_title: "2. التكلفة وأسعار البيع والأرباح المحسوبة",
    ap_lbl_carton_purchase: "سعر شراء الكرتون (د.ع)",
    ap_lbl_piece_cost_calc: "تكلفة القطعة من الكرتون (للقراءة فقط)",
    ap_lbl_cost: "تكلفة القطعة المعتمدة (د.ع) *",
    ap_lbl_retail_price: "سعر بيع المفرد للقطعة (د.ع) *",
    ap_r_profit: "ربح القطعة:",
    ap_r_total: "بيع الكرتون بالمفرد:",
    ap_r_c_profit: "ربح الكرتون بالمفرد:",
    ap_lbl_wholesale_price: "سعر بيع الجملة للقطعة (د.ع)",
    ap_w_profit: "ربح قطعة الجملة:",
    ap_w_c_profit: "ربح الكرتون بالجملة:",
    ap_lbl_carton_sell: "سعر بيع الكرتون كاملاً (د.ع)",
    ap_c_profit: "ربح بيع الكرتون كاملاً:",

    // Add Product Modes (العدد المفرد / الكراتين / الوزن والكيلو)
    ap_mode_simple: "إضافة مواد بالعدد (قطع مباشرة)",
    ap_mode_piece: "إضافة مواد بالكرتون (قطع / كراتين)",
    ap_mode_weight: "إضافة مواد بالوزن (فردة / كيلو)",
    ap_lbl_simple_qty: "الكمية المتوفرة (عدد القطع) *",
    ap_lbl_simple_alert: "تنبيه النواقص عند (قطع):",
    ap_lbl_simple_cost: "سعر شراء القطعة الواحدة (التكلفة د.ع) *",
    ap_lbl_simple_price: "سعر بيع المفرد للقطعة (د.ع) *",
    ap_simple_profit: "ربح القطعة:",
    ap_simple_total_val: "إجمالي قيمة البيع:",
    ap_simple_expected_profit: "إجمالي الأرباح:",

    // Add Product Weight Mode (كغم / فردة)
    ap_lbl_fardah_count: "عدد الفردات (أكياس/شوال)",
    ap_lbl_kg_per_fardah: "الوزن داخل الفردة (كغم)",
    ap_lbl_total_kg: "مجموع كل الوزن (كغم)",
    ap_lbl_kg_alert: "تنبيه النواقص عند وصول الرصيد إلى (كغم):",
    ap_lbl_fardah_purchase: "سعر شراء الفردة (د.ع)",
    ap_lbl_kg_cost_calc: "تكلفة الكيلوغرام من الفردة (للقراءة فقط)",
    ap_lbl_kg_retail_price: "سعر بيع المفرد للكيلو (د.ع) *",
    ap_lbl_kg_wholesale_price: "سعر بيع الجملة للكيلو (د.ع)",
    ap_lbl_fardah_sell: "سعر بيع الفردة كاملة (د.ع)",
    ap_kg_profit: "ربح الكيلو:",
    ap_fardah_total: "بيع الفردة بالمفرد:",
    ap_fardah_r_profit: "ربح الفردة بالمفرد:",
    ap_fardah_w_profit: "ربح الفردة بالجملة:",
    ap_fardah_direct_profit: "ربح بيع الفردة كاملة:",

    // Inventory & Warehouse Screen
    inv_back: "العودة للرئيسية",
    inv_title: "المخزن ورصيد المواد (تفاصيل شاملة)",
    inv_subtitle: "عرض كامل تفاصيل الكراتين، التكلفة، أسعار البيع، والأرباح لكل مادة",
    inv_refresh_btn: "تحديث القائمة",
    inv_add_new_btn: "إضافة مادة جديدة",
    inv_kpi_total_items: "إجمالي عدد المواد",
    inv_kpi_cost_val: "قيمة المخزن بالتكلفة",
    inv_kpi_sell_val: "القيمة البيعية الإجمالية",
    inv_kpi_expected_profit: "الأرباح المتوقعة بالمخزن",
    inv_search_ph: "بحث بالاسم أو الباركود...",
    inv_all_cats: "جميع التصنيفات",
    inv_limit_1000: "عرض: أول 1,000 مادة (الأسرع)",
    inv_limit_500: "عرض: أول 500 مادة",
    inv_limit_2000: "عرض: أول 2,000 مادة",
    inv_limit_5000: "عرض: أول 5,000 مادة",
    inv_limit_all: "عرض: كافة المواد دفعة واحدة (الكل)",
    inv_low_stock_only: "النواقص فقط",
    inv_th_num: "#",
    inv_th_item_barcode: "المادة / الباركود",
    inv_th_cat_supplier: "التصنيف والمورد",
    inv_th_packaging: "التعبئة والكراتين",
    inv_th_stock_qty: "رصيد القطع",
    inv_th_cost: "تكلفة القطعة / الكرتون",
    inv_th_sell_prices: "أسعار البيع (مفرد/جملة/كرتون)",
    inv_th_profits: "الأرباح المحسوبة",
    inv_th_status: "الحالة",
    inv_th_actions: "الإجراءات",

    // Stock Audit Screen
    audit_back: "العودة للرئيسية",
    audit_title: "جرد ومطابقة أرصدة الرفوف والمخزن (جرد ذكي)",
    audit_subtitle: "فحص الكميات الفعلية ومطابقتها مع رصيد النظام فورياً باستخدام الباركود",
    audit_refresh_btn: "تحديث القائمة",
    audit_save_all: "حفظ واعتماد كل الفروقات",
    audit_kpi_total: "إجمالي المواد",
    audit_kpi_matched: "المواد المطابقة (بدون فارق)",
    audit_kpi_shortage: "مواد بها عجز / نقص",
    audit_kpi_surplus: "مواد بها زيادة",
    audit_scan_ph: "⚡ امسح الباركود للجرد المباشر...",
    audit_auto_inc: "زيادة تلقائية (+1 عند كل مسح)",
    audit_filter_all: "كافة المواد",
    audit_filter_diff: "المواد التي بها فروقات فقط",
    audit_filter_shortage: "العجز والنقص فقط",
    audit_filter_surplus: "الزيادة فقط",
    audit_filter_matched: "المطابقة فقط",
    audit_all_cats: "جميع التصنيفات",
    audit_th_num: "#",
    audit_th_name: "المادة / الباركود",
    audit_th_cat: "التصنيف",
    audit_th_sys_stock: "الرصيد بالنظام",
    audit_th_actual_stock: "الكمية الفعلية على الرف (جرد)",
    audit_th_diff: "فارق الجرد",
    audit_th_diff_val: "قيمة الفارق (د.ع)",
    audit_th_status: "الحالة",
    audit_th_action: "تحديث"
  },
  ku: {
    // POS & Cashier Top & Header
    pos_user_title: "کاشێر: بەڕێوەبەری گشتی (Admin)",
    pos_subtitle: "7amo.pos • سیستەمی فرۆشتن",
    pos_home_btn: "سەرەکی",
    pos_tabs_title: "پەنجەرەکانی فرۆشتن:",
    pos_new_tab_btn: "+ پەنجەرەی نوێ F1",
    pos_btn_invoices: "پسوولە فرۆشراو و گەڕاوەکان",
    pos_btn_warehouse: "کۆگا F4",
    pos_btn_return: "گەڕاندنەوەی کاڵا",
    pos_empty_cart: "سەبەتە بەتاڵە، بارکۆد لێبدە یان کاڵا هەڵبژێرە",
    pos_search_ph: "[F3] لێرە بارکۆد لێبدە (چالاک)...",

    // Cart Table Headers
    cart_th_num: "#",
    cart_th_name: "ناوی کاڵا",
    cart_th_cost: "نرخی کڕین",
    cart_th_price: "نرخی فرۆشتن",
    cart_th_type: "جۆری فرۆشتن",
    cart_th_qty: "بڕ / دانە",
    cart_th_total: "کۆی گشتی",
    cart_th_del: "سڕینەوە",
    sale_type_retail: "تاک (مفرد)",
    sale_type_wholesale: "کۆ (جملة)",
    sale_type_carton: "کارتۆن",

    // Right Summary & Payment Panel
    pos_total_due_title: "کۆی گشتی ماوە بۆ دان",
    pos_items_in_cart: "کاڵا لە سەبەتەدا",
    pos_payment_details_title: "شێواز و وردەکاری پارەدان",
    pos_payment_method_lbl: "شێوازی پارەدان:",
    pos_pm_cash: "نەختینە (کاش)",
    pos_pm_debt: "قەرز / لەژمێر",
    pos_pm_card: "کارت",
    pos_pm_nfc: "NFC / بێ بارکەوتن",
    pos_paid_lbl: "بڕی وەرگیراو:",
    pos_subtotal_lbl: "کۆی سەرەتایی:",
    pos_discount_lbl: "داشکاندنی تایبەت:",
    pos_tax_lbl: "باج (%0):",
    pos_change_lbl: "ماوەی گەڕاوە (الباقي):",
    pos_btn_sell_no_receipt: "✔ فرۆشتن (بێ پسوولە)",
    pos_btn_sell_print: "🖨️ فرۆشتن و چاپ",
    pos_btn_smart_print: "⚡ چاپی سمارت",
    pos_btn_clear_cart: "🔄 بەتاڵکردنی سەبەتە [F8]",

    // Modals
    qpm_title: "هەڵبژاردنی خێرا لە کۆگا (قائمة المواد السريعة)",
    inv_modal_title: "پسوولە فرۆشراو و گەڕاوەکان (فواتير المبيعات)",
    inv_filter_all: "هەموو پسوولەکان (الكل)",
    inv_filter_completed: "فرۆشراوەکان (Completed)",
    inv_filter_returned: "گەڕاوەکان (Returned)",
    refresh_btn: "نوێکردنەوە",
    close_btn: "داخستن",

    // Add Product Form
    ap_back: "گەڕانەوە بۆ سەرەکی",
    ap_title: "زیادکردن و دەستکاریکردنی کاڵا لە کۆگا",
    ap_subtitle: "دووجار کلیک لەسەر خانەی بارکۆد بکە بۆ دروستکردنی بارکۆد (200245) بەبێ دووبارەبوونەوە",
    ap_clear: "سڕینەوەی خانەکان",
    ap_save: "پاشەکەوتکردنی کاڵا",
    ap_sec1_title: "١. زانیاری کاڵا، بارکۆد و پاکێج",
    ap_sec1_tag: "دووجار کلیک بۆ بارکۆد ⚡",
    ap_lbl_barcode: "١. خانەی بارکۆد (دووجار کلیک بکە بۆ ٢٠٠٢٤٥) *",
    ap_btn_gen: "دروستکردن",
    ap_lbl_name: "٢. ناوی کاڵا *",
    ap_name_ph: "ناوی کاڵا",
    ap_lbl_cat: "٣. پۆلێنی کاڵا (جۆر)",
    ap_lbl_sup: "٤. ناوی مەندوب / کۆمپانیا",
    ap_lbl_cartons: "ژمارەی کارتۆن",
    ap_lbl_pieces: "دانە لە کارتۆندا",
    ap_lbl_total: "کۆی گشتی هەموو کاڵاکان",
    ap_lbl_alert: "ئاگادارکردنەوە لە کەمی کاڵا لە (دانە):",
    ap_sec2_title: "٢. تێچوون، نرخەکانی فرۆشتن و قازانجەکان",
    ap_lbl_carton_purchase: "نرخی کڕینی کارتۆن (د.ع)",
    ap_lbl_piece_cost_calc: "تێچووی دانە لە کارتۆندا (تەنها خوێندنەوە)",
    ap_lbl_cost: "تێچووی پەسەندکراوی دانە (د.ع) *",
    ap_lbl_retail_price: "نرخی فرۆشتنی تاک بۆ دانە (د.ع) *",
    ap_r_profit: "قازانجی دانە:",
    ap_r_total: "فرۆشتنی کارتۆن بە تاک:",
    ap_r_c_profit: "قازانجی کارتۆن بە تاک:",
    ap_lbl_wholesale_price: "نرخی فرۆشتنی کۆ بۆ دانە (د.ع)",
    ap_w_profit: "قازانجی دانە بە کۆ:",
    ap_w_c_profit: "قازانجی کارتۆن بە کۆ:",
    ap_lbl_carton_sell: "نرخی فرۆشتنی تەواوی کارتۆن (د.ع)",
    ap_c_profit: "قازانجی فرۆشتنی کارتۆن:",

    // Add Product Modes (العدد المفرد / الكراتين / الوزن والكيلو)
    ap_mode_simple: "زیادکردنی کاڵا بە دانەی تاک (دانە)",
    ap_mode_piece: "زیادکردنی کاڵا بە کارتۆن (دانە / کارتۆن)",
    ap_mode_weight: "زیادکردنی کاڵا بە کێش (فەردە / کیلۆ)",
    ap_lbl_simple_qty: "بڕی بەردەست (ژمارەی دانەکان) *",
    ap_lbl_simple_alert: "ئاگادارکردنەوە لە کەمی کاڵا لە (دانە):",
    ap_lbl_simple_cost: "نرخی کڕینی دانە (تێچوون د.ع) *",
    ap_lbl_simple_price: "نرخی فرۆشتنی تاک بۆ دانە (د.ع) *",
    ap_simple_profit: "قازانجی دانە:",
    ap_simple_total_val: "کۆی گشتی بەهای فرۆشتن:",
    ap_simple_expected_profit: "کۆی گشتی قازانج:",

    // Add Product Weight Mode (كغم / فردة)
    ap_lbl_fardah_count: "ژمارەی فەردەکان (کیسە/شەواڵ)",
    ap_lbl_kg_per_fardah: "کێش لەناو فەردەدا (کگم)",
    ap_lbl_total_kg: "کۆی گشتی کێش (کگم)",
    ap_lbl_kg_alert: "ئاگادارکردنەوە لە کەمی کێش لە (کگم):",
    ap_lbl_fardah_purchase: "نرخی کڕینی فەردە (د.ع)",
    ap_lbl_kg_cost_calc: "تێچووی کیلۆگرام لە فەردەدا (تەنها خوێندنەوە)",
    ap_lbl_kg_retail_price: "نرخی فرۆشتنی تاک بۆ کیلۆ (د.ع) *",
    ap_lbl_kg_wholesale_price: "نرخی فرۆشتنی کۆ بۆ کیلۆ (د.ع)",
    ap_lbl_fardah_sell: "نرخی فرۆشتنی تەواوی فەردە (د.ع)",
    ap_kg_profit: "قازانجی کیلۆ:",
    ap_fardah_total: "فرۆشتنی فەردە بە تاک:",
    ap_fardah_r_profit: "قازانجی فەردە بە تاک:",
    ap_fardah_w_profit: "قازانجی فەردە بە کۆ:",
    ap_fardah_direct_profit: "قازانجی فرۆشتنی فەردە:",

    // Inventory & Warehouse Screen
    inv_back: "گەڕانەوە بۆ سەرەکی",
    inv_title: "کۆگا و باڵانسی کاڵاکان (زانیاری تەواو)",
    inv_subtitle: "پیشاندانی تەواوی کارتۆن، تێچوون، نرخەکان و قازانجەکان",
    inv_refresh_btn: "نوێکردنەوەی لیست",
    inv_add_new_btn: "زیادکردنی کاڵای نوێ",
    inv_kpi_total_items: "کۆی گشتی کاڵاکان",
    inv_kpi_cost_val: "نرخی کۆگا بە تێچوون",
    inv_kpi_sell_val: "کۆی بەهای فرۆشتن",
    inv_kpi_expected_profit: "قازانجی چاوەڕوانکراو",
    inv_search_ph: "گەڕان بە ناو یان بارکۆد...",
    inv_all_cats: "هەموو جۆرەکان",
    inv_limit_1000: "پیشاندان: یەکەم ١،٠٠٠ کاڵا",
    inv_limit_500: "پیشاندان: یەکەم ٥٠٠ کاڵا",
    inv_limit_2000: "پیشاندان: یەکەم ٢،٠٠٠ کاڵا",
    inv_limit_5000: "پیشاندان: یەکەم ٥،٠٠٠ کاڵا",
    inv_limit_all: "پیشاندان: هەموو کاڵاکان پێکەوە",
    inv_low_stock_only: "تەنها کەموکوڕییەکان",
    inv_th_num: "#",
    inv_th_item_barcode: "کاڵا / بارکۆد",
    inv_th_cat_supplier: "پۆلێن و مەندوب",
    inv_th_packaging: "پاکێج و کارتۆن",
    inv_th_stock_qty: "باڵانسی دانەکان",
    inv_th_cost: "تێچووی دانە / کارتۆن",
    inv_th_sell_prices: "نرخەکانی فرۆشتن (تاک/کۆ/کارتۆن)",
    inv_th_profits: "قازانجی هەژمارکراو",
    inv_th_status: "دۆخ",
    inv_th_actions: "کردارەکان",

    // Stock Audit Screen
    audit_back: "گەڕانەوە بۆ سەرەکی",
    audit_title: "پشکنین و هاوتای باڵانسی ڕەفەکانی کۆگا (جردی زیرەک)",
    audit_subtitle: "پشکنینی بڕی ڕاستەقینە و بەراوردکردن لەگەڵ سیستەم بە بارکۆد",
    audit_refresh_btn: "نوێکردنەوەی لیست",
    audit_save_all: "پاشەکەوتکردنی هەموو جیاوازییەکان",
    audit_kpi_total: "کۆی گشتی کاڵاکان",
    audit_kpi_matched: "کاڵا هاوتاکان (بێ جیاوازی)",
    audit_kpi_shortage: "کاڵای کەمی / کورتیناو",
    audit_kpi_surplus: "کاڵای زیادی",
    audit_scan_ph: "⚡ لێرە بارکۆد لێبدە بۆ پشکنینی ڕاستەوخۆ...",
    audit_auto_inc: "زیادکردنی خۆکار (+١ لەگەڵ هەر لێدانێک)",
    audit_filter_all: "هەموو کاڵاکان",
    audit_filter_diff: "تەنها کاڵا جیاوازەکان",
    audit_filter_shortage: "تەنها کەمییەکان",
    audit_filter_surplus: "تەنها زیادییەکان",
    audit_filter_matched: "تەنها هاوتاکان",
    audit_all_cats: "هەموو جۆرەکان",
    audit_th_num: "#",
    audit_th_name: "کاڵا / بارکۆد",
    audit_th_cat: "پۆلێن",
    audit_th_sys_stock: "باڵانس لە سیستەم",
    audit_th_actual_stock: "بڕی ڕاستەقینە لەسەر ڕەف",
    audit_th_diff: "جیاوازی پشکنین",
    audit_th_diff_val: "بەهای جیاوازی (د.ع)",
    audit_th_status: "دۆخ",
    audit_th_action: "نوێکردنەوە"
  },
  en: {
    // POS & Cashier Top & Header
    pos_user_title: "Cashier: General Admin",
    pos_subtitle: "7amo.pos • Supermarket POS System",
    pos_home_btn: "Home",
    pos_tabs_title: "Sales Windows:",
    pos_new_tab_btn: "+ New Tab F1",
    pos_btn_invoices: "Invoices (Sales & Returns)",
    pos_btn_warehouse: "Warehouse F4",
    pos_btn_return: "Return Item",
    pos_empty_cart: "Cart is empty. Scan a barcode or pick an item.",
    pos_search_ph: "[F3] Scan barcode here (Active)...",

    // Cart Table Headers
    cart_th_num: "#",
    cart_th_name: "Item Name",
    cart_th_cost: "Cost Price",
    cart_th_price: "Selling Price",
    cart_th_type: "Sale Type",
    cart_th_qty: "Qty",
    cart_th_total: "Total",
    cart_th_del: "Del",
    sale_type_retail: "Retail",
    sale_type_wholesale: "Wholesale",
    sale_type_carton: "Carton",

    // Right Summary & Payment Panel
    pos_total_due_title: "Total Amount Due",
    pos_items_in_cart: "Items in Cart",
    pos_payment_details_title: "Payment Method & Details",
    pos_payment_method_lbl: "Payment Method:",
    pos_pm_cash: "Cash",
    pos_pm_debt: "Debt / Account",
    pos_pm_card: "Card",
    pos_pm_nfc: "NFC Pay",
    pos_paid_lbl: "Paid Amount:",
    pos_subtotal_lbl: "Subtotal:",
    pos_discount_lbl: "Discount:",
    pos_tax_lbl: "Tax (%0):",
    pos_change_lbl: "Change Due:",
    pos_btn_sell_no_receipt: "✔ Sell (No Print)",
    pos_btn_sell_print: "🖨️ Sell & Print",
    pos_btn_smart_print: "⚡ Smart Print",
    pos_btn_clear_cart: "🔄 Clear Cart [F8]",

    // Modals
    qpm_title: "Quick Warehouse Selector (F4)",
    inv_modal_title: "Sold & Returned Invoices",
    inv_filter_all: "All Invoices",
    inv_filter_completed: "Completed",
    inv_filter_returned: "Returned",
    refresh_btn: "Refresh",
    close_btn: "Close",

    // Add Product Form
    ap_back: "Back to Main",
    ap_title: "Add & Edit Product in Inventory",
    ap_subtitle: "Double click barcode input to auto generate unique code (200245)",
    ap_clear: "Clear Fields",
    ap_save: "Save Product",
    ap_sec1_title: "1. Product Info & Packaging",
    ap_sec1_tag: "Double click barcode ⚡",
    ap_lbl_barcode: "1. Barcode *",
    ap_btn_gen: "Generate",
    ap_lbl_name: "2. Item Name *",
    ap_name_ph: "Item Name",
    ap_lbl_cat: "3. Category",
    ap_lbl_sup: "4. Supplier / Company",
    ap_lbl_cartons: "Cartons Count",
    ap_lbl_pieces: "Pieces / Carton",
    ap_lbl_total: "Total Stock Items",
    ap_lbl_alert: "Low Stock Alert At (Pieces):",
    ap_sec2_title: "2. Cost & Prices",
    ap_lbl_carton_purchase: "Carton Cost (IQD)",
    ap_lbl_piece_cost_calc: "Piece Cost from Carton",
    ap_lbl_cost: "Piece Approved Cost (IQD) *",
    ap_lbl_retail_price: "Retail Price / Piece (IQD) *",
    ap_r_profit: "Retail Profit:",
    ap_r_total: "Carton Retail Val:",
    ap_r_c_profit: "Carton Retail Profit:",
    ap_lbl_wholesale_price: "Wholesale Price / Piece (IQD)",
    ap_w_profit: "Wholesale Profit:",
    ap_w_c_profit: "Carton Wholesale Profit:",
    ap_lbl_carton_sell: "Full Carton Price (IQD)",
    ap_c_profit: "Full Carton Profit:",

    // Add Product Modes (العدد المفرد / الكراتين / الوزن والكيلو)
    ap_mode_simple: "Add by Count (Single Pieces)",
    ap_mode_piece: "Add by Carton (Pieces / Cartons)",
    ap_mode_weight: "Add by Weight (Bags / Kg)",
    ap_lbl_simple_qty: "Available Stock (Pieces Count) *",
    ap_lbl_simple_alert: "Low Stock Alert At (Pieces):",
    ap_lbl_simple_cost: "Piece Purchase Cost (IQD) *",
    ap_lbl_simple_price: "Piece Retail Selling Price (IQD) *",
    ap_simple_profit: "Piece Profit:",
    ap_simple_total_val: "Total Selling Value:",
    ap_simple_expected_profit: "Total Profit:",

    // Add Product Weight Mode (كغم / فردة)
    ap_lbl_fardah_count: "Bags / Fardah Count",
    ap_lbl_kg_per_fardah: "Weight per Bag (Kg)",
    ap_lbl_total_kg: "Total Weight (Kg)",
    ap_lbl_kg_alert: "Low Stock Alert At (Kg):",
    ap_lbl_fardah_purchase: "Bag Purchase Cost (IQD)",
    ap_lbl_kg_cost_calc: "Kg Cost from Bag (Readonly)",
    ap_lbl_kg_retail_price: "Retail Price per Kg (IQD) *",
    ap_lbl_kg_wholesale_price: "Wholesale Price per Kg (IQD)",
    ap_lbl_fardah_sell: "Full Bag Selling Price (IQD)",
    ap_kg_profit: "Kg Profit:",
    ap_fardah_total: "Bag Retail Val:",
    ap_fardah_r_profit: "Bag Retail Profit:",
    ap_fardah_w_profit: "Bag Wholesale Profit:",
    ap_fardah_direct_profit: "Full Bag Profit:",

    // Inventory & Warehouse Screen
    inv_back: "Back to Main",
    inv_title: "Inventory & Stock Balance",
    inv_subtitle: "Full details of cartons, costs, sale prices, and profits",
    inv_refresh_btn: "Refresh List",
    inv_add_new_btn: "Add New Product",
    inv_kpi_total_items: "Total Items Count",
    inv_kpi_cost_val: "Warehouse Cost Value",
    inv_kpi_sell_val: "Total Selling Value",
    inv_kpi_expected_profit: "Expected Stock Profit",
    inv_search_ph: "Search by name or barcode...",
    inv_all_cats: "All Categories",
    inv_limit_1000: "Display: First 1,000 (Fast)",
    inv_limit_500: "Display: First 500 items",
    inv_limit_2000: "Display: First 2,000 items",
    inv_limit_5000: "Display: First 5,000 items",
    inv_limit_all: "Display: All Items at Once",
    inv_low_stock_only: "Low Stock Only",
    inv_th_num: "#",
    inv_th_item_barcode: "Item / Barcode",
    inv_th_cat_supplier: "Category & Supplier",
    inv_th_packaging: "Packaging & Cartons",
    inv_th_stock_qty: "Stock Quantity",
    inv_th_cost: "Piece / Carton Cost",
    inv_th_sell_prices: "Sale Prices (Retail/Wholesale/Carton)",
    inv_th_profits: "Calculated Profit",
    inv_th_status: "Status",
    inv_th_actions: "Actions",

    // Stock Audit Screen
    audit_back: "Back to Main",
    audit_title: "Smart Stock Audit & Shelf Reconciliation",
    audit_subtitle: "Verify actual shelf quantities against system balances using barcodes",
    audit_refresh_btn: "Refresh List",
    audit_save_all: "Save All Discrepancies",
    audit_kpi_total: "Total Products",
    audit_kpi_matched: "Matched (No Diff)",
    audit_kpi_shortage: "Shortage Items",
    audit_kpi_surplus: "Surplus Items",
    audit_scan_ph: "⚡ Scan barcode for live audit...",
    audit_auto_inc: "Auto Increment (+1 on scan)",
    audit_filter_all: "All Products",
    audit_filter_diff: "Discrepancies Only",
    audit_filter_shortage: "Shortage Only",
    audit_filter_surplus: "Surplus Only",
    audit_filter_matched: "Matched Only",
    audit_all_cats: "All Categories",
    audit_th_num: "#",
    audit_th_name: "Product / Barcode",
    audit_th_cat: "Category",
    audit_th_sys_stock: "System Stock",
    audit_th_actual_stock: "Actual Shelf Stock",
    audit_th_diff: "Audit Diff",
    audit_th_diff_val: "Diff Value (IQD)",
    audit_th_status: "Status",
    audit_th_action: "Update"
  }
};

// ========================================================
// TAB NAVIGATION
// ========================================================
function switchTab(tabId) {
  state.activeTab = tabId;

  // 1. Hide all tabs
  document.querySelectorAll('.tab-content').forEach(el => el.classList.add('hidden'));

  // 2. Reset sidebar items
  document.querySelectorAll('.sidebar-item').forEach(el => {
    el.classList.remove('sidebar-item-active');
  });

  // 3. Auto-hide sidebar when opening Add Product, Cashier, Inventory, or Stock Audit view as requested
  const sidebar = document.getElementById('appSidebar');
  if (sidebar) {
    if (tabId === 'addProduct' || tabId === 'cashier' || tabId === 'inventory' || tabId === 'stockAudit') {
      sidebar.classList.add('hidden');
    } else {
      sidebar.classList.remove('hidden');
    }
  }

  // 4. Auto-hide the global top navbar when in Cashier, Add Product, Inventory, or Stock Audit view to give 100% full screen
  const appTopHeader = document.getElementById('appTopHeader');
  if (appTopHeader) {
    if (tabId === 'cashier' || tabId === 'addProduct' || tabId === 'inventory' || tabId === 'stockAudit') {
      appTopHeader.classList.add('hidden');
    } else {
      appTopHeader.classList.remove('hidden');
    }
  }

  // 5. Activate selected tab
  const tabEl = document.getElementById(`tab-${tabId}`);
  if (tabEl) tabEl.classList.remove('hidden');

  const sideBtn = document.getElementById(`sidebar-${tabId}`);
  if (sideBtn) sideBtn.classList.add('sidebar-item-active');

  if (tabId === 'cashier') {
    loadProducts();
    setTimeout(() => {
      document.getElementById('cashierBarcodeInput')?.focus();
    }, 50);
  }
  if (tabId === 'addProduct') {
    loadCategoriesList();
    recalcAddProduct();
    document.getElementById('ap-barcode')?.focus();
  }
  if (tabId === 'dashboard') loadDashboard();
  if (tabId === 'inventory') loadInventory();
  if (tabId === 'purchase') initPurchaseTab();
  if (tabId === 'repOrders') loadRepOrders();
  if (tabId === 'suppliers') loadSuppliers();
  if (tabId === 'customers') loadCustomers();
  if (tabId === 'stockAudit') loadStockAudit();
  if (tabId === 'damagedItems') loadDamagedItems();
  if (tabId === 'reports') loadReports();
  if (tabId === 'users') loadUsers();
  if (tabId === 'printing') loadPrintingSettings();
  if (tabId === 'settings') loadSettingsInfo();

  lucide.createIcons();
}

// ========================================================
// THEME & LANGUAGE SWITCHER
// ========================================================
function toggleTheme() {
  state.theme = state.theme === 'dark' ? 'light' : 'dark';
  localStorage.setItem('pos_theme', state.theme);
  applyTheme(state.theme);
  if (state.activeTab === 'dashboard') {
    loadDashboard();
  }
}

function applyTheme(theme) {
  const icon = document.getElementById('themeIcon');
  const posThemeText = document.getElementById('posThemeText');
  if (theme === 'dark') {
    document.documentElement.classList.add('dark');
    document.body.classList.add('dark-theme');
    if (icon) icon.innerText = '☀️';
    if (posThemeText) posThemeText.innerText = 'شەو';
  } else {
    document.documentElement.classList.remove('dark');
    document.body.classList.remove('dark-theme');
    if (icon) icon.innerText = '🌙';
    if (posThemeText) posThemeText.innerText = 'ڕۆژ';
  }
}

function toggleLanguage() {
  state.language = state.language === 'ar' ? 'ku' : 'ar';
  setLanguage(state.language);
}

function setLanguage(lang) {
  state.language = lang;
  localStorage.setItem('pos_lang', lang);
  ['ar', 'ku', 'en'].forEach(l => {
    const btn = document.getElementById(`posLang${l.charAt(0).toUpperCase() + l.slice(1)}`);
    if (btn) {
      if (l === lang) {
        btn.className = 'px-2.5 py-1 rounded-xl bg-sky-500 text-white transition';
      } else {
        btn.className = 'px-2.5 py-1 rounded-xl text-slate-300 hover:text-white transition';
      }
    }
  });
  const langBtnText = document.getElementById('langBtnText');
  if (langBtnText) {
    langBtnText.innerText = lang === 'ar' ? 'العربية' : lang === 'ku' ? 'کوردی' : 'English';
  }
  applyLanguage(lang);
}

function applyLanguage(lang) {
  const dict = i18n[lang] || i18n.ar;
  document.querySelectorAll('[data-i18n]').forEach(el => {
    const key = el.getAttribute('data-i18n');
    if (dict[key]) {
      el.innerText = dict[key];
    }
  });

  const nameInput = document.getElementById('ap-name');
  if (nameInput) {
    nameInput.placeholder = dict.ap_name_ph || "اسم المادة";
  }

  const barcodeInput = document.getElementById('cashierBarcodeInput');
  if (barcodeInput) {
    barcodeInput.placeholder = dict.pos_search_ph || "[F3] امسح الباركود هنا (نشط)...";
  }

  const invSearch = document.getElementById('invSearchInput');
  if (invSearch) {
    invSearch.placeholder = dict.inv_search_ph || "بحث بالاسم أو الباركود...";
  }

  const auditScan = document.getElementById('auditBarcodeScannerInput');
  if (auditScan) {
    auditScan.placeholder = dict.audit_scan_ph || "⚡ امسح الباركود للجرد المباشر...";
  }

  const auditSearch = document.getElementById('audit-searchInput');
  if (auditSearch) {
    auditSearch.placeholder = dict.inv_search_ph || "بحث بالاسم أو الباركود...";
  }

  // Re-render cart, tabs, inventory, and audit to apply language changes
  renderInvoiceTabs();
  renderCashierCart();
  if (state.activeTab === 'inventory') {
    renderInventoryTable();
  }
  if (state.activeTab === 'stockAudit') {
    renderAuditTable();
  }
}

// ========================================================
// DASHBOARD CHARTS
// ========================================================
async function loadDashboard() {
  const res = await callBackend('get_dashboard_data');
  if (res && res.success) {
    document.getElementById('kpiTodayRevenue').innerText = Number(res.todayRevenue || 0).toLocaleString() + ' د.ع';
    document.getElementById('kpiTodayInvoices').innerText = Number(res.todayInvoices || 0).toLocaleString();
    document.getElementById('kpiMonthlyRevenue').innerText = Number(res.monthlyRevenue || 0).toLocaleString() + ' د.ع';
    document.getElementById('kpiLowStockCount').innerText = Number(res.lowStockCount || 0).toLocaleString();
  }

  renderAttendanceChart();
  renderPerformanceChart();
  renderActivitySplineChart();
}

function renderAttendanceChart() {
  const ctx = document.getElementById('attendanceChart');
  if (!ctx) return;

  if (state.attendanceChart) state.attendanceChart.destroy();

  state.attendanceChart = new Chart(ctx, {
    type: 'doughnut',
    data: {
      labels: ['نقداً', 'آجل'],
      datasets: [{
        data: [80, 20],
        backgroundColor: ['#A78BFA', '#FBBF24'],
        borderWidth: 0
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      cutout: '76%',
      plugins: { legend: { display: false } }
    }
  });
}

function renderPerformanceChart() {
  const ctx = document.getElementById('performanceChart');
  if (!ctx) return;

  if (state.performanceChart) state.performanceChart.destroy();

  const isDark = state.theme === 'dark';

  state.performanceChart = new Chart(ctx, {
    type: 'bar',
    data: {
      labels: ['السبت', 'الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء'],
      datasets: [
        {
          label: 'غذائية',
          data: [95, 60, 75, 90, 65],
          backgroundColor: '#38BDF8',
          borderRadius: 6,
          barPercentage: 0.7
        },
        {
          label: 'منظفات',
          data: [65, 80, 60, 75, 45],
          backgroundColor: '#FBBF24',
          borderRadius: 6,
          barPercentage: 0.7
        },
        {
          label: 'مشروبات',
          data: [75, 65, 80, 70, 50],
          backgroundColor: '#A78BFA',
          borderRadius: 6,
          barPercentage: 0.7
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        y: { 
          min: 0,
          max: 100,
          ticks: { stepSize: 25, callback: (v) => v + '%', color: isDark ? '#94A3B8' : '#94A3B8', font: { size: 10 } },
          grid: { color: isDark ? 'rgba(255,255,255,0.04)' : 'rgba(0,0,0,0.04)' }
        },
        x: { 
          ticks: { color: isDark ? '#94A3B8' : '#64748B', font: { size: 11, weight: 'bold' } },
          grid: { display: false }
        }
      }
    }
  });
}

function renderActivitySplineChart() {
  const ctx = document.getElementById('activitySplineChart');
  if (!ctx) return;

  if (state.activityChart) state.activityChart.destroy();

  state.activityChart = new Chart(ctx, {
    type: 'line',
    data: {
      labels: ['8ص', '11ص', '2ظ', '5ع', '8م', '11م'],
      datasets: [{
        data: [45, 95, 60, 110, 140, 85],
        borderColor: '#FBBF24',
        backgroundColor: 'rgba(251, 191, 36, 0.12)',
        tension: 0.45,
        fill: true,
        pointBackgroundColor: '#FBBF24',
        pointRadius: 4,
        pointHoverRadius: 6
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        y: { 
          min: 20,
          max: 160,
          ticks: { stepSize: 40, color: '#94A3B8', font: { size: 10 } },
          grid: { color: 'rgba(255,255,255,0.04)' }
        },
        x: { 
          grid: { display: false },
          ticks: { color: '#94A3B8', font: { size: 10 } }
        }
      }
    }
  });
}

// ========================================================
// CASHIER / POS SYSTEM (RAPID CHECKOUT)
// ========================================================
async function loadProducts(showAlert = false) {
  const res = await callBackend('get_pos_products');
  if (res && res.success) {
    state.products = res.products || [];
    const badge = document.getElementById('cashierTotalProductsBadge');
    if (badge) badge.innerText = state.products.length.toLocaleString();
    if (showAlert) {
      alert(`✔ تم تحديث قائمة المواد من المخزن بنجاح! (${state.products.length.toLocaleString()} مادة جاهزة للبيع)`);
    }
  }
}

async function loadSuppliersList() {
  const res = await callBackend('get_suppliers');
  if (res && res.success) {
    state.suppliers = res.suppliers || [];
    const supSelect = document.getElementById('ap-supplier');
    if (supSelect) {
      supSelect.innerHTML = '<option value="">بدون مندوب (مباشر)</option>';
      state.suppliers.forEach(s => {
        supSelect.innerHTML += `<option value="${s.name}">${s.name} (${s.company || 'شركة'})</option>`;
      });
    }
  }
}

// Ensure invoice tabs are strictly numbered sequentially (فاتورة 1 / پەنجەرە 1 / Invoice 1)
function reindexInvoiceTabs() {
  const prefix = state.language === 'en' ? 'Invoice' : state.language === 'ku' ? 'پەنجەرە' : 'فاتورة';
  state.invoiceTabs.forEach((tab, index) => {
    tab.title = `${prefix} ${index + 1}`;
  });
}

function getCurrentTab() {
  return state.invoiceTabs.find(t => t.id === state.selectedInvoiceTabId) || state.invoiceTabs[0];
}

function renderInvoiceTabs() {
  const container = document.getElementById('invoiceTabsContainer');
  if (!container) return;

  reindexInvoiceTabs();
  container.innerHTML = '';
  state.invoiceTabs.forEach(t => {
    const isSel = t.id === state.selectedInvoiceTabId;
    const tabEl = document.createElement('div');
    tabEl.className = `flex items-center gap-1.5 px-3 py-1 rounded-xl cursor-pointer text-xs font-bold transition border ${isSel ? 'bg-gradient-to-r from-teal-600 to-emerald-600 text-white border-teal-400 shadow-md' : 'pos-subpanel hover:bg-slate-200 dark:hover:bg-slate-800'}`;
    tabEl.onclick = () => selectInvoiceTab(t.id);
    tabEl.innerHTML = `
      <span>${t.title}</span>
      <span class="bg-black/20 dark:bg-black/40 px-1.5 py-0.2 rounded-full text-[10px] font-mono">${t.items.length}</span>
      ${state.invoiceTabs.length > 1 ? `<button onclick="event.stopPropagation(); closeInvoiceTab('${t.id}')" class="text-rose-400 hover:text-rose-600 px-1 font-black" title="إغلاق">✕</button>` : ''}
    `;
    container.appendChild(tabEl);
  });
}

function addNewInvoiceTab() {
  const newId = 'inv_' + Date.now() + '_' + Math.floor(Math.random() * 1000);
  const prefix = state.language === 'en' ? 'Invoice' : state.language === 'ku' ? 'پەنجەرە' : 'فاتورة';
  state.invoiceTabs.push({
    id: newId,
    title: `${prefix} ${state.invoiceTabs.length + 1}`,
    items: [],
    discount: 0,
    paid: 0,
    paymentMethod: 'Cash'
  });
  reindexInvoiceTabs();
  selectInvoiceTab(newId);
}

function selectInvoiceTab(id) {
  state.selectedInvoiceTabId = id;
  renderInvoiceTabs();
  renderCashierCart();
  setTimeout(() => {
    document.getElementById('cashierBarcodeInput')?.focus();
  }, 50);
}

function closeInvoiceTab(id) {
  if (state.invoiceTabs.length <= 1) return;
  const index = state.invoiceTabs.findIndex(t => t.id === id || String(t.id) === String(id));
  state.invoiceTabs = state.invoiceTabs.filter(t => t.id !== id && String(t.id) !== String(id));
  reindexInvoiceTabs();
  if (state.selectedInvoiceTabId === id || String(state.selectedInvoiceTabId) === String(id)) {
    const nextTab = state.invoiceTabs[Math.max(0, index - 1)] || state.invoiceTabs[0];
    state.selectedInvoiceTabId = nextTab.id;
  }
  renderInvoiceTabs();
  renderCashierCart();
  setTimeout(() => {
    document.getElementById('cashierBarcodeInput')?.focus();
  }, 50);
}

// Flash Barcode Input RED on Not Found Error
function flashBarcodeError() {
  const input = document.getElementById('cashierBarcodeInput');
  if (!input) return;
  input.classList.add('border-rose-500', 'text-rose-400', 'ring-4', 'ring-rose-500/30');
  input.select();
  setTimeout(() => {
    input.classList.remove('border-rose-500', 'text-rose-400', 'ring-4', 'ring-rose-500/30');
  }, 1800);
}

async function handleBarcodeKeyDown(e) {
  if (e.key === 'Enter') {
    e.preventDefault();
    const input = document.getElementById('cashierBarcodeInput');
    const rawQuery = input ? input.value : '';
    const query = rawQuery.replace(/[\r\n]/g, '').trim();
    if (!query) return;

    hideCashierSearchResults();

    // 1. Check local state.products (exact barcode or name)
    let matched = state.products.find(p => 
      (p.barcode && String(p.barcode).trim().toLowerCase() === query.toLowerCase()) ||
      (p.name && String(p.name).trim().toLowerCase() === query.toLowerCase())
    );

    // 2. Check local state.products (partial match or contains)
    if (!matched) {
      matched = state.products.find(p => 
        (p.barcode && String(p.barcode).includes(query)) ||
        (p.name && String(p.name).toLowerCase().includes(query.toLowerCase()))
      );
    }

    // 3. Fallback: Search directly in database via backend RPC
    if (!matched) {
      const res = await callBackend('find_product', { query });
      if (res && res.success && res.found && res.product) {
        matched = res.product;
        if (!state.products.some(p => p.id === matched.id)) {
          state.products.push(matched);
        }
      }
    }

    if (matched) {
      addItemToCurrentCart(matched);
      if (input) input.value = '';
      setTimeout(() => input?.focus(), 50);
    } else {
      flashBarcodeError();
    }
  }
}

function handleCashierSearchInput() {
  const input = document.getElementById('cashierBarcodeInput');
  const query = input ? input.value.trim().toLowerCase() : '';
  const resultsContainer = document.getElementById('cashierSearchResults');
  if (!resultsContainer) return;

  if (!query || query.length < 1) {
    resultsContainer.classList.add('hidden');
    resultsContainer.innerHTML = '';
    return;
  }

  const matches = state.products.filter(p => 
    (p.barcode && p.barcode.toLowerCase().includes(query)) ||
    (p.name && p.name.toLowerCase().includes(query))
  ).slice(0, 8);

  if (matches.length === 0) {
    resultsContainer.classList.add('hidden');
    return;
  }

  resultsContainer.innerHTML = '';
  matches.forEach(p => {
    const item = document.createElement('div');
    item.className = 'p-3 hover:bg-slate-800/80 cursor-pointer flex items-center justify-between transition border-b border-slate-800/60';
    item.onclick = () => {
      addItemToCurrentCart(p);
      input.value = '';
      resultsContainer.classList.add('hidden');
      input.focus();
    };
    item.innerHTML = `
      <div class="flex items-center gap-2.5">
        <span class="text-lg">🏷</span>
        <div>
          <div class="font-bold text-xs text-white">${p.name || p.Name}</div>
          <div class="text-[10px] font-mono text-sky-400">باركود: ${p.barcode || p.Barcode || '--'}</div>
        </div>
      </div>
      <div class="text-left">
        <div class="font-black text-xs text-emerald-400 font-mono">${Number(p.price || p.Price || 0).toLocaleString()} د.ع</div>
        <div class="text-[10px] text-slate-400 font-bold">الرصيد: ${p.stockQuantity || 0} قطعة</div>
      </div>
    `;
    resultsContainer.appendChild(item);
  });
  resultsContainer.classList.remove('hidden');
}

function hideCashierSearchResults() {
  const rc = document.getElementById('cashierSearchResults');
  if (rc) rc.classList.add('hidden');
}

// Barcode Auto-Focus Controller
let isBarcodeFocusPaused = false;

function pauseBarcodeFocus() {
  isBarcodeFocusPaused = true;
}

function resumeBarcodeFocus() {
  isBarcodeFocusPaused = false;
  setTimeout(() => {
    if (state.activeTab === 'cashier' && !isBarcodeFocusPaused) {
      const active = document.activeElement;
      if (!active || (active.tagName !== 'INPUT' && active.tagName !== 'SELECT' && active.tagName !== 'TEXTAREA')) {
        document.getElementById('cashierBarcodeInput')?.focus();
      }
    }
  }, 100);
}

function addItemToCurrentCart(product) {
  const currentTab = getCurrentTab();
  const prodId = product.id || product.Id;
  const prodName = product.name || product.Name || 'مادة بدون اسم';
  const prodBarcode = product.barcode || product.Barcode || '--';
  const prodCost = Number(product.cost ?? product.Cost ?? 0) || 0;
  const prodRetailPrice = Number(product.price ?? product.Price ?? 0) || 0;
  const prodWholesalePrice = Number(product.wholesalePrice ?? product.WholesalePrice ?? prodRetailPrice) || prodRetailPrice;
  const prodCartonPrice = Number(product.cartonSellingPrice ?? product.CartonSellingPrice ?? 0) || (prodRetailPrice * (Number(product.piecesPerCarton || product.ItemsPerCarton || 1) || 1));
  const piecesPerCarton = Number(product.piecesPerCarton ?? product.ItemsPerCarton ?? 1) || 1;

  const existing = currentTab.items.find(i => i.id === prodId && i.saleType === 'retail');

  if (existing) {
    existing.qty = (Number(existing.qty) || 1) + 1;
  } else {
    currentTab.items.push({
      id: prodId,
      name: prodName,
      barcode: prodBarcode,
      cost: prodCost,
      retailPrice: prodRetailPrice,
      wholesalePrice: prodWholesalePrice,
      cartonPrice: prodCartonPrice,
      piecesPerCarton: piecesPerCarton,
      saleType: 'retail',
      price: prodRetailPrice,
      qty: 1
    });
  }

  renderInvoiceTabs();
  renderCashierCart();
}

function changeCartItemSaleType(id, newType) {
  const currentTab = getCurrentTab();
  const item = currentTab.items.find(i => i.id === id);
  if (item) {
    item.saleType = newType;
    if (newType === 'wholesale') {
      item.price = item.wholesalePrice || item.retailPrice;
    } else if (newType === 'carton') {
      item.price = item.cartonPrice || (item.retailPrice * item.piecesPerCarton);
    } else {
      item.price = item.retailPrice;
    }
    renderCashierCart();
  }
}

function setCartItemDirectQty(id, val) {
  const currentTab = getCurrentTab();
  const item = currentTab.items.find(i => i.id === id);
  if (item) {
    const parsed = parseFloat(val);
    if (!isNaN(parsed) && parsed > 0) {
      item.qty = parsed;
    } else {
      item.qty = 1;
    }
    recalcCashierInvoice();
  }
}

function updateCartItemQty(id, delta) {
  const currentTab = getCurrentTab();
  const item = currentTab.items.find(i => i.id === id);
  if (item) {
    item.qty = Math.max(0, (Number(item.qty) || 1) + delta);
    if (item.qty <= 0) {
      currentTab.items = currentTab.items.filter(i => i.id !== id);
    }
  }
  renderInvoiceTabs();
  renderCashierCart();
}

function removeCartItem(id) {
  const currentTab = getCurrentTab();
  currentTab.items = currentTab.items.filter(i => i.id !== id);
  renderInvoiceTabs();
  renderCashierCart();
}

function clearCurrentInvoice() {
  const currentTab = getCurrentTab();
  currentTab.items = [];
  const discountInput = document.getElementById('cashierDiscountInput');
  const paidInput = document.getElementById('cashierPaidInput');
  const taxInput = document.getElementById('cashierTaxInput');
  if (discountInput) discountInput.value = 0;
  if (paidInput) paidInput.value = 0;
  if (taxInput) taxInput.value = 0;

  renderInvoiceTabs();
  renderCashierCart();
  resumeBarcodeFocus();
}

function renderCashierCart() {
  const currentTab = getCurrentTab();
  const emptyState = document.getElementById('cashierCartEmptyState');
  const tableWrapper = document.getElementById('cashierCartTableWrapper');
  const tbody = document.getElementById('cashierCartTbody');
  const dict = i18n[state.language] || i18n.ar;

  if (!tbody) return;

  if (currentTab.items.length === 0) {
    if (emptyState) emptyState.classList.remove('hidden');
    if (tableWrapper) tableWrapper.classList.add('hidden');
    recalcCashierInvoice();
    return;
  }

  if (emptyState) emptyState.classList.add('hidden');
  if (tableWrapper) tableWrapper.classList.remove('hidden');

  tbody.innerHTML = '';
  currentTab.items.forEach((item, index) => {
    const itemCost = Number(item.cost) || 0;
    const itemPrice = Number(item.price) || 0;
    const itemQty = Number(item.qty) || 1;
    const itemTotal = itemPrice * itemQty;
    const saleType = item.saleType || 'retail';

    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-800/50 transition border-b border-slate-800/60';
    tr.innerHTML = `
      <td class="p-2 text-center text-slate-400 font-bold font-mono">${index + 1}</td>
      <td class="p-2 font-bold text-white">
        <div>${item.name}</div>
        <div class="text-[9px] font-mono text-sky-400">${item.barcode || '--'}</div>
      </td>
      <td class="p-2 text-center font-mono text-slate-400 font-semibold text-[11px]">${itemCost.toLocaleString()} د.ع</td>
      <td class="p-2 text-center font-mono text-emerald-400 font-bold text-[11px]">${itemPrice.toLocaleString()} د.ع</td>
      <td class="p-2 text-center">
        <select onfocus="pauseBarcodeFocus()" onblur="resumeBarcodeFocus()" onchange="changeCartItemSaleType('${item.id}', this.value)" class="bg-[#060c1c] border border-slate-700 rounded-lg px-1.5 py-0.5 text-[10px] font-bold text-sky-400 focus:outline-none focus:border-sky-500">
          <option value="retail" ${saleType === 'retail' ? 'selected' : ''}>${dict.sale_type_retail || 'مفرد'}</option>
          <option value="wholesale" ${saleType === 'wholesale' ? 'selected' : ''}>${dict.sale_type_wholesale || 'جملة'}</option>
          <option value="carton" ${saleType === 'carton' ? 'selected' : ''}>${dict.sale_type_carton || 'كرتون'}</option>
        </select>
      </td>
      <td class="p-2 text-center">
        <div class="inline-flex items-center gap-1 bg-[#060c1c] border border-slate-700 px-1 py-0.5 rounded-xl">
          <button onclick="updateCartItemQty('${item.id}', -1)" class="w-5 h-5 rounded-lg bg-rose-500/20 hover:bg-rose-500/40 text-rose-400 font-black text-xs flex items-center justify-center">-</button>
          <input type="number" step="any" min="0.1" value="${itemQty}" onfocus="pauseBarcodeFocus()" onblur="resumeBarcodeFocus()" oninput="setCartItemDirectQty('${item.id}', this.value)" onkeydown="if(event.key==='Enter'){this.blur();}" class="w-12 bg-transparent text-center font-black text-white font-mono text-xs focus:ring-1 focus:ring-sky-500 rounded outline-none">
          <button onclick="updateCartItemQty('${item.id}', 1)" class="w-5 h-5 rounded-lg bg-emerald-500/20 hover:bg-emerald-500/40 text-emerald-400 font-black text-xs flex items-center justify-center">+</button>
        </div>
      </td>
      <td class="p-2 text-center font-black text-emerald-300 font-mono text-xs">${itemTotal.toLocaleString()} د.ع</td>
      <td class="p-2 text-center">
        <button onclick="removeCartItem('${item.id}')" class="p-1 hover:bg-rose-500/20 text-rose-400 rounded-lg font-bold text-xs" title="حذف">🗑</button>
      </td>
    `;
    tbody.appendChild(tr);
  });

  recalcCashierInvoice();
}

function recalcCashierInvoice() {
  const currentTab = getCurrentTab();
  const totalItems = currentTab.items.reduce((sum, i) => sum + (Number(i.qty) || 1), 0);
  const subtotal = currentTab.items.reduce((sum, i) => sum + ((Number(i.price) || 0) * (Number(i.qty) || 1)), 0);
  const discount = Math.max(0, Number(document.getElementById('cashierDiscountInput')?.value || 0));
  const tax = Math.max(0, Number(document.getElementById('cashierTaxInput')?.value || 0));
  const paid = Math.max(0, Number(document.getElementById('cashierPaidInput')?.value || 0));

  const total = Math.max(0, subtotal - discount + tax);
  const change = Math.max(0, paid - total);

  const subtotalEl = document.getElementById('cashierSubtotal');
  const totalEl = document.getElementById('cashierTotalDisplay');
  const changeEl = document.getElementById('cashierChangeDisplay');
  const countBadgeEl = document.getElementById('cashierItemCountBadge');

  if (subtotalEl) subtotalEl.innerText = `${subtotal.toLocaleString()} د.ع`;
  if (totalEl) totalEl.innerText = `${total.toLocaleString()} د.ع`;
  if (changeEl) changeEl.innerText = `${change.toLocaleString()} د.ع`;
  if (countBadgeEl) countBadgeEl.innerText = `${totalItems} ${state.language === 'en' ? 'Items in Cart' : state.language === 'ku' ? 'کاڵا لە سەبەتەدا' : 'مواد في السلة'}`;
}

function setPaidAmount(amt) {
  const paidInput = document.getElementById('cashierPaidInput');
  if (paidInput) {
    paidInput.value = amt;
    recalcCashierInvoice();
  }
}

function setPaymentMethod(pm) {
  const currentTab = getCurrentTab();
  currentTab.paymentMethod = pm;
  ['Cash', 'Debt', 'Card', 'Nfc'].forEach(m => {
    const btn = document.getElementById(`pm-${m}`);
    if (btn) {
      if (m === pm) {
        btn.className = 'py-1.5 px-2 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs rounded-xl shadow-md flex items-center justify-center gap-1 transition';
      } else {
        btn.className = 'py-1.5 px-2 bg-[#060c1c] hover:bg-slate-800 text-slate-300 border border-slate-700 font-bold text-xs rounded-xl flex items-center justify-center gap-1 transition';
      }
    }
  });
}

// ========================================================
// SOLD & RETURNED INVOICES MODAL
// ========================================================
let allInvoicesHistory = [];

async function openInvoicesHistoryModal() {
  pauseBarcodeFocus();
  const modal = document.getElementById('invoicesHistoryModal');
  if (modal) modal.classList.remove('hidden');
  await loadInvoicesHistory();
}

function closeInvoicesHistoryModal() {
  const modal = document.getElementById('invoicesHistoryModal');
  if (modal) modal.classList.add('hidden');
  resumeBarcodeFocus();
}

async function loadInvoicesHistory(showAlert = false) {
  const tbody = document.getElementById('invoicesHistoryTbody');
  if (tbody) tbody.innerHTML = '<tr><td colspan="7" class="text-center py-6 text-slate-400 font-bold">جارٍ تحميل الفواتير...</td></tr>';

  const res = await callBackend('get_invoices');
  if (res && res.success) {
    allInvoicesHistory = res.invoices || [];
    filterInvoicesHistory();
    if (showAlert) {
      alert(`✔ تم تحديث قائمة الفواتير (${allInvoicesHistory.length} فاتورة مسجلة)`);
    }
  } else {
    if (tbody) tbody.innerHTML = '<tr><td colspan="7" class="text-center py-6 text-rose-400 font-bold">تعذر تحميل الفواتير</td></tr>';
  }
}

function filterInvoicesHistory() {
  const q = document.getElementById('inv-search-input')?.value.trim().toLowerCase() || '';
  const statusFilter = document.getElementById('inv-status-filter')?.value || 'ALL';
  const tbody = document.getElementById('invoicesHistoryTbody');
  const countBadge = document.getElementById('invoicesCountBadge');
  if (!tbody) return;

  let filtered = allInvoicesHistory;
  if (statusFilter !== 'ALL') {
    filtered = filtered.filter(i => (i.status || 'Completed') === statusFilter);
  }
  if (q) {
    filtered = filtered.filter(i => 
      (i.invoiceNumber && i.invoiceNumber.toLowerCase().includes(q)) ||
      (i.customerName && i.customerName.toLowerCase().includes(q))
    );
  }

  if (countBadge) countBadge.innerText = `${filtered.length} پسوولە دۆزرایەوە (فواتير)`;

  if (filtered.length === 0) {
    tbody.innerHTML = '<tr><td colspan="7" class="text-center py-6 text-slate-500 font-bold">هیچ پسوولەیەک نەدۆزرایەوە (لا توجد فواتير)</td></tr>';
    return;
  }

  tbody.innerHTML = '';
  filtered.forEach((inv) => {
    const isReturned = inv.status === 'Returned';
    const tr = document.createElement('tr');
    tr.className = `hover:bg-slate-800/60 transition ${isReturned ? 'bg-rose-950/20' : ''}`;
    tr.innerHTML = `
      <td class="p-2.5 font-mono text-sky-400 font-bold">${inv.invoiceNumber}</td>
      <td class="p-2.5 font-bold text-white">${inv.customerName || 'زبون نقدي'}</td>
      <td class="p-2.5 text-center">
        <span class="px-2 py-0.5 rounded-lg text-[10px] font-bold bg-slate-800 text-slate-300">${inv.paymentMethod || 'Cash'}</span>
      </td>
      <td class="p-2.5 text-center font-mono font-black text-emerald-400">${Number(inv.totalAmount || 0).toLocaleString()} د.ع</td>
      <td class="p-2.5 text-center">
        ${isReturned 
          ? '<span class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-rose-500/20 text-rose-300 border border-rose-500/30">گەڕاوە (مرتجع)</span>'
          : '<span class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">فرۆشراو (مباعة)</span>'}
      </td>
      <td class="p-2.5 text-center text-slate-400 text-[11px] font-mono">${inv.createdAt || '--'}</td>
      <td class="p-2.5 text-center">
        <div class="flex items-center justify-center gap-1">
          <button onclick="reprintInvoiceReceipt('${inv.invoiceNumber}')" class="px-2 py-1 bg-sky-900/60 hover:bg-sky-800 text-sky-300 rounded-lg text-[10px] font-bold" title="إعادة طباعة">🖨️ چاپ</button>
          ${!isReturned ? `<button onclick="returnInvoiceAction('${inv.invoiceNumber}')" class="px-2 py-1 bg-rose-900/60 hover:bg-rose-800 text-rose-300 rounded-lg text-[10px] font-bold" title="إرجاع الوصل">🔄 إرجاع</button>` : ''}
        </div>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

async function returnInvoiceAction(invoiceNumber) {
  if (!confirm(`هل أنت متأكد من إرجاع الفاتورة ${invoiceNumber} وإعادة كامل كمياتها إلى المخزن؟`)) return;
  const res = await callBackend('return_invoice', { invoiceNumber });
  if (res && res.success) {
    alert('✔ تم إرجاع الفاتورة بنجاح وإعادة رصيد المواد إلى المخزن!');
    await loadInvoicesHistory();
    await loadProducts();
    await loadInventory();
    await loadDashboard();
  } else {
    alert('تعذر إرجاع الفاتورة: ' + (res?.message || ''));
  }
}

function reprintInvoiceReceipt(invoiceNumber) {
  const inv = allInvoicesHistory.find(i => i.invoiceNumber === invoiceNumber);
  if (!inv) return;
  window.print();
}

async function submitCashierSale(printReceipt = false) {
  const currentTab = getCurrentTab();
  if (currentTab.items.length === 0) {
    alert('سەبەتە بەتاڵە! تکایە پێش فرۆشتن کاڵا زیاد بکە.');
    return;
  }

  const discount = Math.max(0, Number(document.getElementById('cashierDiscountInput')?.value || 0));
  const payload = {
    paymentMethod: currentTab.paymentMethod || 'Cash',
    discount: discount,
    items: currentTab.items
  };

  const res = await callBackend('complete_sale', payload);
  if (res && res.success) {
    alert(`🎉 فرۆشتن بە سەرکەوتوویی تەواو بوو!\nژمارەی پسوولە: ${res.invoiceNumber}\nبڕی پارە: ${Number(res.total).toLocaleString()} د.ع`);
    
    if (printReceipt || isFastPrintEnabled) {
      window.print();
    }

    currentTab.items = [];
    document.getElementById('cashierDiscountInput').value = 0;
    document.getElementById('cashierPaidInput').value = 0;
    document.getElementById('cashierTaxInput').value = 0;
    
    renderInvoiceTabs();
    renderCashierCart();
    loadDashboard();
    loadInventory();
  } else {
    alert('هەڵەیەک ڕوویدا لە کاتی فرۆشتندا: ' + (res?.message || ''));
  }
}

// --------------------------------------------------------
// QUICK WAREHOUSE PRODUCTS MODAL (کۆگا F4)
// --------------------------------------------------------
function openQuickProductModal() {
  renderQuickProductsGrid(state.products);
  document.getElementById('quickProductModal')?.classList.remove('hidden');
  setTimeout(() => {
    document.getElementById('qpm-search')?.focus();
  }, 100);
}

function closeQuickProductModal() {
  document.getElementById('quickProductModal')?.classList.add('hidden');
  document.getElementById('cashierBarcodeInput')?.focus();
}

function renderQuickProductsGrid(list) {
  const grid = document.getElementById('qpm-grid');
  if (!grid) return;

  grid.innerHTML = '';
  list.slice(0, 60).forEach(p => {
    const card = document.createElement('div');
    card.className = 'p-3 bg-slate-900/90 hover:bg-slate-800 border border-slate-700/80 hover:border-emerald-500 rounded-xl cursor-pointer transition flex flex-col justify-between';
    card.onclick = () => {
      addItemToCurrentCart(p);
      closeQuickProductModal();
    };
    card.innerHTML = `
      <div>
        <div class="font-bold text-xs text-white line-clamp-1">${p.name || p.Name}</div>
        <div class="text-[10px] font-mono text-sky-400">${p.barcode || p.Barcode || '--'}</div>
      </div>
      <div class="flex items-center justify-between pt-2 mt-2 border-t border-slate-800 text-xs">
        <span class="font-black text-emerald-400 font-mono">${Number(p.price || p.Price || 0).toLocaleString()} د.ع</span>
        <span class="text-[10px] text-slate-400 font-bold">رصيد: ${p.stockQuantity || 0}</span>
      </div>
    `;
    grid.appendChild(card);
  });
}

function filterQuickProductModal() {
  const q = (document.getElementById('qpm-search')?.value || '').toLowerCase().trim();
  const filtered = state.products.filter(p => 
    (p.name && p.name.toLowerCase().includes(q)) || (p.barcode && p.barcode.includes(q))
  );
  renderQuickProductsGrid(filtered);
}

let isFastPrintEnabled = false;
function toggleFastPrint() {
  isFastPrintEnabled = !isFastPrintEnabled;
  const btn = document.getElementById('btnFastPrint');
  if (btn) {
    btn.className = isFastPrintEnabled 
      ? 'px-3.5 py-2 bg-teal-600 text-white font-bold text-xs rounded-xl shadow-md flex items-center gap-1.5 transition whitespace-nowrap'
      : 'px-3.5 py-2 bg-slate-900 border border-teal-500/40 text-teal-400 hover:bg-teal-950/40 rounded-xl text-xs font-bold flex items-center gap-1.5 transition whitespace-nowrap';
  }
}

let isSmartPrintEnabled = false;
function toggleSmartPrint() {
  isSmartPrintEnabled = !isSmartPrintEnabled;
  const btn = document.getElementById('btnSmartPrint');
  if (btn) {
    btn.className = isSmartPrintEnabled
      ? 'flex-1 py-1.5 bg-teal-600 text-white font-bold rounded-xl text-[11px] flex items-center justify-center gap-1 transition'
      : 'flex-1 py-1.5 bg-[#060c1c] border border-teal-500/40 text-teal-400 hover:bg-teal-950/40 rounded-xl text-[11px] font-bold flex items-center justify-center gap-1 transition';
  }
}

let isReturnMode = false;
function toggleReturnMode() {
  isReturnMode = !isReturnMode;
  const btn = document.getElementById('btnReturnMode');
  if (btn) {
    btn.className = isReturnMode
      ? 'px-3 py-2 bg-rose-600 text-white font-bold text-xs rounded-xl shadow-md flex items-center gap-1.5 transition whitespace-nowrap'
      : 'px-3 py-2 bg-slate-900 border border-slate-700 text-slate-300 hover:bg-slate-800 rounded-xl text-xs font-bold flex items-center gap-1.5 transition whitespace-nowrap';
  }
}

// Global Keyboard Shortcuts Listener
window.addEventListener('keydown', (e) => {
  if (state.activeTab !== 'cashier') return;

  if (e.key === 'F1') {
    e.preventDefault();
    addNewInvoiceTab();
  } else if (e.key === 'F3') {
    e.preventDefault();
    document.getElementById('cashierBarcodeInput')?.focus();
  } else if (e.key === 'F4') {
    e.preventDefault();
    openQuickProductModal();
  } else if (e.key === 'F8') {
    e.preventDefault();
    clearCurrentInvoice();
  } else if (e.key === 'F12') {
    e.preventDefault();
    submitCashierSale(true);
  }
});

// ========================================================
// ADD / EDIT PRODUCT FULL FORM (DETAILED MARKET LOGIC)
// ========================================================
function generateUniqueMarketBarcode() {
  const prefix = "200245";
  let uniqueBarcode = "";
  let attempts = 0;

  do {
    const randomPart = Math.floor(100000 + Math.random() * 900000); // 6 digits
    uniqueBarcode = `${prefix}${randomPart}`;
    attempts++;
  } while (state.products.some(p => p.barcode === uniqueBarcode) && attempts < 500);

  const barcodeInput = document.getElementById('ap-barcode');
  if (barcodeInput) {
    barcodeInput.value = uniqueBarcode;
  }

  // Auto focus on Product Name as requested
  setTimeout(() => {
    document.getElementById('ap-name')?.focus();
  }, 50);
}

function handleBarcodeEnter(e) {
  if (e.key === 'Enter') {
    e.preventDefault();
    document.getElementById('ap-name')?.focus();
  }
}

async function loadCategoriesList() {
  const res = await callBackend('get_categories');
  const catSelect = document.getElementById('ap-categorySelect');
  if (!catSelect) return;

  const currentVal = catSelect.value || "عام";
  catSelect.innerHTML = '';
  
  const cats = (res && res.success && res.categories && res.categories.length > 0) 
    ? res.categories 
    : ["عام", "مواد غذائية", "معلبات", "منظفات", "مشروبات وعصائر", "ألبان وأجبان", "حلويات وبسكويت"];

  cats.forEach(c => {
    catSelect.innerHTML += `<option value="${c}">${c}</option>`;
  });

  if (cats.includes(currentVal)) {
    catSelect.value = currentVal;
  }
}

async function promptAddCategory() {
  const newCat = prompt("أدخل اسم التصنيف الجديد:");
  if (newCat && newCat.trim()) {
    const res = await callBackend('add_category', { name: newCat.trim() });
    await loadCategoriesList();
    const catSelect = document.getElementById('ap-categorySelect');
    if (catSelect) catSelect.value = newCat.trim();
  }
}

async function deleteCurrentCategory() {
  const catSelect = document.getElementById('ap-categorySelect');
  if (!catSelect) return;
  const selectedCat = catSelect.value;
  if (!selectedCat || selectedCat === "عام") {
    alert("لا يمكن حذف التصنيف العام الأساسي!");
    return;
  }

  if (confirm(`هل أنت متأكد من حذف التصنيف: "${selectedCat}"؟`)) {
    await callBackend('delete_category', { name: selectedCat });
    await loadCategoriesList();
  }
}

let currentAddProductMode = 'simple'; // 'simple', 'piece', or 'weight'

function switchAddProductMode(mode) {
  currentAddProductMode = mode;
  const btnSimple = document.getElementById('apModeBtnSimple');
  const btnPiece = document.getElementById('apModeBtnPiece');
  const btnWeight = document.getElementById('apModeBtnWeight');

  const pkgSimple = document.getElementById('ap-packaging-simple');
  const pkgPiece = document.getElementById('ap-packaging-piece');
  const pkgWeight = document.getElementById('ap-packaging-weight');
  const priceSimple = document.getElementById('ap-pricing-simple');
  const pricePiece = document.getElementById('ap-pricing-piece');
  const priceWeight = document.getElementById('ap-pricing-weight');

  // Reset all buttons styling
  const defaultBtnClass = 'flex-1 py-2 px-3 rounded-xl text-xs font-black flex items-center justify-center gap-1.5 bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700 transition';
  if (btnSimple) btnSimple.className = defaultBtnClass;
  if (btnPiece) btnPiece.className = defaultBtnClass;
  if (btnWeight) btnWeight.className = defaultBtnClass;

  // Hide all sections
  if (pkgSimple) pkgSimple.classList.add('hidden');
  if (pkgPiece) pkgPiece.classList.add('hidden');
  if (pkgWeight) pkgWeight.classList.add('hidden');
  if (priceSimple) priceSimple.classList.add('hidden');
  if (pricePiece) pricePiece.classList.add('hidden');
  if (priceWeight) priceWeight.classList.add('hidden');

  if (mode === 'simple') {
    if (btnSimple) {
      btnSimple.className = 'flex-1 py-2 px-3 rounded-xl text-xs font-black flex items-center justify-center gap-1.5 bg-emerald-600 text-white shadow-md transition';
    }
    if (pkgSimple) pkgSimple.classList.remove('hidden');
    if (priceSimple) priceSimple.classList.remove('hidden');
    recalcAddProductSimple();
  } else if (mode === 'weight') {
    if (btnWeight) {
      btnWeight.className = 'flex-1 py-2 px-3 rounded-xl text-xs font-black flex items-center justify-center gap-1.5 bg-amber-500 text-white shadow-md transition';
    }
    if (pkgWeight) pkgWeight.classList.remove('hidden');
    if (priceWeight) priceWeight.classList.remove('hidden');
    recalcAddProductWeight();
  } else {
    if (btnPiece) {
      btnPiece.className = 'flex-1 py-2 px-3 rounded-xl text-xs font-black flex items-center justify-center gap-1.5 bg-sky-500 text-white shadow-md transition';
    }
    if (pkgPiece) pkgPiece.classList.remove('hidden');
    if (pricePiece) pricePiece.classList.remove('hidden');
    recalcAddProduct();
  }
}

function recalcAddProductSimple() {
  const qty = Number(document.getElementById('ap-simpleStockQty')?.value || 0);
  const cost = Number(document.getElementById('ap-simpleCost')?.value || 0);
  const price = Number(document.getElementById('ap-simplePrice')?.value || 0);

  const costInput = document.getElementById('ap-cost');
  if (costInput) {
    costInput.value = cost;
  }

  const pieceProfit = (price > 0 && cost > 0) ? (price - cost) : 0;
  const totalSell = (price > 0 && qty > 0) ? (price * qty) : 0;
  const totalProfit = (pieceProfit > 0 && qty > 0) ? (pieceProfit * qty) : 0;

  const ppEl = document.getElementById('ap-simplePieceProfit');
  if (ppEl) ppEl.innerText = `${pieceProfit >= 0 ? '+' : ''}${Math.round(pieceProfit).toLocaleString()} د.ع`;
  const tsEl = document.getElementById('ap-simpleTotalSell');
  if (tsEl) tsEl.innerText = `${Math.round(totalSell).toLocaleString()} د.ع`;
  const tpEl = document.getElementById('ap-simpleTotalProfit');
  if (tpEl) tpEl.innerText = `${totalProfit >= 0 ? '+' : ''}${Math.round(totalProfit).toLocaleString()} د.ع`;
}

function recalcAddProduct() {
  const itemsPerCarton = Number(document.getElementById('ap-itemsPerCarton')?.value || 0);
  const cartonsCount = Number(document.getElementById('ap-cartonsCount')?.value || 0);
  const cartonPurchase = Number(document.getElementById('ap-cartonPurchase')?.value || 0);

  // Total stock in pieces
  const totalPieces = itemsPerCarton * cartonsCount;
  const totalStockEl = document.getElementById('ap-totalStock');
  if (totalStockEl) {
    totalStockEl.value = (itemsPerCarton > 0 && cartonsCount > 0) ? `${totalPieces} قطعة` : '';
  }

  // Calculated Piece Cost from Carton (Readonly)
  const pieceCostFromCarton = itemsPerCarton > 0 ? (cartonPurchase / itemsPerCarton) : 0;
  const pieceCostDisplayEl = document.getElementById('ap-pieceCostFromCarton');
  if (pieceCostDisplayEl) {
    pieceCostDisplayEl.value = pieceCostFromCarton > 0 ? `${Math.round(pieceCostFromCarton).toLocaleString()} د.ع` : '';
  }

  const cost = Math.round(pieceCostFromCarton);
  const costInput = document.getElementById('ap-cost');
  if (costInput) {
    costInput.value = cost;
  }
  const price = Number(document.getElementById('ap-price')?.value || 0); // بيع مفرد
  const wholesale = Number(document.getElementById('ap-wholesalePrice')?.value || 0); // بيع جملة
  const cartonSelling = Number(document.getElementById('ap-cartonSelling')?.value || 0); // بيع كرتون

  // 1. Retail calculations
  const retailPieceProfit = (price > 0 && cost > 0) ? (price - cost) : 0;
  const retailCartonTotal = (price > 0 && itemsPerCarton > 0) ? (price * itemsPerCarton) : 0;
  const retailCartonProfit = (price > 0 && cost > 0 && itemsPerCarton > 0) ? ((price - cost) * itemsPerCarton) : 0;

  const rppEl = document.getElementById('ap-retailPieceProfit');
  if (rppEl) rppEl.innerText = `${retailPieceProfit >= 0 ? '+' : ''}${Math.round(retailPieceProfit).toLocaleString()} د.ع`;
  const rctEl = document.getElementById('ap-retailCartonTotal');
  if (rctEl) rctEl.innerText = `${Math.round(retailCartonTotal).toLocaleString()} د.ع`;
  const rcpEl = document.getElementById('ap-retailCartonProfit');
  if (rcpEl) rcpEl.innerText = `${retailCartonProfit >= 0 ? '+' : ''}${Math.round(retailCartonProfit).toLocaleString()} د.ع`;

  // 2. Wholesale calculations
  const wholesalePieceProfit = (wholesale > 0 && cost > 0) ? (wholesale - cost) : 0;
  const wholesaleCartonProfit = (wholesale > 0 && cost > 0 && itemsPerCarton > 0) ? ((wholesale - cost) * itemsPerCarton) : 0;

  const wppEl = document.getElementById('ap-wholesalePieceProfit');
  if (wppEl) wppEl.innerText = `${wholesalePieceProfit >= 0 ? '+' : ''}${Math.round(wholesalePieceProfit).toLocaleString()} د.ع`;
  const wcpEl = document.getElementById('ap-wholesaleCartonProfit');
  if (wcpEl) wcpEl.innerText = `${wholesaleCartonProfit >= 0 ? '+' : ''}${Math.round(wholesaleCartonProfit).toLocaleString()} د.ع`;

  // 3. Carton calculations
  const cartonDirectProfit = (cartonSelling > 0 && cartonPurchase > 0) ? (cartonSelling - cartonPurchase) : 0;
  const cdpEl = document.getElementById('ap-cartonDirectProfit');
  if (cdpEl) cdpEl.innerText = `${cartonDirectProfit >= 0 ? '+' : ''}${Math.round(cartonDirectProfit).toLocaleString()} د.ع`;
}

function recalcAddProductWeight() {
  const fardahCount = Number(document.getElementById('ap-fardahCount')?.value || 0);
  const kgPerFardah = Number(document.getElementById('ap-kgPerFardah')?.value || 0);
  const fardahPurchase = Number(document.getElementById('ap-fardahPurchase')?.value || 0);

  // Total weight in Kg
  const totalKg = fardahCount * kgPerFardah;
  const totalKgEl = document.getElementById('ap-totalKg');
  if (totalKgEl) {
    totalKgEl.value = (fardahCount > 0 && kgPerFardah > 0) ? `${totalKg} كغم` : '';
  }

  // Calculated Kg Cost from Fardah (Readonly)
  const kgCostFromFardah = kgPerFardah > 0 ? (fardahPurchase / kgPerFardah) : 0;
  const kgCostDisplayEl = document.getElementById('ap-kgCostFromFardah');
  if (kgCostDisplayEl) {
    kgCostDisplayEl.value = kgCostFromFardah > 0 ? `${Math.round(kgCostFromFardah).toLocaleString()} د.ع` : '';
  }

  const cost = Math.round(kgCostFromFardah);
  const costInput = document.getElementById('ap-cost');
  if (costInput) {
    costInput.value = cost;
  }
  const kgRetailPrice = Number(document.getElementById('ap-kgRetailPrice')?.value || 0); // بيع مفرد للكيلو

  // 1. Retail Kg calculations
  const retailKgProfit = (kgRetailPrice > 0 && cost > 0) ? (kgRetailPrice - cost) : 0;
  const retailFardahTotal = (kgRetailPrice > 0 && kgPerFardah > 0) ? (kgRetailPrice * kgPerFardah) : 0;
  const retailFardahProfit = (kgRetailPrice > 0 && cost > 0 && kgPerFardah > 0) ? ((kgRetailPrice - cost) * kgPerFardah) : 0;

  const rkpEl = document.getElementById('ap-retailKgProfit');
  if (rkpEl) rkpEl.innerText = `${retailKgProfit >= 0 ? '+' : ''}${Math.round(retailKgProfit).toLocaleString()} د.ع`;
  const rftEl = document.getElementById('ap-retailFardahTotal');
  if (rftEl) rftEl.innerText = `${Math.round(retailFardahTotal).toLocaleString()} د.ع`;
  const rfpEl = document.getElementById('ap-retailFardahProfit');
  if (rfpEl) rfpEl.innerText = `${retailFardahProfit >= 0 ? '+' : ''}${Math.round(retailFardahProfit).toLocaleString()} د.ع`;
}

function clearAddProductForm() {
  document.getElementById('ap-id').value = '';
  document.getElementById('ap-barcode').value = '';
  document.getElementById('ap-name').value = '';
  
  // Simple mode fields - empty and ready for typing
  const sQty = document.getElementById('ap-simpleStockQty');
  if (sQty) sQty.value = '';
  const sAlert = document.getElementById('ap-simpleMinAlert');
  if (sAlert) sAlert.value = '';
  const sCost = document.getElementById('ap-simpleCost');
  if (sCost) sCost.value = '';
  const sPrice = document.getElementById('ap-simplePrice');
  if (sPrice) sPrice.value = '';

  // Piece mode fields - empty and ready for typing
  document.getElementById('ap-cartonsCount').value = '';
  document.getElementById('ap-itemsPerCarton').value = '';
  document.getElementById('ap-totalStock').value = '';
  document.getElementById('ap-cartonPurchase').value = '';
  document.getElementById('ap-pieceCostFromCarton').value = '';
  document.getElementById('ap-price').value = '';
  document.getElementById('ap-wholesalePrice').value = '';
  document.getElementById('ap-cartonSelling').value = '';
  document.getElementById('ap-minStockAlert').value = '';

  // Weight mode fields - empty and ready for typing
  const fCount = document.getElementById('ap-fardahCount');
  if (fCount) fCount.value = '';
  const kgPerF = document.getElementById('ap-kgPerFardah');
  if (kgPerF) kgPerF.value = '';
  const totalKg = document.getElementById('ap-totalKg');
  if (totalKg) totalKg.value = '';
  const fPurchase = document.getElementById('ap-fardahPurchase');
  if (fPurchase) fPurchase.value = '';
  const kgCost = document.getElementById('ap-kgCostFromFardah');
  if (kgCost) kgCost.value = '';
  const kgRetail = document.getElementById('ap-kgRetailPrice');
  if (kgRetail) kgRetail.value = '';
  const minKg = document.getElementById('ap-minKgAlert');
  if (minKg) minKg.value = '';

  document.getElementById('ap-cost').value = '0';
  document.getElementById('addProductFormTitle').innerText = 'إضافة وتعديل مادة جديدة بالمخزن';
  
  recalcAddProductSimple();
  recalcAddProduct();
  recalcAddProductWeight();
}

async function saveProductFull() {
  const name = document.getElementById('ap-name')?.value.trim();
  if (!name) {
    alert('يرجى كتابة اسم المادة أولاً!');
    document.getElementById('ap-name')?.focus();
    return;
  }

  const barcode = document.getElementById('ap-barcode')?.value.trim();
  const category = document.getElementById('ap-categorySelect')?.value || 'عام';
  const supplierName = document.getElementById('ap-supplier')?.value || '';

  let payload = {};

  if (currentAddProductMode === 'simple') {
    const stockQty = Number(document.getElementById('ap-simpleStockQty')?.value || 0);
    const cost = Number(document.getElementById('ap-simpleCost')?.value || 0);
    const price = Number(document.getElementById('ap-simplePrice')?.value || 0);
    const minAlert = Number(document.getElementById('ap-simpleMinAlert')?.value || 5);

    payload = {
      id: document.getElementById('ap-id')?.value || undefined,
      name: name,
      barcode: barcode || undefined,
      category: category,
      supplierName: supplierName,
      unit: "قطعة",
      cost: cost,
      price: price,
      wholesalePrice: 0,
      cartonPurchasePrice: 0,
      cartonSellingPrice: 0,
      stockQuantity: stockQty,
      cartonsCount: 0,
      piecesPerCarton: 1,
      minStockAlert: minAlert
    };
  } else if (currentAddProductMode === 'weight') {
    const fardahCount = Math.max(0, Number(document.getElementById('ap-fardahCount')?.value || 0));
    const kgPerFardah = Math.max(1, Number(document.getElementById('ap-kgPerFardah')?.value || 1));
    const totalKg = fardahCount * (Number(document.getElementById('ap-kgPerFardah')?.value) || 0);
    const fardahPurchase = Number(document.getElementById('ap-fardahPurchase')?.value || 0);
    const cost = Math.round(Number(document.getElementById('ap-cost')?.value || 0));
    const price = Number(document.getElementById('ap-kgRetailPrice')?.value || 0);
    const minAlert = Number(document.getElementById('ap-minKgAlert')?.value || 5);

    payload = {
      id: document.getElementById('ap-id')?.value || undefined,
      name: name,
      barcode: barcode || undefined,
      category: category,
      supplierName: supplierName,
      unit: "كيلو",
      cost: cost,
      price: price,
      wholesalePrice: 0,
      cartonPurchasePrice: fardahPurchase,
      cartonSellingPrice: 0,
      stockQuantity: totalKg,
      cartonsCount: fardahCount,
      piecesPerCarton: kgPerFardah,
      minStockAlert: minAlert
    };
  } else {
    const itemsPerCarton = Math.max(1, Number(document.getElementById('ap-itemsPerCarton')?.value || 1));
    const cartonsCount = Math.max(0, Number(document.getElementById('ap-cartonsCount')?.value || 0));
    const totalStock = itemsPerCarton * cartonsCount;
    const cartonPurchase = Number(document.getElementById('ap-cartonPurchase')?.value || 0);
    const cartonSelling = Number(document.getElementById('ap-cartonSelling')?.value || 0);
    const cost = Number(document.getElementById('ap-cost')?.value || 0);
    const price = Number(document.getElementById('ap-price')?.value || 0);
    const wholesalePrice = Number(document.getElementById('ap-wholesalePrice')?.value || 0);
    const minAlert = Number(document.getElementById('ap-minStockAlert')?.value || 5);

    payload = {
      id: document.getElementById('ap-id')?.value || undefined,
      name: name,
      barcode: barcode || undefined,
      category: category,
      supplierName: supplierName,
      unit: "قطعة",
      cost: cost,
      price: price,
      wholesalePrice: wholesalePrice,
      cartonPurchasePrice: cartonPurchase,
      cartonSellingPrice: cartonSelling,
      stockQuantity: totalStock,
      cartonsCount: cartonsCount,
      piecesPerCarton: itemsPerCarton,
      minStockAlert: minAlert
    };
  }

  const res = await callBackend('save_product', payload);
  if (res && res.success) {
    const modeLabel = currentAddProductMode === 'simple' ? 'بالعدد المفرد' : (currentAddProductMode === 'weight' ? 'بالوزن والكيلو' : 'بالكرتون والتعبئة');
    alert(`✔ تم حفظ المادة (${modeLabel}) بنجاح في قاعدة البيانات!`);
    clearAddProductForm();
    await loadProducts();
    await loadInventory();
    switchTab('inventory');
  }
}

// ========================================================
// INVENTORY & WAREHOUSE MANAGEMENT (FULL DETAILS)
// ========================================================
let inventoryData = [];
let invFilteredData = [];

async function loadInventory() {
  const res = await callBackend('get_inventory');
  if (!res || !res.success) return;

  inventoryData = res.products || [];

  // 1. Update KPI Summary Cards
  const totalProdsEl = document.getElementById('invTotalProducts');
  if (totalProdsEl) totalProdsEl.innerText = `${inventoryData.length.toLocaleString()} مادة`;

  const totalCostEl = document.getElementById('invTotalCostValue');
  if (totalCostEl) totalCostEl.innerText = `${Number(res.totalCostValue || 0).toLocaleString()} د.ع`;

  const totalSellEl = document.getElementById('invTotalSellingValue');
  if (totalSellEl) totalSellEl.innerText = `${Number(res.totalSellingValue || 0).toLocaleString()} د.ع`;

  const totalProfitEl = document.getElementById('invTotalProfitValue');
  if (totalProfitEl) totalProfitEl.innerText = `+${Number(res.expectedProfit || 0).toLocaleString()} د.ع`;

  // 2. Update Categories Filter Dropdown
  const catFilter = document.getElementById('invCategoryFilter');
  if (catFilter) {
    const cats = [...new Set(inventoryData.map(p => p.category).filter(Boolean))];
    const currentVal = catFilter.value;
    catFilter.innerHTML = '<option value="">جميع التصنيفات</option>';
    cats.forEach(c => {
      catFilter.innerHTML += `<option value="${c}">${c}</option>`;
    });
    if (cats.includes(currentVal)) catFilter.value = currentVal;
  }

  filterInventoryTable();
}

function filterInventoryTable() {
  const query = (document.getElementById('invSearchInput')?.value || '').toLowerCase().trim();
  const selCat = document.getElementById('invCategoryFilter')?.value || '';
  const lowStockOnly = document.getElementById('invLowStockOnly')?.checked || false;

  invFilteredData = inventoryData.filter(p => {
    const pName = (p.name || p.Name || '').toLowerCase();
    const pBar = (p.barcode || p.Barcode || '').toLowerCase();
    const pCat = (p.category || p.Category || '').toLowerCase();
    const pSup = (p.supplierName || p.SupplierName || '').toLowerCase();

    const matchesSearch = !query || 
      pName.includes(query) || 
      pBar.includes(query) ||
      pCat.includes(query) ||
      pSup.includes(query);
    
    const matchesCat = !selCat || (p.category === selCat || p.Category === selCat);
    const matchesLowStock = !lowStockOnly || (p.stockQuantity <= (p.minStockAlert || 5));

    return matchesSearch && matchesCat && matchesLowStock;
  });

  renderInventoryTable();
}

function renderInventoryTable() {
  const tbody = document.getElementById('inventoryTableBody');
  const summaryEl = document.getElementById('invCountSummary');
  const showAllBtn = document.getElementById('invShowAllBtn');
  if (!tbody) return;

  if (invFilteredData.length === 0) {
    tbody.innerHTML = `<tr><td colspan="10" class="text-center py-12 text-slate-400 font-bold">لا توجد مواد مطابقة للبحث أو الفلترة</td></tr>`;
    if (summaryEl) summaryEl.innerText = 'يتم عرض 0 مادة';
    if (showAllBtn) showAllBtn.classList.add('hidden');
    return;
  }

  const limitVal = document.getElementById('invDisplayLimit')?.value || '1000';
  let limit = invFilteredData.length;
  if (limitVal !== 'all') {
    limit = parseInt(limitVal, 10) || 1000;
  }

  const displayItems = invFilteredData.slice(0, limit);

  let rowsHtml = '';
  for (let i = 0; i < displayItems.length; i++) {
    const p = displayItems[i];
    const isLow = p.stockQuantity <= (p.minStockAlert || 5);
    const isOutOfStock = p.stockQuantity <= 0;
    const rProfit = (p.price || 0) - (p.cost || 0);
    const wProfit = (p.wholesalePrice || 0) - (p.cost || 0);
    const cProfit = (p.cartonSellingPrice || 0) - (p.cartonPurchasePrice || 0);
    const displayName = p.name || p.Name || 'مادة بدون اسم';
    const displayBarcode = p.barcode || p.Barcode || 'بدون باركود';

    rowsHtml += `
      <tr class="hover:bg-slate-50 dark:hover:bg-slate-800/40 transition cursor-pointer" onclick="handleInventoryRowClick(event, '${p.id || p.Id}')">
        <td class="p-2.5 text-center text-slate-400 font-bold">${i + 1}</td>
        <td class="p-2.5">
          <div class="font-black text-slate-800 dark:text-white text-xs">${displayName}</div>
          <div class="text-[10px] font-mono text-sky-500 font-bold flex items-center gap-1">
            <span>🏷</span><span>${displayBarcode}</span>
          </div>
        </td>
        <td class="p-2.5">
          <span class="inline-block px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-800 text-[10px] font-bold text-slate-600 dark:text-slate-300 mb-0.5">${p.category || 'عام'}</span>
          ${p.supplierName ? `<div class="text-[10px] text-slate-400 font-semibold">🏢 ${p.supplierName}</div>` : ''}
        </td>
        <td class="p-2.5 text-center">
          <div class="font-bold text-slate-700 dark:text-slate-200 text-xs">
            ${p.cartonsCount || 0} ${p.unit === 'كيلو' || p.unit === 'كغم' ? 'فردة' : 'كرتون'}
          </div>
          <div class="text-[10px] text-slate-400 font-semibold">
            (${p.piecesPerCarton || 1} ${p.unit === 'كيلو' || p.unit === 'كغم' ? 'كغم/فردة' : 'قطعة/كرتون'})
          </div>
        </td>
        <td class="p-2.5 text-center">
          <div class="font-black text-xs ${isOutOfStock ? 'text-rose-500' : isLow ? 'text-amber-500' : 'text-slate-800 dark:text-white'}">
            ${p.stockQuantity} ${p.unit === 'كيلو' || p.unit === 'كغم' ? 'كغم' : 'قطعة'}
          </div>
        </td>
        <td class="p-2.5 text-center">
          <div class="font-black text-blue-600 dark:text-blue-400 text-xs">${Number(p.cost).toLocaleString()} د.ع</div>
          ${p.cartonPurchasePrice > 0 ? `<div class="text-[10px] text-slate-400 font-semibold">${p.unit === 'كيلو' || p.unit === 'كغم' ? 'شراء فردة' : 'شراء كرتون'}: ${Number(p.cartonPurchasePrice).toLocaleString()} د.ع</div>` : ''}
        </td>
        <td class="p-2.5 text-center space-y-0.5">
          <div class="text-xs font-black text-emerald-600 dark:text-emerald-400">مفرد: ${Number(p.price).toLocaleString()} د.ع</div>
          <div class="text-[10px] font-bold text-sky-600 dark:text-sky-400">جملة: ${Number(p.wholesalePrice || 0).toLocaleString()} د.ع</div>
          ${p.cartonSellingPrice > 0 ? `<div class="text-[10px] font-bold text-purple-600 dark:text-purple-400">${p.unit === 'كيلو' || p.unit === 'كغم' ? 'فردة' : 'كرتون'}: ${Number(p.cartonSellingPrice).toLocaleString()} د.ع</div>` : ''}
        </td>
        <td class="p-2.5 text-center space-y-0.5">
          <div class="text-[10px] font-black text-emerald-600 dark:text-emerald-400">ربح مفرد: +${Number(rProfit).toLocaleString()} د.ع</div>
          <div class="text-[10px] font-bold text-sky-600 dark:text-sky-400">ربح جملة: +${Number(wProfit).toLocaleString()} د.ع</div>
          ${cProfit > 0 ? `<div class="text-[10px] font-bold text-purple-600 dark:text-purple-400">${p.unit === 'كيلو' || p.unit === 'كغم' ? 'ربح فردة' : 'ربح كرتون'}: +${Number(cProfit).toLocaleString()} د.ع</div>` : ''}
        </td>
        <td class="p-2.5 text-center">
          ${isOutOfStock 
            ? `<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-rose-100 text-rose-700 dark:bg-rose-950/60 dark:text-rose-400">نفذ الرصيد</span>` 
            : isLow 
            ? `<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-amber-100 text-amber-700 dark:bg-amber-950/60 dark:text-amber-400">نواقص (${p.stockQuantity})</span>` 
            : `<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-emerald-100 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400">متوفر ✔</span>`}
        </td>
        <td class="p-2.5 text-center">
          <div class="flex items-center justify-center gap-1.5">
            <button onclick="openProductDetailModal('${p.id || p.Id}')" class="p-1.5 bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-300 rounded-lg text-xs" title="عرض التفاصيل الكاملة">👁</button>
            <button onclick="editProductFromInventory('${p.id || p.Id}')" class="p-1.5 bg-sky-100 hover:bg-sky-200 dark:bg-sky-950/60 text-sky-600 dark:text-sky-400 rounded-lg text-xs font-bold" title="تعديل">✏</button>
            <button onclick="deleteProductFromInventory('${p.id || p.Id}')" class="p-1.5 bg-rose-100 hover:bg-rose-200 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 rounded-lg text-xs font-bold" title="حذف">🗑</button>
          </div>
        </td>
      </tr>
    `;
  }

  tbody.innerHTML = rowsHtml;

  if (summaryEl) {
    summaryEl.innerText = `يتم عرض ${displayItems.length.toLocaleString()} مادة من إجمالي ${invFilteredData.length.toLocaleString()} مادة`;
  }

  if (showAllBtn) {
    if (invFilteredData.length > displayItems.length) {
      showAllBtn.innerText = `👁 عرض كافة المواد (${invFilteredData.length.toLocaleString()} مادة) دفعة واحدة`;
      showAllBtn.classList.remove('hidden');
    } else {
      showAllBtn.classList.add('hidden');
    }
  }
}

function showAllInventoryItems() {
  const limitSelect = document.getElementById('invDisplayLimit');
  if (limitSelect) limitSelect.value = 'all';
  renderInventoryTable();
}

function handleInventoryRowClick(event, id) {
  if (event.target.tagName !== 'BUTTON' && !event.target.closest('button')) {
    openProductDetailModal(id);
  }
}

function openProductDetailModal(id) {
  const prod = inventoryData.find(p => p.id === id);
  if (!prod) return;

  document.getElementById('pdm-title').innerText = prod.name;
  
  const content = document.getElementById('pdm-content');
  if (content) {
    const rProfit = (prod.price || 0) - (prod.cost || 0);
    const rCartonProfit = rProfit * (prod.piecesPerCarton || 1);
    const wProfit = (prod.wholesalePrice || 0) - (prod.cost || 0);
    const cProfit = (prod.cartonSellingPrice || 0) - (prod.cartonPurchasePrice || 0);

    content.innerHTML = `
      <div class="grid grid-cols-2 gap-2 p-3 bg-slate-50 dark:bg-slate-900 rounded-2xl">
        <div><span class="text-slate-400 block text-[10px]">الباركود:</span><span class="font-mono font-bold text-sky-500">${prod.barcode || 'بدون باركود'}</span></div>
        <div><span class="text-slate-400 block text-[10px]">التصنيف:</span><span class="font-bold">${prod.category || 'عام'}</span></div>
        <div><span class="text-slate-400 block text-[10px]">المندوب / الشركة:</span><span class="font-bold">${prod.supplierName || 'بدون مندوب'}</span></div>
        <div><span class="text-slate-400 block text-[10px]">تاريخ التسجيل:</span><span class="font-bold text-slate-500">${prod.createdAt || '--'}</span></div>
      </div>

      <div class="grid grid-cols-3 gap-2 p-3 bg-sky-50/60 dark:bg-sky-950/30 rounded-2xl border border-sky-500/20 text-center">
        <div><span class="text-slate-400 block text-[10px]">عدد الكراتين:</span><span class="font-black text-sky-600">${prod.cartonsCount || 0} كرتون</span></div>
        <div><span class="text-slate-400 block text-[10px]">المواد بالكرتون:</span><span class="font-black text-sky-600">${prod.piecesPerCarton || 1} قطعة</span></div>
        <div><span class="text-slate-400 block text-[10px]">إجمالي الرصيد:</span><span class="font-black text-emerald-600 text-sm">${prod.stockQuantity} قطعة</span></div>
      </div>

      <div class="space-y-1.5 p-3 bg-emerald-50/60 dark:bg-emerald-950/30 rounded-2xl border border-emerald-500/20">
        <div class="flex justify-between font-bold"><span>تكلفة القطعة المعتمدة:</span><span class="text-blue-600 font-black">${Number(prod.cost).toLocaleString()} د.ع</span></div>
        <div class="flex justify-between font-bold"><span>سعر بيع المفرد:</span><span class="text-emerald-600 font-black">${Number(prod.price).toLocaleString()} د.ع</span></div>
        <div class="flex justify-between font-bold"><span>ربح القطعة المفردة:</span><span class="text-emerald-600 font-black">+${Number(rProfit).toLocaleString()} د.ع</span></div>
        <div class="flex justify-between font-bold"><span>ربح الكرتون كاملاً بالمفرد:</span><span class="text-emerald-600 font-black">+${Number(rCartonProfit).toLocaleString()} د.ع</span></div>
      </div>

      <div class="space-y-1.5 p-3 bg-purple-50/60 dark:bg-purple-950/30 rounded-2xl border border-purple-500/20">
        <div class="flex justify-between font-bold"><span>سعر شراء الكرتون:</span><span class="text-slate-700 dark:text-slate-200">${Number(prod.cartonPurchasePrice || 0).toLocaleString()} د.ع</span></div>
        <div class="flex justify-between font-bold"><span>سعر بيع الكرتون كاملاً:</span><span class="text-purple-600 font-black">${Number(prod.cartonSellingPrice || 0).toLocaleString()} د.ع</span></div>
        <div class="flex justify-between font-bold"><span>ربح بيع الكرتون:</span><span class="text-purple-600 font-black">+${Number(cProfit).toLocaleString()} د.ع</span></div>
      </div>
    `;
  }

  const editBtn = document.getElementById('pdm-editBtn');
  if (editBtn) {
    editBtn.onclick = () => {
      closeProductDetailModal();
      editProductFromInventory(prod.id);
    };
  }

  document.getElementById('productDetailModal')?.classList.remove('hidden');
}

function closeProductDetailModal() {
  document.getElementById('productDetailModal')?.classList.add('hidden');
}

async function deleteProductFromInventory(id) {
  const prod = inventoryData.find(p => p.id === id);
  const name = prod ? prod.name : 'هذه المادة';
  if (confirm(`هل أنت متأكد من حذف: "${name}" من المخزن؟`)) {
    const res = await callBackend('delete_product', { id });
    if (res && res.success) {
      await loadProducts();
      await loadInventory();
    }
  }
}

function editProductFromInventory(id) {
  const prod = inventoryData.find(p => p.id === id) || state.products.find(p => p.id === id);
  if (prod) {
    switchTab('addProduct');
    document.getElementById('ap-id').value = prod.id;
    document.getElementById('ap-name').value = prod.name;
    document.getElementById('ap-barcode').value = prod.barcode || '';
    document.getElementById('ap-categorySelect').value = prod.category || 'عام';
    document.getElementById('ap-supplier').value = prod.supplierName || '';
    document.getElementById('ap-cost').value = prod.cost;

    const isWeight = (prod.unit === 'كيلو' || prod.unit === 'كغم');

    if (isWeight) {
      switchAddProductMode('weight');
      const fCount = document.getElementById('ap-fardahCount');
      if (fCount) fCount.value = prod.cartonsCount || Math.floor((prod.stockQuantity || 0) / (prod.piecesPerCarton || 1));
      const kgPerF = document.getElementById('ap-kgPerFardah');
      if (kgPerF) kgPerF.value = prod.piecesPerCarton || '';
      const fPurchase = document.getElementById('ap-fardahPurchase');
      if (fPurchase) fPurchase.value = prod.cartonPurchasePrice || '';
      const kgRetail = document.getElementById('ap-kgRetailPrice');
      if (kgRetail) kgRetail.value = prod.price || '';
      const minKg = document.getElementById('ap-minKgAlert');
      if (minKg) minKg.value = prod.minStockAlert || 5;
      recalcAddProductWeight();
    } else if ((prod.cartonsCount > 0 || prod.cartonPurchasePrice > 0 || prod.cartonSellingPrice > 0) && (prod.piecesPerCarton && prod.piecesPerCarton > 1)) {
      switchAddProductMode('piece');
      document.getElementById('ap-price').value = prod.price || '';
      document.getElementById('ap-wholesalePrice').value = prod.wholesalePrice || '';
      document.getElementById('ap-cartonPurchase').value = prod.cartonPurchasePrice || '';
      document.getElementById('ap-cartonSelling').value = prod.cartonSellingPrice || '';
      document.getElementById('ap-itemsPerCarton').value = prod.piecesPerCarton || '';
      document.getElementById('ap-cartonsCount').value = prod.cartonsCount || Math.floor((prod.stockQuantity || 0) / (prod.piecesPerCarton || 1));
      document.getElementById('ap-minStockAlert').value = prod.minStockAlert || 5;
      recalcAddProduct();
    } else {
      switchAddProductMode('simple');
      const sQty = document.getElementById('ap-simpleStockQty');
      if (sQty) sQty.value = prod.stockQuantity || '';
      const sAlert = document.getElementById('ap-simpleMinAlert');
      if (sAlert) sAlert.value = prod.minStockAlert || 5;
      const sCost = document.getElementById('ap-simpleCost');
      if (sCost) sCost.value = prod.cost || '';
      const sPrice = document.getElementById('ap-simplePrice');
      if (sPrice) sPrice.value = prod.price || '';
      recalcAddProductSimple();
    }

    document.getElementById('addProductFormTitle').innerText = 'تعديل بيانات المادة';
  }
}

let suppliersData = [];

async function loadSuppliers() {
  const res = await callBackend('get_suppliers');
  if (!res || !res.success) return;

  suppliersData = res.suppliers || [];

  // Update KPIs
  const totalCountEl = document.getElementById('sup-totalCount');
  if (totalCountEl) totalCountEl.innerText = `${suppliersData.length} مندوب / شركة`;

  const totalDebts = suppliersData.reduce((sum, s) => sum + Number(s.balance || 0), 0);
  const totalDebtsEl = document.getElementById('sup-totalDebts');
  if (totalDebtsEl) totalDebtsEl.innerText = `${Math.round(totalDebts).toLocaleString()} د.ع`;

  renderSuppliersGrid(suppliersData);
}

function filterSuppliersGrid() {
  const query = (document.getElementById('sup-searchInput')?.value || '').trim().toLowerCase();
  if (!query) {
    renderSuppliersGrid(suppliersData);
    return;
  }

  const filtered = suppliersData.filter(s => 
    (s.name && s.name.toLowerCase().includes(query)) ||
    (s.company && s.company.toLowerCase().includes(query)) ||
    (s.phone && s.phone.toLowerCase().includes(query)) ||
    (s.address && s.address.toLowerCase().includes(query))
  );

  renderSuppliersGrid(filtered);
}

function renderSuppliersGrid(list) {
  const grid = document.getElementById('suppliersCardsGrid');
  if (!grid) return;

  grid.innerHTML = '';

  if (!list || list.length === 0) {
    grid.innerHTML = `
      <div class="col-span-3 sh-card p-12 text-center space-y-3">
        <div class="w-16 h-16 mx-auto rounded-3xl bg-sky-500/10 text-sky-500 flex items-center justify-center text-3xl font-bold">
          🤝
        </div>
        <h4 class="font-black text-base text-slate-800 dark:text-white">لا توجد حسابات مناديب أو شركات مسجلة</h4>
        <p class="text-xs text-slate-400 max-w-sm mx-auto">يمكنك البدء بإنشاء حساب للمندوب أو الشركة الموردة لتنظيم الفواتير والدفعات والمستحقات بدقة</p>
        <button onclick="openCreateSupplierModal()" class="px-6 py-2.5 bg-sky-600 hover:bg-sky-500 text-white font-black text-xs rounded-xl shadow-md inline-flex items-center gap-2 transition">
          <span>➕</span>
          <span>إنشاء أول حساب مندوب الآن</span>
        </button>
      </div>
    `;
    return;
  }

  list.forEach(s => {
    const card = document.createElement('div');
    card.className = 'sh-card p-4 flex flex-col justify-between hover:shadow-lg transition border border-slate-200 dark:border-slate-800 hover:border-sky-400 dark:hover:border-sky-600 space-y-3';
    
    const sId = s.id || s.Id;
    const sName = s.name || s.Name || 'مندوب';
    const sCompany = s.company || s.Company || 'شركة عامة';
    const sPhone = s.phone || s.Phone || '';
    const sAddress = s.address || s.Address || '';
    const sNotes = s.notes || s.Notes || '';
    const sProductsCount = s.productsCount || s.ProductsCount || 0;
    const balanceNum = Number(s.balance ?? s.Balance ?? 0);
    const balanceColor = balanceNum > 0 ? 'text-amber-500 font-black' : (balanceNum < 0 ? 'text-emerald-500 font-black' : 'text-slate-500 font-bold');

    card.innerHTML = `
      <div class="space-y-2.5">
        <div class="flex items-start justify-between">
          <div class="flex items-center gap-2.5">
            <div class="w-10 h-10 rounded-2xl bg-sky-500/10 text-sky-600 dark:text-sky-400 flex items-center justify-center font-black text-sm">
              ${sName[0] || 'م'}
            </div>
            <div>
              <h4 class="font-black text-sm text-slate-900 dark:text-white">${sName}</h4>
              <span class="text-[10px] text-sky-600 dark:text-sky-400 font-bold">${sCompany}</span>
            </div>
          </div>
          <button onclick="openSupplierProductsModal('${sId}')" class="text-[10px] bg-slate-100 dark:bg-slate-800 hover:bg-sky-50 hover:text-sky-600 dark:hover:bg-sky-950/60 dark:hover:text-sky-400 text-slate-600 dark:text-slate-300 px-2.5 py-1 rounded-xl font-bold flex items-center gap-1 transition" title="عرض كافة المواد التابعة لهذا المندوب">
            <span>📦</span>
            <span>${sProductsCount} مادة</span>
          </button>
        </div>

        <div class="space-y-1 text-xs text-slate-600 dark:text-slate-400 pt-1">
          <div class="flex items-center justify-between text-[11px]">
            <span class="text-slate-400 font-semibold">📞 الهاتف:</span>
            <span class="font-mono font-bold text-slate-800 dark:text-slate-200">${sPhone || 'غير مسجل'}</span>
          </div>
          ${sAddress ? `
            <div class="flex items-center justify-between text-[11px]">
              <span class="text-slate-400 font-semibold">📍 العنوان:</span>
              <span class="font-bold text-slate-800 dark:text-slate-200 truncate max-w-[150px]">${sAddress}</span>
            </div>
          ` : ''}
          ${sNotes ? `
            <p class="text-[10px] text-slate-400 italic bg-slate-50 dark:bg-slate-900/50 p-1.5 rounded-lg">${sNotes}</p>
          ` : ''}
        </div>
      </div>

      <div class="pt-2 border-t border-slate-100 dark:border-slate-800 space-y-2">
        <div class="flex items-center justify-between bg-slate-50 dark:bg-slate-800/50 p-2 rounded-xl">
          <span class="text-[11px] text-slate-500 dark:text-slate-400 font-bold">المستحق بذمة الماركت:</span>
          <span class="text-sm font-mono ${balanceColor}">${Math.round(balanceNum).toLocaleString()} د.ع</span>
        </div>

        <!-- Primary Actions: Buy Products & Statement -->
        <div class="grid grid-cols-2 gap-1.5">
          <button onclick="openPurchaseFromSupplierModal('${sId}')" class="py-2 px-2 bg-gradient-to-r from-sky-600 to-blue-600 hover:from-sky-500 hover:to-blue-500 text-white rounded-xl text-xs font-black flex items-center justify-center gap-1.5 shadow-sm transition">
            <span>🛒</span>
            <span>شراء بضاعة (وصل)</span>
          </button>
          <button onclick="openSupplierStatementModal('${sId}')" class="py-2 px-2 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200 rounded-xl text-xs font-black flex items-center justify-center gap-1.5 transition">
            <span>📄</span>
            <span>كشف حساب ووصولات</span>
          </button>
        </div>

        <!-- Secondary Actions: Pay, Products, Edit, Delete -->
        <div class="grid grid-cols-4 gap-1 pt-0.5">
          <button onclick="openSupplierPaymentModal('${sId}')" class="py-1 px-1.5 bg-emerald-50 dark:bg-emerald-950/40 hover:bg-emerald-100 dark:hover:bg-emerald-900 text-emerald-700 dark:text-emerald-300 rounded-lg text-[10px] font-bold flex items-center justify-center gap-0.5 transition" title="تسديد دفعة">
            <span>💵</span>
            <span>تسديد</span>
          </button>
          <button onclick="openSupplierProductsModal('${sId}')" class="py-1 px-1.5 bg-sky-50 dark:bg-sky-950/40 hover:bg-sky-100 dark:hover:bg-sky-900 text-sky-700 dark:text-sky-300 rounded-lg text-[10px] font-bold flex items-center justify-center gap-0.5 transition" title="عرض المواد">
            <span>📦</span>
            <span>المواد</span>
          </button>
          <button onclick="openEditSupplierModal('${sId}')" class="py-1 px-1.5 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-300 rounded-lg text-[10px] font-bold flex items-center justify-center gap-0.5 transition" title="تعديل">
            <span>✏️</span>
            <span>تعديل</span>
          </button>
          <button onclick="deleteSupplierAccount('${sId}')" class="py-1 px-1.5 bg-rose-50 dark:bg-rose-950/40 hover:bg-rose-100 dark:hover:bg-rose-900 text-rose-600 dark:text-rose-400 rounded-lg text-[10px] font-bold flex items-center justify-center gap-0.5 transition" title="حذف">
            <span>🗑️</span>
            <span>حذف</span>
          </button>
        </div>
      </div>
    `;
    grid.appendChild(card);
  });
}

function openCreateSupplierModal() {
  document.getElementById('sup-id').value = '';
  document.getElementById('sup-formName').value = '';
  document.getElementById('sup-formCompany').value = '';
  document.getElementById('sup-formPhone').value = '';
  document.getElementById('sup-formBalance').value = '';
  document.getElementById('sup-formAddress').value = '';
  document.getElementById('sup-formNotes').value = '';
  document.getElementById('supModalTitle').innerText = 'إنشاء حساب مندوب أو شركة جديدة';
  document.getElementById('supplierAccountModal')?.classList.remove('hidden');
  document.getElementById('sup-formName')?.focus();
}

function openEditSupplierModal(id) {
  const sup = suppliersData.find(s => (s.id === id || s.Id === id));
  if (!sup) return;

  document.getElementById('sup-id').value = sup.id || sup.Id;
  document.getElementById('sup-formName').value = sup.name || sup.Name || '';
  document.getElementById('sup-formCompany').value = sup.company || sup.Company || '';
  document.getElementById('sup-formPhone').value = sup.phone || sup.Phone || '';
  document.getElementById('sup-formBalance').value = sup.balance ?? sup.Balance ?? 0;
  document.getElementById('sup-formAddress').value = sup.address || sup.Address || '';
  document.getElementById('sup-formNotes').value = sup.notes || sup.Notes || '';
  document.getElementById('supModalTitle').innerText = 'تعديل بيانات حساب المندوب / الشركة';
  document.getElementById('supplierAccountModal')?.classList.remove('hidden');
}

function closeSupplierModal() {
  document.getElementById('supplierAccountModal')?.classList.add('hidden');
}

async function saveSupplierAccount() {
  const name = document.getElementById('sup-formName')?.value.trim();
  if (!name) {
    alert('يرجى كتابة اسم المندوب أو الشخص المسؤول أولاً!');
    document.getElementById('sup-formName')?.focus();
    return;
  }

  const id = document.getElementById('sup-id')?.value || undefined;
  const company = document.getElementById('sup-formCompany')?.value.trim() || undefined;
  const phone = document.getElementById('sup-formPhone')?.value.trim() || undefined;
  const balance = Number(document.getElementById('sup-formBalance')?.value || 0);
  const address = document.getElementById('sup-formAddress')?.value.trim() || undefined;
  const notes = document.getElementById('sup-formNotes')?.value.trim() || undefined;

  const payload = {
    id,
    name,
    company,
    phone,
    balance,
    address,
    notes
  };

  const res = await callBackend('save_supplier', payload);
  if (res && res.success) {
    alert(`✔ تم حفظ حساب المندوب (${name}) بنجاح!`);
    closeSupplierModal();
    await loadSuppliers();
  }
}

async function deleteSupplierAccount(id) {
  const sup = suppliersData.find(s => (s.id === id || s.Id === id));
  const name = sup ? (sup.name || sup.Name) : 'هذا الحساب';

  if (confirm(`هل أنت متأكد من رغبتك في حذف حساب: "${name}"؟`)) {
    const res = await callBackend('delete_supplier', { id });
    if (res && res.success) {
      alert(`✔ تم حذف الحساب بنجاح!`);
      await loadSuppliers();
    }
  }
}

function openSupplierPaymentModal(id) {
  const sup = suppliersData.find(s => (s.id === id || s.Id === id));
  if (!sup) return;

  const sName = sup.name || sup.Name;
  const sCompany = sup.company || sup.Company;
  const sBal = sup.balance ?? sup.Balance ?? 0;

  document.getElementById('sp-supId').value = sup.id || sup.Id;
  document.getElementById('sp-supName').innerText = `المندوب: ${sName} (${sCompany || 'عام'})`;
  document.getElementById('sp-currentBalance').innerText = `${Math.round(Number(sBal)).toLocaleString()} د.ع`;
  document.getElementById('sp-amount').value = '';
  document.getElementById('sp-receiptNo').value = '';
  document.getElementById('sp-notes').value = '';

  document.getElementById('supplierPaymentModal')?.classList.remove('hidden');
  document.getElementById('sp-amount')?.focus();
}

function closeSupplierPaymentModal() {
  document.getElementById('supplierPaymentModal')?.classList.add('hidden');
}

async function submitSupplierPayment() {
  const supId = document.getElementById('sp-supId')?.value;
  const amount = Number(document.getElementById('sp-amount')?.value || 0);
  if (amount <= 0) {
    alert('يرجى كتابة مبلغ التسديد بشكل صحيح (أكبر من 0)!');
    document.getElementById('sp-amount')?.focus();
    return;
  }

  const receiptNumber = document.getElementById('sp-receiptNo')?.value.trim();
  const notes = document.getElementById('sp-notes')?.value.trim();

  const payload = {
    supplierId: supId,
    amount: amount,
    receiptNumber: receiptNumber || undefined,
    notes: notes || undefined
  };

  const res = await callBackend('add_supplier_payment', payload);
  if (res && res.success) {
    alert(`✔ تم تسجيل وتأكيد تسديد الدفعة بقيمة ${amount.toLocaleString()} د.ع بنجاح!`);
    closeSupplierPaymentModal();
    await loadSuppliers();
  }
}

// ========================================================
// SUPPLIER PRODUCTS (مواد المندوب المسجلة)
// ========================================================
let currentSupplierProducts = [];

async function openSupplierProductsModal(id) {
  const sup = suppliersData.find(s => (s.id === id || s.Id === id));
  if (!sup) return;

  const sId = sup.id || sup.Id;
  const sName = sup.name || sup.Name;
  const sCompany = sup.company || sup.Company;

  document.getElementById('spm-supName').innerText = `مواد المندوب: ${sName}`;
  document.getElementById('spm-supCompany').innerText = `الشركة: ${sCompany || 'عام'}`;

  const addNewBtn = document.getElementById('spm-addNewProductBtn');
  if (addNewBtn) {
    addNewBtn.onclick = () => {
      closeSupplierProductsModal();
      switchTab('addProduct');
      setTimeout(() => {
        const supInput = document.getElementById('ap-supplier');
        if (supInput) supInput.value = sName;
      }, 100);
    };
  }

  const tbody = document.getElementById('supplierProductsTableBody');
  if (tbody) tbody.innerHTML = `<tr><td colspan="6" class="text-center py-6 text-slate-400">جاري تحميل مواد المندوب...</td></tr>`;

  document.getElementById('supplierProductsModal')?.classList.remove('hidden');

  const res = await callBackend('get_supplier_products', { supplierId: sId });
  if (res && res.success && res.products) {
    currentSupplierProducts = res.products;
    const countEl = document.getElementById('spm-productsCount');
    if (countEl) countEl.innerText = `${currentSupplierProducts.length} مادة مسجلة`;

    if (currentSupplierProducts.length === 0) {
      tbody.innerHTML = `<tr><td colspan="6" class="text-center py-6 text-slate-400">لا توجد مواد مسجلة لهذا المندوب بعد</td></tr>`;
      return;
    }

    tbody.innerHTML = '';
    currentSupplierProducts.forEach(p => {
      const pCost = Number(p.cost || p.Cost || 0);
      const pPrice = Number(p.price || p.Price || 0);
      const pStock = Number(p.stockQuantity || p.StockQuantity || 0);
      const pProfit = Math.max(0, pPrice - pCost);

      const row = document.createElement('tr');
      row.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/50 transition';
      row.innerHTML = `
        <td class="p-2.5 font-mono text-slate-500">${p.barcode || p.Barcode || '--'}</td>
        <td class="p-2.5 font-bold text-slate-900 dark:text-white">${p.name || p.Name}</td>
        <td class="p-2.5 text-center font-bold font-mono text-sky-600 dark:text-sky-400">${pStock} ${p.unit || 'قطعة'}</td>
        <td class="p-2.5 text-center font-mono text-slate-700 dark:text-slate-300">${pCost.toLocaleString()} د.ع</td>
        <td class="p-2.5 text-center font-mono text-slate-700 dark:text-slate-300">${pPrice.toLocaleString()} د.ع</td>
        <td class="p-2.5 text-center font-mono font-bold text-emerald-600 dark:text-emerald-400">${pProfit.toLocaleString()} د.ع</td>
      `;
      tbody.appendChild(row);
    });
  }
}

function closeSupplierProductsModal() {
  document.getElementById('supplierProductsModal')?.classList.add('hidden');
}

// ========================================================
// PURCHASE FROM SUPPLIER & OFFICIAL INVOICE RECEIPT
// ========================================================
let pfsItems = [];
let pfsCurrentSupplierId = null;

async function openPurchaseFromSupplierModal(id) {
  const sup = suppliersData.find(s => (s.id === id || s.Id === id));
  if (!sup) return;

  pfsCurrentSupplierId = sup.id || sup.Id;
  const sName = sup.name || sup.Name;
  const sCompany = sup.company || sup.Company;

  document.getElementById('pfs-supId').value = pfsCurrentSupplierId;
  document.getElementById('pfs-supName').innerText = `شراء وتوريد مواد من المندوب: ${sName}`;
  document.getElementById('pfs-supCompany').innerText = `الشركة: ${sCompany || 'عام'} | الهاتف: ${sup.phone || sup.Phone || '--'}`;

  // Reset inputs
  pfsItems = [];
  renderPfsItemsTable();
  document.getElementById('pfs-itemName').value = '';
  document.getElementById('pfs-itemBarcode').value = '';
  document.getElementById('pfs-itemQty').value = '1';
  document.getElementById('pfs-itemCost').value = '';
  document.getElementById('pfsNotes').value = '';

  // Load existing products for dropdown
  const select = document.getElementById('pfs-existingProductSelect');
  if (select) {
    select.innerHTML = '<option value="">-- اختيار من مواد المندوب السابقة أو كتابة مادة جديدة --</option>';
    const res = await callBackend('get_supplier_products', { supplierId: pfsCurrentSupplierId });
    if (res && res.success && res.products) {
      currentSupplierProducts = res.products;
      res.products.forEach(p => {
        const opt = document.createElement('option');
        opt.value = p.id || p.Id;
        opt.innerText = `${p.name || p.Name} (باركود: ${p.barcode || p.Barcode || '--'} | تكلفة: ${(p.cost || p.Cost || 0).toLocaleString()} د.ع)`;
        select.appendChild(opt);
      });
    }
  }

  document.getElementById('purchaseFromSupplierModal')?.classList.remove('hidden');
  document.getElementById('pfs-itemName')?.focus();
}

function handlePfsProductSelect() {
  const select = document.getElementById('pfs-existingProductSelect');
  const prodId = select?.value;
  if (!prodId) return;

  const prod = currentSupplierProducts.find(p => (p.id === prodId || p.Id === prodId));
  if (prod) {
    document.getElementById('pfs-itemName').value = prod.name || prod.Name || '';
    document.getElementById('pfs-itemBarcode').value = prod.barcode || prod.Barcode || '';
    document.getElementById('pfs-itemCost').value = prod.cost || prod.Cost || 0;
    document.getElementById('pfs-itemQty')?.focus();
  }
}

function addPfsItemToInvoice() {
  const name = document.getElementById('pfs-itemName')?.value.trim();
  if (!name) {
    alert('يرجى كتابة أو اختيار اسم المادة أولاً!');
    document.getElementById('pfs-itemName')?.focus();
    return;
  }

  const barcode = document.getElementById('pfs-itemBarcode')?.value.trim() || '';
  const qty = Number(document.getElementById('pfs-itemQty')?.value || 1);
  const cost = Number(document.getElementById('pfs-itemCost')?.value || 0);

  if (qty <= 0) {
    alert('الكمية يجب أن تكون أكبر من 0');
    return;
  }
  if (cost <= 0) {
    alert('يرجى تحديد سعر شراء / تكلفة القطعة بشكل صحيح!');
    document.getElementById('pfs-itemCost')?.focus();
    return;
  }

  const prodId = document.getElementById('pfs-existingProductSelect')?.value || undefined;

  pfsItems.push({
    productId: prodId,
    name: name,
    barcode: barcode,
    quantity: qty,
    unitCost: cost,
    totalPrice: qty * cost,
    unit: 'قطعة'
  });

  // Reset inputs for next item
  document.getElementById('pfs-itemName').value = '';
  document.getElementById('pfs-itemBarcode').value = '';
  document.getElementById('pfs-itemQty').value = '1';
  document.getElementById('pfs-itemCost').value = '';
  document.getElementById('pfs-existingProductSelect').value = '';

  renderPfsItemsTable();
  document.getElementById('pfs-itemName')?.focus();
}

function removePfsItem(index) {
  pfsItems.splice(index, 1);
  renderPfsItemsTable();
}

function renderPfsItemsTable() {
  const tbody = document.getElementById('pfsItemsTableBody');
  const totalEl = document.getElementById('pfsTotalAmount');
  if (!tbody) return;

  if (pfsItems.length === 0) {
    tbody.innerHTML = `<tr><td colspan="6" class="text-center py-6 text-slate-400">لم يتم إضافة مواد للفاتورة بعد</td></tr>`;
    if (totalEl) totalEl.innerText = '0 د.ع';
    return;
  }

  tbody.innerHTML = '';
  let grandTotal = 0;

  pfsItems.forEach((item, idx) => {
    grandTotal += item.totalPrice;
    const row = document.createElement('tr');
    row.innerHTML = `
      <td class="p-2.5 font-bold text-slate-900 dark:text-white">${item.name}</td>
      <td class="p-2.5 font-mono text-slate-500">${item.barcode || '--'}</td>
      <td class="p-2.5 text-center font-mono font-bold">${item.quantity}</td>
      <td class="p-2.5 text-center font-mono text-emerald-600 dark:text-emerald-400 font-bold">${item.unitCost.toLocaleString()} د.ع</td>
      <td class="p-2.5 text-center font-mono font-black text-slate-900 dark:text-white">${item.totalPrice.toLocaleString()} د.ع</td>
      <td class="p-2.5 text-center">
        <button onclick="removePfsItem(${idx})" class="w-6 h-6 rounded-lg bg-rose-50 hover:bg-rose-100 text-rose-600 text-xs font-bold transition">✕</button>
      </td>
    `;
    tbody.appendChild(row);
  });

  if (totalEl) totalEl.innerText = `${grandTotal.toLocaleString()} د.ع`;
}

function closePurchaseFromSupplierModal() {
  document.getElementById('purchaseFromSupplierModal')?.classList.add('hidden');
}

async function savePurchaseInvoiceAndGenerateReceipt() {
  if (pfsItems.length === 0) {
    alert('يرجى إضافة مادة واحدة على الأقل لفاتورة الشراء!');
    return;
  }

  const supId = document.getElementById('pfs-supId')?.value;
  const payMethod = document.querySelector('input[name="pfsPayMethod"]:checked')?.value || 'credit';
  const isPaid = (payMethod === 'cash');
  const notes = document.getElementById('pfsNotes')?.value.trim();
  const invoiceNumber = `PUR-${Date.now()}`;

  const payload = {
    supplierId: supId,
    invoiceNumber: invoiceNumber,
    isPaid: isPaid,
    notes: notes,
    items: pfsItems
  };

  const res = await callBackend('create_purchase_invoice', payload);
  if (res && res.success) {
    alert(`✔ تم حفظ فاتورة الشراء وتحديث رصيد المخزن بنجاح! رقم الوصل: ${invoiceNumber}`);
    closePurchaseFromSupplierModal();
    await loadSuppliers();
    await loadProducts(); // Update inventory
    // Open the official receipt preview modal immediately!
    await openPurchaseReceiptModal(invoiceNumber);
  }
}

// ========================================================
// OFFICIAL RECEIPT PREVIEW & PRINT (عرض وطباعة الوصل)
// ========================================================
async function openPurchaseReceiptModal(invoiceNumber, orderId) {
  const res = await callBackend('get_purchase_invoice_details', {
    invoiceNumber: invoiceNumber || undefined,
    orderId: orderId || undefined
  });

  if (!res || !res.success || !res.order) {
    alert('تعذر تحميل تفاصيل الوصل!');
    return;
  }

  const o = res.order;
  document.getElementById('rcpt-num').innerText = o.invoiceNumber || '--';
  document.getElementById('rcpt-date').innerText = o.date || '--';
  document.getElementById('rcpt-supName').innerText = o.supplierName || '--';
  document.getElementById('rcpt-supCompany').innerText = o.company || 'شركة عامة';
  document.getElementById('rcpt-supPhone').innerText = o.phone || '--';
  document.getElementById('rcpt-grandTotal').innerText = `${Number(o.totalAmount || 0).toLocaleString()} د.ع`;
  document.getElementById('rcpt-notes').innerText = o.notes || 'لا توجد ملاحظات';

  const tbody = document.getElementById('rcpt-itemsTableBody');
  if (tbody) {
    tbody.innerHTML = '';
    (o.items || []).forEach(it => {
      const row = document.createElement('tr');
      row.innerHTML = `
        <td class="p-2 border-l border-slate-300">${it.productName}</td>
        <td class="p-2 text-center font-mono font-bold border-l border-slate-300">${it.quantity} ${it.unitType || 'قطعة'}</td>
        <td class="p-2 text-center font-mono border-l border-slate-300">${Number(it.unitPrice).toLocaleString()} د.ع</td>
        <td class="p-2 text-center font-mono font-bold">${Number(it.totalPrice).toLocaleString()} د.ع</td>
      `;
      tbody.appendChild(row);
    });
  }

  document.getElementById('purchaseReceiptModal')?.classList.remove('hidden');
}

function closePurchaseReceiptModal() {
  document.getElementById('purchaseReceiptModal')?.classList.add('hidden');
}

function printPurchaseReceipt() {
  const content = document.getElementById('printablePurchaseReceipt')?.innerHTML;
  if (!content) return;

  const printWindow = window.open('', '_blank', 'width=700,height=800');
  if (!printWindow) {
    window.print();
    return;
  }

  printWindow.document.write(`
    <!DOCTYPE html>
    <html dir="rtl" lang="ar">
    <head>
      <meta charset="utf-8">
      <title>وصل استلام بضاعة</title>
      <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 20px; direction: rtl; }
        table { width: 100%; border-collapse: collapse; margin-top: 10px; }
        th, td { border: 1px solid #333; padding: 8px; text-align: right; }
        th { background: #f0f0f0; }
        .text-center { text-align: center; }
        .font-mono { font-family: monospace; }
        .font-bold { font-weight: bold; }
      </style>
    </head>
    <body>
      ${content}
      <script>
        window.onload = function() {
          window.print();
          setTimeout(() => { window.close(); }, 500);
        };
      </script>
    </body>
    </html>
  `);
  printWindow.document.close();
}

async function openSupplierStatementModal(id) {
  const sup = suppliersData.find(s => (s.id === id || s.Id === id));
  if (!sup) return;

  const sName = sup.name || sup.Name;
  const sCompany = sup.company || sup.Company;
  const sPhone = sup.phone || sup.Phone;
  const sBal = sup.balance ?? sup.Balance ?? 0;

  document.getElementById('stmt-supName').innerText = `كشف حساب: ${sName}`;
  document.getElementById('stmt-supCompany').innerText = `الشركة: ${sCompany || 'غير محدد'} | الهاتف: ${sPhone || '--'}`;
  document.getElementById('stmt-currentBalance').innerText = `${Math.round(Number(sBal)).toLocaleString()} د.ع`;

  const tbody = document.getElementById('supplierStatementTableBody');
  if (tbody) tbody.innerHTML = `<tr><td colspan="5" class="text-center py-6 text-slate-400">جاري تحميل كشف الحركات والوصولات...</td></tr>`;

  document.getElementById('supplierStatementModal')?.classList.remove('hidden');

  const res = await callBackend('get_supplier_transactions', { supplierId: sup.id || sup.Id });
  if (res && res.success && res.transactions && res.transactions.length > 0) {
    tbody.innerHTML = '';
    res.transactions.forEach(t => {
      const isPayment = (t.transactionType === 'Payment' || t.transactionType === 'دفع');
      const typeBadge = isPayment
        ? `<span class="bg-emerald-100 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400 px-2 py-0.5 rounded-full font-bold text-[10px]">تسديد دفعة</span>`
        : `<span class="bg-amber-100 text-amber-700 dark:bg-amber-950/60 dark:text-amber-400 px-2 py-0.5 rounded-full font-bold text-[10px]">فاتورة شراء</span>`;
      
      const amountColor = isPayment ? 'text-emerald-600 font-bold' : 'text-amber-600 font-bold';

      const hasReceipt = t.invoiceNumber && t.invoiceNumber.startsWith('PUR-');
      const actionBtn = hasReceipt
        ? `<button onclick="openPurchaseReceiptModal('${t.invoiceNumber}')" class="px-2 py-0.5 bg-sky-50 dark:bg-sky-950/40 hover:bg-sky-100 text-sky-700 dark:text-sky-300 rounded-lg text-[10px] font-bold flex items-center gap-1"><span>👁️</span><span>عرض الوصل</span></button>`
        : `<span class="text-slate-400 text-[10px] font-mono">${t.invoiceNumber || '--'}</span>`;

      const row = document.createElement('tr');
      row.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40 transition';
      row.innerHTML = `
        <td class="p-2.5 text-slate-500 font-mono">${t.date || '--'}</td>
        <td class="p-2.5">${typeBadge}</td>
        <td class="p-2.5 text-center font-mono ${amountColor}">${Number(t.amount).toLocaleString()} د.ع</td>
        <td class="p-2.5">${actionBtn}</td>
        <td class="p-2.5 text-slate-700 dark:text-slate-300 font-semibold">${t.description || '--'}</td>
      `;
      tbody.appendChild(row);
    });
  } else {
    if (tbody) tbody.innerHTML = `<tr><td colspan="5" class="text-center py-6 text-slate-400">لا توجد حركات مالية أو وصولات مسجلة لهذا المندوب بعد</td></tr>`;
  }
}

function closeSupplierStatementModal() {
  document.getElementById('supplierStatementModal')?.classList.add('hidden');
}

// ========================================================
// VIEW 5: PURCHASE & RECEIVING (شراء وتوريد مواد للمخزن)
// ========================================================
let purInvoiceItems = [];
let purCurrentProduct = null;
let purInputMode = 'piece'; // 'piece' or 'carton'

async function initPurchaseTab() {
  // Ensure products list is loaded for auto-search
  if (!state.products || state.products.length === 0) {
    await loadProducts();
  }

  // Load suppliers dropdown
  const supSelect = document.getElementById('pur-supplierSelect');
  if (supSelect) {
    supSelect.innerHTML = '<option value="">-- اختر المندوب أو الشركة الموردة --</option>';
    const res = await callBackend('get_suppliers');
    if (res && res.success && res.suppliers) {
      suppliersData = res.suppliers;
      res.suppliers.forEach(s => {
        const sId = s.id || s.Id;
        const sName = s.name || s.Name;
        const sCompany = s.company || s.Company || 'عام';
        const opt = document.createElement('option');
        opt.value = sId;
        opt.innerText = `${sName} (${sCompany}) - الرصيد: ${Math.round(Number(s.balance ?? s.Balance ?? 0)).toLocaleString()} د.ع`;
        supSelect.appendChild(opt);
      });
    }
  }

  // Generate clean invoice number if empty
  const invNoEl = document.getElementById('pur-invoiceNumber');
  if (invNoEl && !invNoEl.value) {
    invNoEl.value = `PUR-${Date.now()}`;
  }

  // Reset inputs
  setPurchaseInputMode('piece');
  renderPurchaseInvoiceTable();

  // Attach key listener on qty & cost inputs to auto-advance
  const qtyInput = document.getElementById('pur-itemQty');
  if (qtyInput && !qtyInput.dataset.listener) {
    qtyInput.dataset.listener = 'true';
    qtyInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        const newCostInput = document.getElementById('pur-newCost');
        if (newCostInput && !newCostInput.value) {
          newCostInput.focus();
          newCostInput.select();
        } else {
          addPurchaseItemToInvoice();
        }
      }
    });
  }

  const newCostInput = document.getElementById('pur-newCost');
  if (newCostInput && !newCostInput.dataset.listener) {
    newCostInput.dataset.listener = 'true';
    newCostInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        addPurchaseItemToInvoice();
      }
    });
  }

  setTimeout(() => {
    document.getElementById('pur-barcodeSearch')?.focus();
  }, 100);
}

function handlePurchaseSupplierChange() {
  const supId = document.getElementById('pur-supplierSelect')?.value;
  // If a supplier is selected, we can optionally filter or recommend products
}

function setPurchaseInputMode(mode) {
  purInputMode = mode;
  const btnPiece = document.getElementById('pur-mode-piece');
  const btnCarton = document.getElementById('pur-mode-carton');
  const cartonQtyContainer = document.getElementById('pur-itemsPerCarton-container');
  const qtyLabel = document.getElementById('pur-qty-label');

  if (mode === 'carton') {
    if (btnPiece) btnPiece.className = 'flex-1 py-1 text-center text-xs font-black rounded-lg transition text-slate-500 hover:text-slate-900 dark:hover:text-white';
    if (btnCarton) btnCarton.className = 'flex-1 py-1 text-center text-xs font-black rounded-lg transition bg-white dark:bg-slate-900 text-sky-600 shadow-sm';
    if (cartonQtyContainer) cartonQtyContainer.classList.remove('hidden');
    if (qtyLabel) qtyLabel.innerText = 'عدد الكراتين *';

    // If a product is selected and has carton packaging info, set itemsPerCarton
    if (purCurrentProduct) {
      const ipc = Number(purCurrentProduct.itemsPerCarton || purCurrentProduct.ItemsPerCarton || 1);
      const ipcInput = document.getElementById('pur-itemsPerCarton');
      if (ipcInput) ipcInput.value = ipc > 1 ? ipc : 12;

      const cCost = Number(purCurrentProduct.cartonPurchasePrice || purCurrentProduct.CartonPurchasePrice || 0);
      const newCostInput = document.getElementById('pur-newCost');
      if (newCostInput && cCost > 0) newCostInput.value = cCost;

      const oldBadge = document.getElementById('pur-oldCostBadge');
      if (oldBadge) oldBadge.innerText = `${(cCost > 0 ? cCost : (purCurrentProduct.cost * ipc)).toLocaleString()} د.ع (كرتون)`;
    }
  } else {
    if (btnPiece) btnPiece.className = 'flex-1 py-1 text-center text-xs font-black rounded-lg transition bg-white dark:bg-slate-900 text-sky-600 shadow-sm';
    if (btnCarton) btnCarton.className = 'flex-1 py-1 text-center text-xs font-black rounded-lg transition text-slate-500 hover:text-slate-900 dark:hover:text-white';
    if (cartonQtyContainer) cartonQtyContainer.classList.add('hidden');
    if (qtyLabel) qtyLabel.innerText = 'العدد بالقطعة / المفرد *';

    if (purCurrentProduct) {
      const pCost = Number(purCurrentProduct.cost || purCurrentProduct.Cost || 0);
      const newCostInput = document.getElementById('pur-newCost');
      if (newCostInput) newCostInput.value = pCost;

      const oldBadge = document.getElementById('pur-oldCostBadge');
      if (oldBadge) oldBadge.innerText = `${pCost.toLocaleString()} د.ع (قطعة)`;
    }
  }

  recalcPurchaseCurrentItem();
}

function handlePurchaseSearchInput() {
  const query = (document.getElementById('pur-barcodeSearch')?.value || '').trim().toLowerCase();
  const dropdown = document.getElementById('pur-searchSuggestions');
  if (!dropdown) return;

  if (!query) {
    dropdown.classList.add('hidden');
    dropdown.innerHTML = '';
    return;
  }

  const matches = (state.products || []).filter(p => 
    (p.barcode && p.barcode.toLowerCase().includes(query)) ||
    (p.name && p.name.toLowerCase().includes(query))
  ).slice(0, 10);

  if (matches.length === 0) {
    dropdown.innerHTML = `
      <div class="p-3 text-xs text-slate-400 text-center">
        <span>لا توجد مادة بهذا الاسم/الباركود بالمخزن.</span>
        <span class="block text-[10px] text-sky-500 font-bold mt-1">سيتم إضافتها كمادة جديدة في المخزن والوصل</span>
      </div>
    `;
    dropdown.classList.remove('hidden');
    return;
  }

  dropdown.innerHTML = '';
  matches.forEach(p => {
    const itemEl = document.createElement('div');
    itemEl.className = 'p-2.5 hover:bg-sky-50 dark:hover:bg-slate-800/80 cursor-pointer flex items-center justify-between text-xs transition';
    itemEl.innerHTML = `
      <div class="flex items-center gap-2">
        <span class="font-bold text-slate-900 dark:text-white">${p.name}</span>
        <span class="text-slate-400 font-mono text-[10px]">(${p.barcode})</span>
      </div>
      <div class="flex items-center gap-3">
        <span class="text-[10px] text-slate-400">رصيد: <b class="text-sky-600">${p.stockQuantity}</b></span>
        <span class="text-xs font-black text-emerald-600 font-mono">شراء: ${Number(p.cost).toLocaleString()} د.ع</span>
      </div>
    `;
    itemEl.onclick = () => {
      selectPurchaseProduct(p);
      dropdown.classList.add('hidden');
    };
    dropdown.appendChild(itemEl);
  });
  dropdown.classList.remove('hidden');
}

function handlePurchaseSearchKeydown(e) {
  if (e.key === 'Enter') {
    e.preventDefault();
    const query = (document.getElementById('pur-barcodeSearch')?.value || '').trim();
    if (!query) return;

    // Check exact barcode match first
    const exact = (state.products || []).find(p => p.barcode === query || p.name.toLowerCase() === query.toLowerCase());
    if (exact) {
      selectPurchaseProduct(exact);
      document.getElementById('pur-searchSuggestions')?.classList.add('hidden');
      return;
    }

    // Check first partial match
    const partial = (state.products || []).find(p => 
      (p.barcode && p.barcode.includes(query)) ||
      (p.name && p.name.toLowerCase().includes(query.toLowerCase()))
    );
    if (partial) {
      selectPurchaseProduct(partial);
      document.getElementById('pur-searchSuggestions')?.classList.add('hidden');
      return;
    }

    // If not found in warehouse, treat as new item with this barcode / name
    purCurrentProduct = {
      name: query,
      barcode: query,
      cost: 0,
      stockQuantity: 0,
      unit: 'قطعة'
    };
    showPurchaseSelectedStrip(purCurrentProduct, true);
    document.getElementById('pur-searchSuggestions')?.classList.add('hidden');
    document.getElementById('pur-newCost')?.focus();
  }
}

function selectPurchaseProduct(prod) {
  purCurrentProduct = prod;
  document.getElementById('pur-barcodeSearch').value = `${prod.name} (${prod.barcode})`;
  showPurchaseSelectedStrip(prod, false);

  const cost = Number(prod.cost || prod.Cost || 0);
  const oldCostBadge = document.getElementById('pur-oldCostBadge');
  if (oldCostBadge) oldCostBadge.innerText = `${cost.toLocaleString()} د.ع (قطعة)`;

  const newCostInput = document.getElementById('pur-newCost');
  if (newCostInput) newCostInput.value = cost > 0 ? cost : '';

  // Auto-set supplier if linked
  if (prod.supplierId || prod.SupplierId) {
    const sId = prod.supplierId || prod.SupplierId;
    const supSelect = document.getElementById('pur-supplierSelect');
    if (supSelect && supSelect.querySelector(`option[value="${sId}"]`)) {
      supSelect.value = sId;
    }
  }

  // Detect carton default if itemsPerCarton > 1
  const ipc = Number(prod.itemsPerCarton || prod.ItemsPerCarton || 1);
  if (ipc > 1) {
    setPurchaseInputMode('carton');
  } else {
    setPurchaseInputMode('piece');
  }

  recalcPurchaseCurrentItem();
  document.getElementById('pur-itemQty')?.focus();
  document.getElementById('pur-itemQty')?.select();
}

function showPurchaseSelectedStrip(prod, isNew) {
  const strip = document.getElementById('pur-selectedProdStrip');
  if (!strip) return;

  document.getElementById('pur-stripName').innerText = `المادة: ${prod.name}`;
  document.getElementById('pur-stripBarcode').innerText = `(باركود: ${prod.barcode || 'تلقائي'})`;
  document.getElementById('pur-stripStock').innerText = isNew ? 'مادة جديدة غير مسجلة' : `الرصيد الحالي بالمخزن: ${prod.stockQuantity || 0}`;

  strip.classList.remove('hidden');
}

function recalcPurchaseCurrentItem() {
  const qty = Number(document.getElementById('pur-itemQty')?.value || 1);
  const newCost = Number(document.getElementById('pur-newCost')?.value || 0);
  const oldCost = purCurrentProduct ? Number(purCurrentProduct.cost || purCurrentProduct.Cost || 0) : 0;

  let totalItemPrice = 0;
  let totalPieces = qty;

  if (purInputMode === 'carton') {
    const ipc = Number(document.getElementById('pur-itemsPerCarton')?.value || 1);
    totalPieces = qty * ipc;
    totalItemPrice = qty * newCost; // newCost is per carton in carton mode
  } else {
    totalItemPrice = qty * newCost; // newCost is per piece in piece mode
  }

  const stripTotal = document.getElementById('pur-stripTotalCalc');
  if (stripTotal) stripTotal.innerText = `إجمالي البند: ${Math.round(totalItemPrice).toLocaleString()} د.ع (${totalPieces} قطعة)`;

  // Price difference badge
  const diffBadge = document.getElementById('pur-priceDiffBadge');
  if (diffBadge && newCost > 0 && oldCost > 0) {
    const diff = newCost - oldCost;
    if (diff > 0) {
      diffBadge.className = 'h-9 px-3 bg-rose-50 dark:bg-rose-950/40 rounded-xl border border-rose-200 dark:border-rose-800 flex items-center justify-center text-xs font-black text-rose-600 dark:text-rose-400 gap-1';
      diffBadge.innerHTML = `<span>🔺 ارتفع السعر: +${diff.toLocaleString()} د.ع</span>`;
    } else if (diff < 0) {
      diffBadge.className = 'h-9 px-3 bg-emerald-50 dark:bg-emerald-950/40 rounded-xl border border-emerald-200 dark:border-emerald-800 flex items-center justify-center text-xs font-black text-emerald-600 dark:text-emerald-400 gap-1';
      diffBadge.innerHTML = `<span>🔻 انخفض السعر: ${diff.toLocaleString()} د.ع</span>`;
    } else {
      diffBadge.className = 'h-9 px-3 bg-slate-50 dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 flex items-center justify-center text-xs font-bold text-slate-400';
      diffBadge.innerHTML = `<span>السعر ثابت لم يتغير</span>`;
    }
  } else if (diffBadge) {
    diffBadge.className = 'h-9 px-3 bg-slate-50 dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 flex items-center justify-center text-xs font-bold text-slate-400';
    diffBadge.innerHTML = `<span>السعر لم يتغير</span>`;
  }
}

function addPurchaseItemToInvoice() {
  const searchVal = document.getElementById('pur-barcodeSearch')?.value.trim();
  if (!purCurrentProduct && !searchVal) {
    alert('يرجى اختيار مادة من المخزن أو كتابة اسمها والباركود أولاً!');
    document.getElementById('pur-barcodeSearch')?.focus();
    return;
  }

  const pName = purCurrentProduct ? purCurrentProduct.name : searchVal;
  const pBarcode = purCurrentProduct ? (purCurrentProduct.barcode || '') : '';
  const qty = Number(document.getElementById('pur-itemQty')?.value || 1);
  const newCost = Number(document.getElementById('pur-newCost')?.value || 0);
  const oldCost = purCurrentProduct ? Number(purCurrentProduct.cost || purCurrentProduct.Cost || 0) : 0;

  if (qty <= 0) {
    alert('الكمية يجب أن تكون أكبر من 0!');
    document.getElementById('pur-itemQty')?.focus();
    return;
  }
  if (newCost <= 0) {
    alert('يرجى تحديد سعر الشراء الجديد بشكل صحيح (أكبر من 0)!');
    document.getElementById('pur-newCost')?.focus();
    return;
  }

  let packagingText = `${qty} قطعة`;
  let totalPieces = qty;
  let cartonCount = 0;
  let itemsPerCarton = 1;
  let unitCostForDb = newCost;
  let cartonPurchaseForDb = 0;
  let totalRowAmount = 0;

  if (purInputMode === 'carton') {
    itemsPerCarton = Number(document.getElementById('pur-itemsPerCarton')?.value || 1);
    cartonCount = qty;
    totalPieces = cartonCount * itemsPerCarton;
    packagingText = `${cartonCount} كرتون (${itemsPerCarton} ق/ك)`;
    cartonPurchaseForDb = newCost;
    unitCostForDb = itemsPerCarton > 0 ? (newCost / itemsPerCarton) : newCost;
    totalRowAmount = cartonCount * newCost;
  } else {
    totalRowAmount = qty * newCost;
    unitCostForDb = newCost;
  }

  purInvoiceItems.push({
    productId: purCurrentProduct ? (purCurrentProduct.id || purCurrentProduct.Id) : undefined,
    name: pName,
    barcode: pBarcode,
    packagingText: packagingText,
    inputMode: purInputMode,
    quantity: totalPieces,
    cartonsCount: cartonCount,
    itemsPerCarton: itemsPerCarton,
    cartonPurchasePrice: cartonPurchaseForDb,
    unitCost: unitCostForDb,
    displayNewCost: newCost,
    oldCost: oldCost,
    totalPrice: totalRowAmount,
    unit: 'قطعة'
  });

  // Clear current item inputs for next product
  purCurrentProduct = null;
  document.getElementById('pur-barcodeSearch').value = '';
  document.getElementById('pur-itemQty').value = '1';
  document.getElementById('pur-newCost').value = '';
  document.getElementById('pur-oldCostBadge').innerText = '0 د.ع';
  document.getElementById('pur-selectedProdStrip')?.classList.add('hidden');
  document.getElementById('pur-priceDiffBadge').innerHTML = '<span>السعر لم يتغير</span>';

  renderPurchaseInvoiceTable();
  document.getElementById('pur-barcodeSearch')?.focus();
}

function removePurchaseInvoiceItem(idx) {
  purInvoiceItems.splice(idx, 1);
  renderPurchaseInvoiceTable();
}

function renderPurchaseInvoiceTable() {
  const tbody = document.getElementById('pur-invoiceTableBody');
  const countBadge = document.getElementById('pur-itemsCountBadge');
  const piecesEl = document.getElementById('pur-totalPieces');
  const grandTotalEl = document.getElementById('pur-grandTotalDisplay');

  if (!tbody) return;

  if (purInvoiceItems.length === 0) {
    tbody.innerHTML = `<tr><td colspan="9" class="text-center py-8 text-slate-400">لم يتم إضافة مواد إلى وصل الشراء بعد</td></tr>`;
    if (countBadge) countBadge.innerText = '0 مادة في الوصل';
    if (piecesEl) piecesEl.innerText = '0 قطعة';
    if (grandTotalEl) grandTotalEl.innerText = '0 د.ع';
    return;
  }

  tbody.innerHTML = '';
  let grandTotal = 0;
  let grandPieces = 0;

  purInvoiceItems.forEach((item, idx) => {
    grandTotal += item.totalPrice;
    grandPieces += item.quantity;

    const costDiff = item.displayNewCost - item.oldCost;
    const diffBadge = item.oldCost > 0 
      ? (costDiff > 0 ? `<span class="text-rose-500 font-bold text-[10px] block">🔺 +${costDiff.toLocaleString()}</span>` : (costDiff < 0 ? `<span class="text-emerald-500 font-bold text-[10px] block">🔻 ${costDiff.toLocaleString()}</span>` : ''))
      : '';

    const row = document.createElement('tr');
    row.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40 transition';
    row.innerHTML = `
      <td class="p-3 text-slate-400 font-mono">${idx + 1}</td>
      <td class="p-3 font-bold text-slate-900 dark:text-white">${item.name}</td>
      <td class="p-3 font-mono text-slate-500">${item.barcode || '--'}</td>
      <td class="p-3 text-center font-bold text-sky-600 dark:text-sky-400">${item.packagingText}</td>
      <td class="p-3 text-center font-black font-mono">${item.quantity} قطعة</td>
      <td class="p-3 text-center font-mono text-slate-400">${item.oldCost ? item.oldCost.toLocaleString() + ' د.ع' : '--'}</td>
      <td class="p-3 text-center font-mono font-black text-emerald-600 dark:text-emerald-400">
        ${item.displayNewCost.toLocaleString()} د.ع
        ${diffBadge}
      </td>
      <td class="p-3 text-center font-mono font-black text-slate-900 dark:text-white">${item.totalPrice.toLocaleString()} د.ع</td>
      <td class="p-3 text-center">
        <button onclick="removePurchaseInvoiceItem(${idx})" class="w-6 h-6 rounded-lg bg-rose-50 hover:bg-rose-100 text-rose-600 text-xs font-bold transition">✕</button>
      </td>
    `;
    tbody.appendChild(row);
  });

  if (countBadge) countBadge.innerText = `${purInvoiceItems.length} مادة في الوصل`;
  if (piecesEl) piecesEl.innerText = `${grandPieces.toLocaleString()} قطعة`;
  if (grandTotalEl) grandTotalEl.innerText = `${Math.round(grandTotal).toLocaleString()} د.ع`;
}

function resetPurchaseForm() {
  purInvoiceItems = [];
  purCurrentProduct = null;
  document.getElementById('pur-barcodeSearch').value = '';
  document.getElementById('pur-itemQty').value = '1';
  document.getElementById('pur-newCost').value = '';
  document.getElementById('pur-oldCostBadge').innerText = '0 د.ع';
  document.getElementById('pur-selectedProdStrip')?.classList.add('hidden');
  document.getElementById('pur-invoiceNotes').value = '';
  document.getElementById('pur-invoiceNumber').value = `PUR-${Date.now()}`;
  renderPurchaseInvoiceTable();
}

async function finalizePurchaseInvoice() {
  if (purInvoiceItems.length === 0) {
    alert('يرجى إضافة مادة واحدة على الأقل لفاتورة الشراء أولاً!');
    document.getElementById('pur-barcodeSearch')?.focus();
    return;
  }

  const supId = document.getElementById('pur-supplierSelect')?.value;
  if (!supId) {
    alert('يرجى اختيار المندوب أو الشركة الموردة قبل تأكيد الشراء!');
    document.getElementById('pur-supplierSelect')?.focus();
    return;
  }

  const payMethod = document.querySelector('input[name="pur-payMethod"]:checked')?.value || 'credit';
  const isPaid = (payMethod === 'cash');
  const notes = document.getElementById('pur-invoiceNotes')?.value.trim();
  const invoiceNumber = document.getElementById('pur-invoiceNumber')?.value || `PUR-${Date.now()}`;

  const payload = {
    supplierId: supId,
    invoiceNumber: invoiceNumber,
    isPaid: isPaid,
    notes: notes,
    items: purInvoiceItems
  };

  const res = await callBackend('create_purchase_invoice', payload);
  if (res && res.success) {
    alert(`✔ تم تأكيد الشراء وتحديث رصيد المخزن والأسعار بنجاح! رقم الوصل: ${invoiceNumber}`);
    
    // Refresh all data
    await loadSuppliers();
    await loadProducts();

    // Open official printable receipt modal
    await openPurchaseReceiptModal(invoiceNumber);

    // Reset form for next invoice
    resetPurchaseForm();
  }
}

// ========================================================
// OCR & PHOTO RECEIPT SCANNING SYSTEM (إضافة مواد بالصورة)
// ========================================================
let ocrMediaStream = null;
let ocrDetectedItems = [];

async function openReceiptPhotoModal(defaultSource = 'file') {
  document.getElementById('receiptPhotoModal')?.classList.remove('hidden');
  document.getElementById('rpm-previewContainer')?.classList.add('hidden');
  document.getElementById('rpm-statusContainer')?.classList.add('hidden');
  ocrDetectedItems = [];
  renderOcrItemsTable();
  switchOcrInputSource(defaultSource || 'file');

  // Setup clipboard paste listener (Ctrl + V)
  if (!window._ocrPasteBound) {
    window._ocrPasteBound = true;
    window.addEventListener('paste', (e) => {
      const modal = document.getElementById('receiptPhotoModal');
      if (modal && !modal.classList.contains('hidden')) {
        const items = e.clipboardData?.items;
        if (items) {
          for (let i = 0; i < items.length; i++) {
            if (items[i].type.indexOf('image') !== -1) {
              const blob = items[i].getAsFile();
              const reader = new FileReader();
              reader.onload = (ev) => {
                const dataUrl = ev.target.result;
                showOcrPreviewThumbnail(dataUrl);
                processReceiptImage(dataUrl);
              };
              reader.readAsDataURL(blob);
              break;
            }
          }
        }
      }
    });
  }
}

function closeReceiptPhotoModal() {
  stopOcrCamera();
  document.getElementById('receiptPhotoModal')?.classList.add('hidden');
}

function switchOcrInputSource(source) {
  const tabCam = document.getElementById('rpm-tab-camera');
  const tabFile = document.getElementById('rpm-tab-file');
  const camContainer = document.getElementById('rpm-cameraContainer');
  const fileContainer = document.getElementById('rpm-fileContainer');

  if (source === 'camera') {
    if (tabCam) tabCam.className = 'flex-1 py-1.5 text-center text-xs font-black rounded-lg transition bg-white dark:bg-slate-900 text-purple-600 shadow-sm flex items-center justify-center gap-1';
    if (tabFile) tabFile.className = 'flex-1 py-1.5 text-center text-xs font-black rounded-lg transition text-slate-500 hover:text-slate-900 dark:hover:text-white flex items-center justify-center gap-1';
    if (camContainer) camContainer.classList.remove('hidden');
    if (fileContainer) fileContainer.classList.add('hidden');
    startOcrCamera();
  } else {
    stopOcrCamera();
    if (tabCam) tabCam.className = 'flex-1 py-1.5 text-center text-xs font-black rounded-lg transition text-slate-500 hover:text-slate-900 dark:hover:text-white flex items-center justify-center gap-1';
    if (tabFile) tabFile.className = 'flex-1 py-1.5 text-center text-xs font-black rounded-lg transition bg-white dark:bg-slate-900 text-purple-600 shadow-sm flex items-center justify-center gap-1';
    if (camContainer) camContainer.classList.add('hidden');
    if (fileContainer) fileContainer.classList.remove('hidden');
  }
}

async function startOcrCamera() {
  const video = document.getElementById('rpm-video');
  const placeholder = document.getElementById('rpm-cameraPlaceholder');
  const captureBtn = document.getElementById('rpm-captureBtn');

  try {
    if (ocrMediaStream) {
      ocrMediaStream.getTracks().forEach(t => t.stop());
    }
    ocrMediaStream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 }, height: { ideal: 1080 } },
      audio: false
    });
    if (video) {
      video.srcObject = ocrMediaStream;
      video.classList.remove('hidden');
    }
    if (placeholder) placeholder.classList.add('hidden');
    if (captureBtn) captureBtn.classList.remove('hidden');
  } catch (err) {
    console.warn('[Camera] Could not access video stream:', err);
    if (placeholder) placeholder.classList.remove('hidden');
    if (captureBtn) captureBtn.classList.add('hidden');
  }
}

function stopOcrCamera() {
  if (ocrMediaStream) {
    ocrMediaStream.getTracks().forEach(t => t.stop());
    ocrMediaStream = null;
  }
  const video = document.getElementById('rpm-video');
  if (video) video.srcObject = null;
}

function captureOcrSnapshot() {
  const video = document.getElementById('rpm-video');
  const canvas = document.getElementById('rpm-canvas');
  if (!video || !canvas) return;

  canvas.width = video.videoWidth || 1280;
  canvas.height = video.videoHeight || 720;
  const ctx = canvas.getContext('2d');
  ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

  const dataUrl = canvas.toDataURL('image/jpeg', 0.92);
  showOcrPreviewThumbnail(dataUrl);
  stopOcrCamera();
  processReceiptImage(dataUrl);
}

function handleOcrFileUpload(e) {
  const file = e.target.files?.[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = (ev) => {
    const dataUrl = ev.target.result;
    showOcrPreviewThumbnail(dataUrl);
    processReceiptImage(dataUrl);
  };
  reader.readAsDataURL(file);
}

let ocrCurrentRotation = 0;
let ocrOriginalImageSrc = null;

function showOcrPreviewThumbnail(dataUrl) {
  ocrOriginalImageSrc = dataUrl;
  ocrCurrentRotation = 0;
  const previewContainer = document.getElementById('rpm-previewContainer');
  const previewImg = document.getElementById('rpm-previewImg');
  const camContainer = document.getElementById('rpm-cameraContainer');
  const fileContainer = document.getElementById('rpm-fileContainer');

  if (previewImg) {
    previewImg.src = dataUrl;
    previewImg.style.transform = 'rotate(0deg)';
  }
  if (previewContainer) previewContainer.classList.remove('hidden');
  if (camContainer) camContainer.classList.add('hidden');
  if (fileContainer) fileContainer.classList.add('hidden');
}

function rotateOcrImage(deg) {
  ocrCurrentRotation = (ocrCurrentRotation + deg) % 360;
  const img = document.getElementById('rpm-previewImg');
  if (img) img.style.transform = `rotate(${ocrCurrentRotation}deg)`;

  if (ocrOriginalImageSrc) {
    const tempImg = new Image();
    tempImg.onload = () => {
      const canvas = document.createElement('canvas');
      const ctx = canvas.getContext('2d');
      if (Math.abs(ocrCurrentRotation) === 90 || Math.abs(ocrCurrentRotation) === 270) {
        canvas.width = tempImg.height;
        canvas.height = tempImg.width;
      } else {
        canvas.width = tempImg.width;
        canvas.height = tempImg.height;
      }
      ctx.translate(canvas.width / 2, canvas.height / 2);
      ctx.rotate((ocrCurrentRotation * Math.PI) / 180);
      ctx.drawImage(tempImg, -tempImg.width / 2, -tempImg.height / 2);
      const rotatedData = canvas.toDataURL('image/jpeg', 0.95);
      processReceiptImage(rotatedData);
    };
    tempImg.src = ocrOriginalImageSrc;
  }
}

function enhanceOcrContrast() {
  const img = document.getElementById('rpm-previewImg');
  if (!img || !ocrOriginalImageSrc) return;

  const tempImg = new Image();
  tempImg.onload = () => {
    const canvas = document.createElement('canvas');
    canvas.width = tempImg.width;
    canvas.height = tempImg.height;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(tempImg, 0, 0);

    const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    const d = imgData.data;
    for (let i = 0; i < d.length; i += 4) {
      const v = 0.2126 * d[i] + 0.7152 * d[i + 1] + 0.0722 * d[i + 2];
      const contrast = 1.35;
      const factor = (259 * (contrast * 128 + 255)) / (255 * (259 - contrast * 128));
      const nv = Math.min(255, Math.max(0, factor * (v - 128) + 128));
      d[i] = nv;
      d[i + 1] = nv;
      d[i + 2] = nv;
    }
    ctx.putImageData(imgData, 0, 0);
    const enhancedData = canvas.toDataURL('image/jpeg', 0.95);
    img.src = enhancedData;
    processReceiptImage(enhancedData);
  };
  tempImg.src = ocrOriginalImageSrc;
}

function reCaptureOcrPhoto() {
  document.getElementById('rpm-previewContainer')?.classList.add('hidden');
  switchOcrInputSource('file');
}

function normalizeArabicNumerals(str) {
  if (!str) return '';
  return str.replace(/[٠-٩]/g, d => '٠١٢٣٤٥٦٧٨٩'.indexOf(d))
            .replace(/[۰-۹]/g, d => '۰۱۲۳۴۵۶۷۸۹'.indexOf(d));
}

async function processReceiptImage(imageSrc) {
  const statusContainer = document.getElementById('rpm-statusContainer');
  const statusText = document.getElementById('rpm-statusText');
  const progressBar = document.getElementById('rpm-progressBar');

  if (statusContainer) statusContainer.classList.remove('hidden');
  if (statusText) statusText.innerText = 'جاري تحليل الصورة والتعرف على النصوص...';
  if (progressBar) progressBar.style.width = '25%';

  try {
    let recognizedText = '';

    if (window.Tesseract) {
      if (statusText) statusText.innerText = 'جاري تشغيل محرك الذكاء الاصطناعي Tesseract OCR...';
      if (progressBar) progressBar.style.width = '50%';

      // Create OCR worker with graceful fallback
      const ocrPromise = (async () => {
        const worker = await Tesseract.createWorker(['ara', 'eng']);
        const ret = await worker.recognize(imageSrc);
        await worker.terminate();
        return ret.data.text;
      })();

      // 8 second timeout safeguard
      const timeoutPromise = new Promise((resolve) => setTimeout(() => resolve(''), 8000));
      recognizedText = await Promise.race([ocrPromise, timeoutPromise]);
    }

    if (progressBar) progressBar.style.width = '90%';
    if (statusText) statusText.innerText = 'جاري مطابقة المواد مع المخزن واستخراج الأسعار...';

    parseReceiptLines(recognizedText || '');

    if (progressBar) progressBar.style.width = '100%';
    setTimeout(() => {
      if (statusContainer) statusContainer.classList.add('hidden');
    }, 500);

  } catch (err) {
    console.error('[OCR Error]', err);
    if (statusText) statusText.innerText = 'تم تجهيز الجدول، يمكنك تعديل المواد والأسعار مباشرة.';
    parseReceiptLines('');
    setTimeout(() => {
      if (statusContainer) statusContainer.classList.add('hidden');
    }, 600);
  }
}

function parseReceiptLines(text) {
  const normalizedText = normalizeArabicNumerals(text || '');
  const lines = normalizedText.split(/\r?\n/).map(l => l.trim()).filter(l => l.length > 1);
  const items = [];

  lines.forEach((line) => {
    // Look for numbers in line
    const numbers = line.match(/\d+([.,]\d+)?/g);
    let cleanName = line.replace(/[\d.,:=#\-\*\/\(\)\[\]]/g, ' ').replace(/\s+/g, ' ').trim();

    if (cleanName.length < 2 && numbers && numbers.length > 0) {
      cleanName = `بند (${numbers[0]})`;
    }

    if (cleanName.length >= 2 || (numbers && numbers.length >= 1)) {
      let qty = 1;
      let unitCost = 0;

      if (numbers && numbers.length >= 2) {
        const n1 = parseFloat(numbers[0].replace(',', ''));
        const n2 = parseFloat(numbers[1].replace(',', ''));
        if (n1 <= 1000 && n2 > n1) {
          qty = n1;
          unitCost = n2;
        } else if (n2 <= 1000 && n1 > n2) {
          qty = n2;
          unitCost = n1;
        } else {
          qty = 1;
          unitCost = Math.max(n1, n2);
        }
      } else if (numbers && numbers.length === 1) {
        const n = parseFloat(numbers[0].replace(',', ''));
        if (n >= 100) unitCost = n;
        else qty = n;
      }

      // Match product from warehouse
      const matchedProd = (state.products || []).find(p => 
        (p.name && cleanName.toLowerCase().includes(p.name.toLowerCase())) || 
        (p.name && p.name.toLowerCase().includes(cleanName.toLowerCase()))
      );

      items.push({
        productId: matchedProd ? (matchedProd.id || matchedProd.Id) : undefined,
        name: matchedProd ? matchedProd.name : (cleanName || 'مادة جديدة'),
        barcode: matchedProd ? matchedProd.barcode : '',
        quantity: qty > 0 ? qty : 1,
        unitCost: unitCost > 0 ? unitCost : (matchedProd ? Number(matchedProd.cost) : 1000),
        unit: 'قطعة'
      });
    }
  });

  if (items.length === 0) {
    // Add default row ready to edit
    items.push({
      name: 'مادة جديدة 1',
      barcode: '',
      quantity: 1,
      unitCost: 1000,
      unit: 'قطعة'
    });
  }

  ocrDetectedItems = items;
  renderOcrItemsTable();
}

function addManualOcrRow() {
  ocrDetectedItems.push({
    name: 'مادة جديدة',
    barcode: '',
    quantity: 1,
    unitCost: 1000,
    unit: 'قطعة'
  });
  renderOcrItemsTable();
}

function removeOcrRow(idx) {
  ocrDetectedItems.splice(idx, 1);
  renderOcrItemsTable();
}

function updateOcrRow(idx, field, value) {
  if (!ocrDetectedItems[idx]) return;
  if (field === 'quantity' || field === 'unitCost') {
    ocrDetectedItems[idx][field] = Number(value) || 0;
  } else {
    ocrDetectedItems[idx][field] = value;
  }
  renderOcrGrandTotal();
}

function generateOcrBarcodeForRow(idx) {
  if (!ocrDetectedItems[idx]) return;
  const newBarcode = '2026' + Math.floor(10000000 + Math.random() * 90000000);
  ocrDetectedItems[idx].barcode = newBarcode;
  renderOcrItemsTable();
}

function generateAllMissingOcrBarcodes() {
  let count = 0;
  ocrDetectedItems.forEach((item) => {
    if (!item.barcode || item.barcode.trim() === '') {
      item.barcode = '2026' + Math.floor(10000000 + Math.random() * 90000000);
      count++;
    }
  });
  renderOcrItemsTable();
  if (count > 0) {
    alert(`✔ تم إنشاء ${count} باركود فريد بنجاح للمواد التي لم يكن لها باركود!`);
  } else {
    alert('جميع المواد لديها باركودات بالفعل ✔');
  }
}

function renderOcrItemsTable() {
  const tbody = document.getElementById('rpm-itemsTableBody');
  const countBadge = document.getElementById('rpm-detectedCount');
  if (!tbody) return;

  if (ocrDetectedItems.length === 0) {
    tbody.innerHTML = `
      <tr>
        <td colspan="6" class="text-center py-16 text-slate-400 space-y-2">
          <span class="block text-3xl">📸</span>
          <span class="font-bold text-sm block">التقط صورة للوصل أو ارفع صورة ليتم استخراج المواد وتوليد الباركودات هنا تلقائياً</span>
          <span class="text-xs text-slate-500">يمكنك تعديل أي مادة أو توليد باركود لها بنقرة زر واحدة</span>
        </td>
      </tr>
    `;
    if (countBadge) countBadge.innerText = '0 مادة';
    renderOcrGrandTotal();
    return;
  }

  tbody.innerHTML = '';
  ocrDetectedItems.forEach((item, idx) => {
    const rowTotal = item.quantity * item.unitCost;
    const hasBarcode = Boolean(item.barcode && item.barcode.trim().length > 0);
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40 transition';
    tr.innerHTML = `
      <td class="p-3">
        <input type="text" value="${item.name}" oninput="updateOcrRow(${idx}, 'name', this.value)" class="w-full bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl px-3 py-2 text-xs font-bold text-slate-900 dark:text-white focus:border-purple-500">
      </td>
      <td class="p-3">
        <div class="space-y-1">
          <input type="text" value="${item.barcode || ''}" placeholder="أدخل باركود المادة..." oninput="updateOcrRow(${idx}, 'barcode', this.value)" class="w-full bg-slate-50 dark:bg-slate-900 border ${hasBarcode ? 'border-slate-200 dark:border-slate-800' : 'border-amber-400/60 dark:border-amber-500/40'} rounded-xl px-3 py-1.5 text-xs font-mono font-bold text-slate-800 dark:text-white">
          ${!hasBarcode ? `
            <button type="button" onclick="generateOcrBarcodeForRow(${idx})" class="w-full py-1 bg-amber-500/10 hover:bg-amber-500/20 text-amber-600 dark:text-amber-400 border border-amber-500/30 rounded-lg text-[10px] font-black flex items-center justify-center gap-1 transition">
              <span>⚡</span>
              <span>توليد باركود تلقائي</span>
            </button>
          ` : `
            <div class="flex items-center justify-between px-1">
              <span class="text-[10px] text-emerald-500 font-bold flex items-center gap-0.5">✔ باركود جاهز</span>
              <button type="button" onclick="generateOcrBarcodeForRow(${idx})" class="text-[10px] text-slate-400 hover:text-amber-500 font-semibold underline">توليد جديد ⚡</button>
            </div>
          `}
        </div>
      </td>
      <td class="p-3 text-center">
        <input type="number" min="1" value="${item.quantity}" oninput="updateOcrRow(${idx}, 'quantity', this.value)" class="w-20 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl px-2 py-2 text-xs font-black text-center text-sky-600 font-mono">
      </td>
      <td class="p-3 text-center">
        <input type="number" min="0" value="${item.unitCost}" oninput="updateOcrRow(${idx}, 'unitCost', this.value)" class="w-28 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl px-2 py-2 text-xs font-black text-center text-emerald-600 font-mono">
      </td>
      <td class="p-3 text-center font-mono font-black text-slate-900 dark:text-white text-sm" id="rpm-rowTotal-${idx}">
        ${rowTotal.toLocaleString()} د.ع
      </td>
      <td class="p-3 text-center">
        <button onclick="removeOcrRow(${idx})" class="w-8 h-8 rounded-xl bg-rose-50 hover:bg-rose-100 text-rose-600 text-xs font-black flex items-center justify-center transition" title="حذف السطر">✕</button>
      </td>
    `;
    tbody.appendChild(tr);
  });

  if (countBadge) countBadge.innerText = `${ocrDetectedItems.length} مادة`;
  renderOcrGrandTotal();
}

function renderOcrGrandTotal() {
  let grandTotal = 0;
  ocrDetectedItems.forEach((item, idx) => {
    const rowTotal = item.quantity * item.unitCost;
    grandTotal += rowTotal;
    const rowTotalEl = document.getElementById(`rpm-rowTotal-${idx}`);
    if (rowTotalEl) rowTotalEl.innerText = `${rowTotal.toLocaleString()} د.ع`;
  });
  const grandEl = document.getElementById('rpm-grandTotal');
  if (grandEl) grandEl.innerText = `${grandTotal.toLocaleString()} د.ع`;
}

function transferOcrItemsToPurchase() {
  if (ocrDetectedItems.length === 0) {
    alert('لا توجد مواد ممسوحة لنقلها!');
    return;
  }

  // Switch to purchase tab
  switchTab('purchase');

  // Push items into purInvoiceItems
  ocrDetectedItems.forEach(item => {
    // Find matching warehouse product for oldCost
    const match = (state.products || []).find(p => 
      (item.barcode && p.barcode === item.barcode) ||
      (p.name && p.name.toLowerCase() === item.name.toLowerCase())
    );

    const oldCost = match ? Number(match.cost || match.Cost || 0) : 0;

    purInvoiceItems.push({
      productId: match ? (match.id || match.Id) : undefined,
      name: item.name,
      barcode: item.barcode || (match ? match.barcode : ''),
      packagingText: `${item.quantity} قطعة`,
      inputMode: 'piece',
      quantity: item.quantity,
      cartonsCount: 0,
      itemsPerCarton: 1,
      cartonPurchasePrice: 0,
      unitCost: item.unitCost,
      displayNewCost: item.unitCost,
      oldCost: oldCost,
      totalPrice: item.quantity * item.unitCost,
      unit: 'قطعة'
    });
  });

  renderPurchaseInvoiceTable();
  closeReceiptPhotoModal();

  alert(`✔ تم نقل ${ocrDetectedItems.length} مادة بنجاح إلى جدول وصل الشراء! يمكنك الآن اختيار المندوب وتأكيد حفظ الوصل.`);
}

async function loadUsers() {
  const res = await callBackend('get_users');
  if (!res || !res.success) return;

  const grid = document.getElementById('usersGrid');
  if (!grid) return;

  grid.innerHTML = '';
  (res.users || []).forEach(u => {
    const card = document.createElement('div');
    card.className = 'sh-card p-5';
    card.innerHTML = `
      <div class="flex items-center gap-3 mb-2">
        <div class="w-10 h-10 rounded-2xl bg-sky-100 dark:bg-sky-950/60 text-sky-600 flex items-center justify-center font-bold text-lg">👤</div>
        <div>
          <h4 class="font-black text-sm">${u.fullName}</h4>
          <span class="text-[10px] text-slate-400 font-mono">@${u.username} (${u.role})</span>
        </div>
      </div>
      <div class="flex items-center justify-between text-xs pt-2 border-t border-slate-100 dark:border-slate-800">
        <span class="text-slate-400">الحالة:</span>
        <span class="font-bold ${u.isActive ? 'text-emerald-600' : 'text-rose-500'}">${u.isActive ? 'نشط ومفعل ✔' : 'معطل ✕'}</span>
      </div>
    `;
    grid.appendChild(card);
  });
}

// ========================================================
// REP CLOUD ORDERS & LIVE NOTIFICATION SYSTEM
// ========================================================
let activeOrderModalData = null;
let knownPendingOrderIds = new Set();
let isFirstRepOrdersLoad = true;

// Synthesize pleasant two-tone notification sound (Web Audio API)
function playOrderNotificationSound() {
  try {
    const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    const now = audioCtx.currentTime;

    // Tone 1 (High bell chime)
    const osc1 = audioCtx.createOscillator();
    const gain1 = audioCtx.createGain();
    osc1.type = 'sine';
    osc1.frequency.setValueAtTime(587.33, now); // D5
    osc1.frequency.exponentialRampToValueAtTime(880.00, now + 0.15); // A5
    gain1.gain.setValueAtTime(0.35, now);
    gain1.gain.exponentialRampToValueAtTime(0.01, now + 0.5);
    osc1.connect(gain1);
    gain1.connect(audioCtx.destination);
    osc1.start(now);
    osc1.stop(now + 0.5);

    // Tone 2 (Sparkle chime)
    const osc2 = audioCtx.createOscillator();
    const gain2 = audioCtx.createGain();
    osc2.type = 'sine';
    osc2.frequency.setValueAtTime(1174.66, now + 0.18); // D6
    gain2.gain.setValueAtTime(0.4, now + 0.18);
    gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.8);
    osc2.connect(gain2);
    gain2.connect(audioCtx.destination);
    osc2.start(now + 0.18);
    osc2.stop(now + 0.8);
  } catch (e) {
    console.log('[Audio] Notification sound skipped:', e);
  }
}

// Show floating interactive notification card
function showOrderNotificationToast(order) {
  const container = document.getElementById('orderNotificationToastContainer');
  if (!container) return;

  const toastId = 'toast_' + Date.now() + '_' + Math.random().toString(36).substr(2, 4);
  const toast = document.createElement('div');
  toast.id = toastId;
  toast.className = 'pointer-events-auto sh-card p-4 rounded-2xl shadow-2xl border-2 border-amber-500/80 bg-slate-900/95 text-white animate-in slide-in-from-top duration-300 flex items-start gap-3 backdrop-blur-md';
  
  toast.innerHTML = `
    <div class="w-10 h-10 rounded-2xl bg-amber-500 text-slate-950 flex items-center justify-center font-black text-xl flex-shrink-0 animate-bounce">
      🔔
    </div>
    <div class="flex-1 min-w-0">
      <div class="flex items-center justify-between gap-2 mb-1">
        <h4 class="font-black text-sm text-amber-400">وصلت طلبية جديدة الآن!</h4>
        <span class="text-[10px] font-mono text-slate-400">${order.orderNumber}</span>
      </div>
      <p class="text-xs font-bold text-slate-200 truncate">🏬 ${order.marketName || 'ماركت'}</p>
      <div class="flex items-center justify-between text-xs mt-1 text-slate-300">
        <span>👤 المندوب: <b class="text-sky-400">${order.representativeName || 'عام'}</b></span>
        <span class="font-black text-emerald-400">${Number(order.totalAmount).toLocaleString()} د.ع</span>
      </div>
      <div class="flex items-center gap-2 mt-3 pt-2 border-t border-slate-800">
        <button onclick="openRepOrderFromToast('${order.id}', '${toastId}')" class="flex-1 py-1.5 px-3 bg-amber-500 hover:bg-amber-400 text-slate-950 font-black text-xs rounded-xl shadow-md flex items-center justify-center gap-1">
          <span>🧾</span>
          <span>فتح الوصل والتجهيز</span>
        </button>
        <button onclick="dismissOrderToast('${toastId}')" class="py-1.5 px-2.5 bg-slate-800 hover:bg-slate-700 text-slate-300 font-bold text-xs rounded-xl">
          إغلاق
        </button>
      </div>
    </div>
  `;

  container.prepend(toast);

  // Auto remove after 14 seconds
  setTimeout(() => {
    dismissOrderToast(toastId);
  }, 14000);
}

function dismissOrderToast(toastId) {
  const el = document.getElementById(toastId);
  if (el) {
    el.classList.add('animate-out', 'fade-out', 'slide-out-to-top', 'duration-200');
    setTimeout(() => el.remove(), 200);
  }
}

function openRepOrderFromToast(orderId, toastId) {
  dismissOrderToast(toastId);
  switchTab('repOrders');
  openRepOrderInvoiceModal(orderId);
}

async function loadRepOrders(triggerAlerts = true) {
  const res = await callBackend('get_supplier_orders');
  if (!res || !res.success) return;

  const orders = res.orders || [];
  const pendingOrders = orders.filter(o => o.status === 'Pending');
  const pendingCount = pendingOrders.length;

  // Check for newly arrived pending orders
  if (triggerAlerts && !isFirstRepOrdersLoad) {
    pendingOrders.forEach(o => {
      if (!knownPendingOrderIds.has(o.id)) {
        playOrderNotificationSound();
        showOrderNotificationToast(o);
      }
    });
  }

  // Update known set
  knownPendingOrderIds = new Set(pendingOrders.map(o => o.id));
  isFirstRepOrdersLoad = false;

  const sideBadge = document.getElementById('repBadgeSidebar');
  const bellBadge = document.getElementById('repBellBadge');
  const notifBadge = document.getElementById('repNotificationCountBadge');

  if (pendingCount > 0) {
    if (sideBadge) { sideBadge.innerText = pendingCount; sideBadge.classList.remove('hidden'); }
    if (bellBadge) { bellBadge.classList.remove('hidden'); }
    if (notifBadge) { notifBadge.innerText = pendingCount; notifBadge.classList.remove('hidden'); }
  } else {
    if (sideBadge) sideBadge.classList.add('hidden');
    if (bellBadge) bellBadge.classList.add('hidden');
    if (notifBadge) notifBadge.classList.add('hidden');
  }

  const tbody = document.getElementById('repOrdersTableBody');
  if (!tbody) return;

  if (orders.length === 0) {
    tbody.innerHTML = `<tr><td colspan="8" class="text-center py-12 text-slate-400 font-bold">لا توجد طلبيات سحابية واردة حتى الآن</td></tr>`;
    return;
  }

  tbody.innerHTML = '';
  orders.forEach(o => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40 transition';
    
    let badgeClass = 'bg-amber-100 text-amber-700 dark:bg-amber-950/60 dark:text-amber-400';
    let statusText = 'قيد الانتظار';
    if (o.status === 'InPreparation') {
      badgeClass = 'bg-sky-100 text-sky-700 dark:bg-sky-950/60 dark:text-sky-400';
      statusText = 'جاري التجهيز';
    } else if (o.status === 'Delivered') {
      badgeClass = 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400';
      statusText = 'تم التسليم ✔';
    } else if (o.status === 'Cancelled') {
      badgeClass = 'bg-rose-100 text-rose-700 dark:bg-rose-950/60 dark:text-rose-400';
      statusText = 'ملغية ✕';
    }

    tr.innerHTML = `
      <td class="p-3 font-mono font-bold text-sky-500">${o.orderNumber}</td>
      <td class="p-3 font-bold">
        <div>${o.marketName || 'ماركت'}</div>
        <div class="text-[10px] text-slate-400 font-normal">${o.marketPhone || ''} ${o.marketCity ? '• ' + o.marketCity : ''}</div>
      </td>
      <td class="p-3 font-bold text-slate-600 dark:text-slate-300">👤 ${o.representativeName || '--'}</td>
      <td class="p-3 text-center font-bold text-amber-500">${o.itemsCount} مواد</td>
      <td class="p-3 text-center font-black text-slate-800 dark:text-white">${Number(o.totalAmount).toLocaleString()} د.ع</td>
      <td class="p-3 text-center text-slate-400 text-[11px] font-mono">${o.date}</td>
      <td class="p-3 text-center"><span class="px-2.5 py-1 rounded-full text-[10px] font-black ${badgeClass}">${statusText}</span></td>
      <td class="p-3 text-center">
        <div class="flex items-center justify-center gap-1.5">
          <button onclick="openRepOrderInvoiceModal('${o.id}')" class="px-3 py-1.5 bg-amber-500 hover:bg-amber-600 text-white font-bold text-xs rounded-xl shadow-sm flex items-center gap-1">
            <span>🧾</span>
            <span>فتح الوصل وتعديل الأسعار</span>
          </button>
          <button onclick="updateOrderStatus('${o.id}', 'Delivered')" class="px-2.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs rounded-xl shadow-sm" title="تسليم سريع">
            ✔
          </button>
        </div>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

// ========================================================
// CREATE REP ACCOUNT MODAL
// ========================================================
function openCreateRepAccountModal() {
  document.getElementById('newRep-name').value = '';
  document.getElementById('newRep-company').value = '';
  document.getElementById('newRep-phone').value = '';
  document.getElementById('newRep-balance').value = '0';
  document.getElementById('newRep-address').value = '';
  document.getElementById('createRepAccountModal')?.classList.remove('hidden');
}

function closeCreateRepAccountModal() {
  document.getElementById('createRepAccountModal')?.classList.add('hidden');
}

async function saveNewRepAccount() {
  const name = document.getElementById('newRep-name')?.value.trim();
  const company = document.getElementById('newRep-company')?.value.trim();
  if (!name || !company) {
    alert('يرجى كتابة اسم المندوب واسم الشركة أولاً!');
    return;
  }

  const payload = {
    name: name,
    company: company,
    phone: document.getElementById('newRep-phone')?.value.trim() || '',
    balance: Number(document.getElementById('newRep-balance')?.value || 0),
    address: document.getElementById('newRep-address')?.value.trim() || ''
  };

  const res = await callBackend('create_rep_account', payload);
  if (res && res.success) {
    alert(`✔ تم إنشاء حساب المندوب (${name}) بنجاح!`);
    closeCreateRepAccountModal();
    await loadSuppliersList();
    await loadSuppliers();
  }
}

// ========================================================
// INTERACTIVE ORDER INVOICE MODAL (PRICE & QTY EDITOR)
// ========================================================
async function openRepOrderInvoiceModal(orderId) {
  const res = await callBackend('get_rep_order_details', { id: orderId });
  if (!res || !res.success || !res.order) {
    alert('تعذر تحميل تفاصيل الوصل');
    return;
  }

  activeOrderModalData = res.order;
  const o = res.order;

  document.getElementById('oim-orderNumber').innerText = o.orderNumber;
  document.getElementById('oim-marketName').innerText = o.marketName || 'ماركت';
  document.getElementById('oim-marketContact').innerText = `${o.marketPhone || '--'} | ${o.marketAddress || 'العراق'}`;
  document.getElementById('oim-repName').innerText = o.representativeName || o.supplierName || 'مندوب عام';
  document.getElementById('oim-notes').value = o.notes || '';
  document.getElementById('oim-statusSelect').value = o.status || 'Pending';

  renderOrderModalItemsTable();
  recalcOrderModalInvoice();

  document.getElementById('orderInvoiceModal')?.classList.remove('hidden');
}

function closeOrderInvoiceModal() {
  document.getElementById('orderInvoiceModal')?.classList.add('hidden');
  activeOrderModalData = null;
}

function renderOrderModalItemsTable() {
  const tbody = document.getElementById('oim-itemsTableBody');
  if (!tbody || !activeOrderModalData) return;

  tbody.innerHTML = '';
  (activeOrderModalData.items || []).forEach((item, idx) => {
    const tr = document.createElement('tr');
    tr.id = `oim-row-${item.id}`;
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40';
    tr.innerHTML = `
      <td class="p-2.5">
        <div class="font-bold text-slate-800 dark:text-white text-xs">${item.productName}</div>
        <div class="text-[10px] text-slate-400 font-mono">${item.barcode || '--'}</div>
      </td>
      <td class="p-2.5 text-center">
        <input type="number" step="1" min="1" value="${item.quantity}" oninput="updateOrderModalItemValue('${item.id}', 'quantity', this.value)" class="w-20 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg px-2 py-1 text-center font-bold text-sky-500">
      </td>
      <td class="p-2.5 text-center">
        <input type="number" step="250" min="0" value="${item.unitPrice}" oninput="updateOrderModalItemValue('${item.id}', 'unitPrice', this.value)" class="w-24 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg px-2 py-1 text-center font-bold text-emerald-600">
      </td>
      <td class="p-2.5 text-center font-black text-slate-800 dark:text-white text-xs" id="oim-item-total-${item.id}">
        ${Number(item.quantity * item.unitPrice).toLocaleString()} د.ع
      </td>
      <td class="p-2.5 text-center">
        <button onclick="removeOrderModalItem('${item.id}')" class="text-rose-500 hover:text-rose-600 font-black text-xs">🗑</button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

function updateOrderModalItemValue(itemId, field, value) {
  if (!activeOrderModalData) return;
  const item = activeOrderModalData.items.find(i => i.id === itemId);
  if (item) {
    item[field] = Math.max(0, Number(value) || 0);
    const totalEl = document.getElementById(`oim-item-total-${itemId}`);
    if (totalEl) {
      totalEl.innerText = `${Number(item.quantity * item.unitPrice).toLocaleString()} د.ع`;
    }
    recalcOrderModalInvoice();
  }
}

function removeOrderModalItem(itemId) {
  if (!activeOrderModalData) return;
  activeOrderModalData.items = activeOrderModalData.items.filter(i => i.id !== itemId);
  renderOrderModalItemsTable();
  recalcOrderModalInvoice();
}

function recalcOrderModalInvoice() {
  if (!activeOrderModalData) return;

  const currentOrderTotal = activeOrderModalData.items.reduce((sum, i) => sum + (i.quantity * i.unitPrice), 0);
  const prevDebt = Number(activeOrderModalData.previousDebt || 0);
  const grandTotal = prevDebt + currentOrderTotal;

  document.getElementById('oim-previousDebt').innerText = `${Number(prevDebt).toLocaleString()} د.ع`;
  document.getElementById('oim-currentTotal').innerText = `${Number(currentOrderTotal).toLocaleString()} د.ع`;
  document.getElementById('oim-grandTotal').innerText = `${Number(grandTotal).toLocaleString()} د.ع`;
}

async function syncCloudNow() {
  const btn = document.getElementById('syncCloudBtn');
  if (btn) btn.innerHTML = '<span>⏳</span><span>جارٍ المزامنة مع السحابة...</span>';

  const res = await callBackend('sync_cloud_orders');
  if (res && res.success) {
    alert('✔ تمت المزامنة مع السحابة وتحديث كافة طلبيات المناديب بنجاح!');
    await loadRepOrders();
    await loadInventory();
    await loadDashboard();
  }
  if (btn) btn.innerHTML = '<span>🔄</span><span>مزامنة السحابة والطلبيات</span>';
}

async function acceptRepOrderDirectly(orderId) {
  if (confirm('هل أنت متأكد من قبول واعتماد هذه الطلبية وخصم موادها من المخزن فوراً؟')) {
    const res = await callBackend('accept_rep_order', { id: orderId, status: 'Delivered' });
    if (res && res.success) {
      alert('✔ تم قبول الطلبية، خصم المواد من المخزن، ومزامنة تطبيق المندوب بنجاح!');
      await loadRepOrders();
      await loadInventory();
      await loadDashboard();
    }
  }
}

async function saveEditedRepOrder() {
  if (!activeOrderModalData) return;

  const status = document.getElementById('oim-statusSelect')?.value || 'InPreparation';
  const payload = {
    id: activeOrderModalData.id,
    status: status,
    notes: document.getElementById('oim-notes')?.value || '',
    items: activeOrderModalData.items.map(i => ({
      id: i.id,
      quantity: i.quantity,
      unitPrice: i.unitPrice
    }))
  };

  // If status is Delivered, also deduct stock
  let res;
  if (status === 'Delivered') {
    res = await callBackend('accept_rep_order', payload);
  } else {
    res = await callBackend('save_rep_order_items', payload);
  }

  if (res && res.success) {
    alert('✔ تم حفظ التعديلات وتحديث الوصل بنجاح!');
    closeOrderInvoiceModal();
    await loadRepOrders();
    await loadInventory();
    await loadDashboard();
  }
}

function printRepOrderInvoice() {
  if (!activeOrderModalData) return;
  window.print();
}

async function updateOrderStatus(id, status) {
  if (status === 'Delivered') {
    await acceptRepOrderDirectly(id);
  } else {
    await callBackend('save_rep_order_items', { id, status });
    await loadRepOrders();
  }
}

function openRepPortalModal() {
  document.getElementById('repModal')?.classList.remove('hidden');
}

function closeRepPortalModal() {
  document.getElementById('repModal')?.classList.add('hidden');
}

// ========================================================
// CASHIER CONTINUOUS AUTO-FOCUS MANAGEMENT
// ========================================================
document.addEventListener('click', (e) => {
  if (state.activeTab !== 'cashier') return;
  const tag = e.target.tagName;
  const isInteractiveInput = tag === 'INPUT' || tag === 'SELECT' || tag === 'TEXTAREA' || tag === 'BUTTON';
  const inModal = e.target.closest('.fixed') || e.target.closest('#productDetailModal') || e.target.closest('#orderInvoiceModal') || e.target.closest('#createRepAccountModal') || e.target.closest('#repModal');
  if (!isInteractiveInput && !inModal) {
    document.getElementById('cashierBarcodeInput')?.focus();
  }
});

// ========================================================
// SETTINGS, SYSTEM UPDATES & EXCEL BULK IMPORTER
// ========================================================
let parsedExcelProductsList = [];

async function loadSettingsInfo() {
  const res = await callBackend('get_app_info');
  if (res && res.success) {
    const verEl = document.getElementById('settingsAppVersion');
    if (verEl) verEl.innerText = res.version ? `v${res.version}` : 'v2.5.0 Pro';

    const stEl = document.getElementById('settingsStoreId');
    if (stEl) stEl.innerText = res.storeId || 'MARKET-DEFAULT-01';
  }
}

async function checkForAppUpdates() {
  alert('🔄 جاري فحص خادم التحديثات السحابي الآن...\nإذا توفر إصدار أحدث، سيبدأ التحديث التلقائي فوراً.');
  await callBackend('check_for_updates');
}

// ========================================================
// 1. SMART ROBUST EXCEL & CSV BULK IMPORTER (FOOLPROOF)
// ========================================================
async function handleExcelFileUpload(event) {
  const file = event.target.files[0];
  if (!file) return;

  const fileName = file.name.toLowerCase();
  const reader = new FileReader();

  if (fileName.endsWith('.xlsx') || fileName.endsWith('.xls')) {
    reader.onload = (e) => {
      try {
        const data = new Uint8Array(e.target.result);
        if (typeof XLSX !== 'undefined') {
          const workbook = XLSX.read(data, { type: 'array' });
          const firstSheetName = workbook.SheetNames[0];
          const worksheet = workbook.Sheets[firstSheetName];
          // Get raw 2D array of all rows & columns
          const raw2D = XLSX.utils.sheet_to_json(worksheet, { header: 1, defval: '' });
          processExcel2DArray(raw2D);
        } else {
          alert('تعذر تحميل محرك الإكسل، يرجى حفظ الملف كـ CSV وإعادة اختياره.');
        }
      } catch (err) {
        alert('حدث خطأ أثناء قراءة ملف الإكسل: ' + err.message);
      }
    };
    reader.readAsArrayBuffer(file);
  } else {
    // CSV / TXT reader
    reader.onload = (e) => {
      try {
        const text = e.target.result;
        const raw2D = parseCSVTo2DArray(text);
        processExcel2DArray(raw2D);
      } catch (err) {
        alert('حدث خطأ أثناء قراءة ملف CSV: ' + err.message);
      }
    };
    reader.readAsText(file, 'UTF-8');
  }

  // Reset file input
  event.target.value = '';
}

function parseCSVTo2DArray(text) {
  const lines = text.split(/\r\n|\n/).filter(l => l.trim().length > 0);
  return lines.map(line => {
    return line.split(',').map(cell => cell.replace(/^["']|["']$/g, '').trim());
  });
}

function processExcel2DArray(raw2D) {
  if (!raw2D || raw2D.length === 0) {
    alert('الملف فارغ ولا يحتوي على أي بيانات!');
    return;
  }

  // 1. Find Header Row index or determine column mapping
  let headerRowIndex = -1;
  let nameCol = -1, barcodeCol = -1, catCol = -1, costCol = -1, priceCol = -1, wholesaleCol = -1;
  let cartonCostCol = -1, cartonPriceCol = -1, piecesPerCartonCol = -1, cartonsCol = -1, stockCol = -1, supCol = -1;

  for (let r = 0; r < Math.min(6, raw2D.length); r++) {
    const row = raw2D[r].map(c => String(c).toLowerCase().trim());
    for (let c = 0; c < row.length; c++) {
      const val = row[c];
      if (val.includes('اسم') || val.includes('مادة') || val.includes('صنف') || val.includes('منتج') || val.includes('name') || val.includes('item') || val.includes('title')) {
        headerRowIndex = r;
        break;
      }
    }
    if (headerRowIndex !== -1) break;
  }

  if (headerRowIndex !== -1) {
    const headerRow = raw2D[headerRowIndex].map(c => String(c).toLowerCase().trim());
    headerRow.forEach((h, idx) => {
      if (h.includes('اسم') || h.includes('مادة') || h.includes('صنف') || h.includes('منتج') || h.includes('name') || h.includes('item')) nameCol = idx;
      else if (h.includes('باركود') || h.includes('barcode') || h.includes('كود') || h.includes('code')) barcodeCol = idx;
      else if (h.includes('تصنيف') || h.includes('قسم') || h.includes('category')) catCol = idx;
      else if (h.includes('شراء كرتون') || h.includes('cartoncost') || h.includes('carton_cost')) cartonCostCol = idx;
      else if (h.includes('بيع كرتون') || h.includes('cartonprice') || h.includes('carton_price')) cartonPriceCol = idx;
      else if (h.includes('تكلفة') || h.includes('شراء') || h.includes('cost') || h.includes('buy')) costCol = idx;
      else if (h.includes('مفرد') || h.includes('بيع') || h.includes('price') || h.includes('sell')) priceCol = idx;
      else if (h.includes('جملة') || h.includes('wholesale')) wholesaleCol = idx;
      else if (h.includes('قطع بالكرتون') || h.includes('تعبئة') || h.includes('pieces')) piecesPerCartonCol = idx;
      else if (h.includes('كراتين') || h.includes('عدد كراتين') || h.includes('cartons')) cartonsCol = idx;
      else if (h.includes('رصيد') || h.includes('كمية') || h.includes('stock') || h.includes('qty')) stockCol = idx;
      else if (h.includes('مندوب') || h.includes('مورد') || h.includes('شركة') || h.includes('supplier')) supCol = idx;
    });
  }

  // Default fallback column mapping if no explicit header row was found
  if (nameCol === -1) nameCol = 0;
  if (barcodeCol === -1 && raw2D[0].length > 1) barcodeCol = 1;
  if (catCol === -1 && raw2D[0].length > 2) catCol = 2;
  if (costCol === -1 && raw2D[0].length > 3) costCol = 3;
  if (priceCol === -1 && raw2D[0].length > 4) priceCol = 4;

  const startRow = headerRowIndex !== -1 ? headerRowIndex + 1 : 0;
  parsedExcelProductsList = [];
  let barcodeCounter = Date.now().toString().slice(-6);

  for (let r = startRow; r < raw2D.length; r++) {
    const row = raw2D[r];
    if (!row || row.length === 0) continue;

    // Extract product name from designated column or first non-empty text cell
    let name = nameCol !== -1 && row[nameCol] ? String(row[nameCol]).trim() : '';
    if (!name) {
      for (let c = 0; c < row.length; c++) {
        const text = String(row[c] || '').trim();
        if (text && isNaN(text) && text.length > 1) {
          name = text;
          break;
        }
      }
    }
    if (!name) continue; // Skip completely blank row

    // Extract or generate barcode
    let barcode = barcodeCol !== -1 && row[barcodeCol] ? String(row[barcodeCol]).replace(/[^0-9a-zA-Z]/g, '').trim() : '';
    if (!barcode) {
      barcode = '200245' + String(barcodeCounter++).padStart(6, '0');
    }

    const category = catCol !== -1 && row[catCol] ? String(row[catCol]).trim() : 'عام';
    const supplierName = supCol !== -1 && row[supCol] ? String(row[supCol]).trim() : '';

    const cost = costCol !== -1 ? parseFloat(String(row[costCol]).replace(/[^0-9.]/g, '')) || 0 : 0;
    let price = priceCol !== -1 ? parseFloat(String(row[priceCol]).replace(/[^0-9.]/g, '')) || 0 : 0;
    if (price === 0 && cost > 0) price = cost * 1.2;

    const wholesalePrice = wholesaleCol !== -1 ? parseFloat(String(row[wholesaleCol]).replace(/[^0-9.]/g, '')) || price : price;
    const cartonPurchasePrice = cartonCostCol !== -1 ? parseFloat(String(row[cartonCostCol]).replace(/[^0-9.]/g, '')) || 0 : 0;
    const cartonSellingPrice = cartonPriceCol !== -1 ? parseFloat(String(row[cartonPriceCol]).replace(/[^0-9.]/g, '')) || 0 : 0;

    const piecesPerCarton = piecesPerCartonCol !== -1 ? parseInt(String(row[piecesPerCartonCol]).replace(/[^0-9]/g, '')) || 1 : 1;
    const cartonsCount = cartonsCol !== -1 ? parseInt(String(row[cartonsCol]).replace(/[^0-9]/g, '')) || 0 : 0;
    let stockQuantity = stockCol !== -1 ? parseInt(String(row[stockCol]).replace(/[^0-9]/g, '')) || (cartonsCount * piecesPerCarton) : (cartonsCount * piecesPerCarton);
    if (stockQuantity <= 0 && cartonsCount > 0) stockQuantity = cartonsCount * piecesPerCarton;

    parsedExcelProductsList.push({
      name,
      barcode,
      category: category || 'عام',
      supplierName,
      cost,
      price,
      wholesalePrice,
      cartonPurchasePrice,
      cartonSellingPrice,
      piecesPerCarton: Math.max(1, piecesPerCarton),
      cartonsCount: Math.max(0, cartonsCount),
      stockQuantity: stockQuantity,
      minStockAlert: 5
    });
  }

  if (parsedExcelProductsList.length === 0) {
    alert('لم يتم العثور على أي صفوف صالحة للمواد داخل الملف!\nيرجى التأكد من أن الملف يحتوي على أسماء المواد.');
    return;
  }

  // Update Settings preview summary box
  const statusBox = document.getElementById('excelImportStatusBox');
  const countEl = document.getElementById('excelParsedCount');
  if (statusBox) statusBox.classList.remove('hidden');
  if (countEl) countEl.innerText = `${parsedExcelProductsList.length} مادة جاهزة للاستيراد`;

  // Open Preview Modal
  openExcelPreviewModal();
}

function openExcelPreviewModal() {
  const modal = document.getElementById('excelPreviewModal');
  const summary = document.getElementById('epm-summary');
  if (summary) summary.innerText = `تم اكتشاف (${parsedExcelProductsList.length}) مادة جاهزة للحفظ في المخزن`;
  renderExcelPreviewTable(parsedExcelProductsList);
  if (modal) modal.classList.remove('hidden');
}

function closeExcelPreviewModal() {
  document.getElementById('excelPreviewModal')?.classList.add('hidden');
}

function renderExcelPreviewTable(list) {
  const tbody = document.getElementById('epm-tableBody');
  if (!tbody) return;

  tbody.innerHTML = '';
  list.slice(0, 100).forEach((p, idx) => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/50';
    tr.innerHTML = `
      <td class="p-2.5 text-center text-slate-400 font-bold">${idx + 1}</td>
      <td class="p-2.5 font-bold text-slate-800 dark:text-white font-sans">${p.name}</td>
      <td class="p-2.5 text-sky-500 font-bold">${p.barcode}</td>
      <td class="p-2.5 font-sans">${p.category}</td>
      <td class="p-2.5 text-center text-slate-500">${Number(p.cost).toLocaleString()}</td>
      <td class="p-2.5 text-center text-emerald-500 font-bold">${Number(p.price).toLocaleString()}</td>
      <td class="p-2.5 text-center text-blue-500">${Number(p.wholesalePrice).toLocaleString()}</td>
      <td class="p-2.5 text-center font-bold">${p.stockQuantity}</td>
    `;
    tbody.appendChild(tr);
  });
}

function filterExcelPreviewTable() {
  const q = (document.getElementById('epm-search')?.value || '').toLowerCase().trim();
  const filtered = parsedExcelProductsList.filter(p => 
    p.name.toLowerCase().includes(q) || p.barcode.includes(q) || p.category.toLowerCase().includes(q)
  );
  renderExcelPreviewTable(filtered);
}

async function confirmExcelImportFromModal() {
  closeExcelPreviewModal();
  await confirmExcelImport();
}

async function confirmExcelImport() {
  if (parsedExcelProductsList.length === 0) {
    alert('لا توجد مواد محملة للاستيراد.');
    return;
  }

  const btn = document.getElementById('btnConfirmExcelImport');
  if (btn) {
    btn.disabled = true;
    btn.innerText = '⏳ جاري الحفظ والتوريد للمخزن...';
  }

  const res = await callBackend('import_excel_products', { products: parsedExcelProductsList });

  if (btn) {
    btn.disabled = false;
    btn.innerText = '✔ حفظ وتوريد المواد إلى المخزن الآن';
  }

  if (res && res.success) {
    alert(`🎉 تم استيراد وتحديث (${res.importedCount || parsedExcelProductsList.length}) مادة بنجاح في المخزن وقاعدة البيانات!`);
    parsedExcelProductsList = [];
    document.getElementById('excelImportStatusBox')?.classList.add('hidden');
    
    await loadInventory();
    await loadProducts();
    await loadDashboard();
  } else {
    alert('حدث خطأ أثناء حفظ المواد: ' + (res?.message || 'خطأ غير معروف'));
  }
}

function downloadExcelSampleTemplate() {
  const headers = [
    'اسم المادة',
    'الباركود',
    'التصنيف',
    'سعر الشراء (التكلفة)',
    'سعر المفرد',
    'سعر الجملة',
    'سعر شراء الكرتون',
    'سعر بيع الكرتون',
    'عدد القطع بالكرتون',
    'عدد الكراتين',
    'الرصيد الكلي بالقطع',
    'اسم المندوب'
  ];

  const sampleRows = [
    ['عصير راني برتقال 240مل', '200245000101', 'مشروبات وعصائر', '400', '500', '450', '9600', '11000', '24', '10', '240', 'شركة الروان'],
    ['بسكويت دايجستف 400غ', '200245000102', 'بسكويت وحلويات', '1250', '1500', '1400', '15000', '17000', '12', '5', '60', 'المندوب أحمد'],
    ['شاي ليبتون 100 خيط', '200245000103', 'شاي وقهوة', '3000', '3500', '3300', '36000', '40000', '12', '8', '96', 'شركة الخيرات']
  ];

  let csvContent = '\uFEFF'; // UTF-8 BOM for Arabic support in Excel
  csvContent += headers.join(',') + '\r\n';
  sampleRows.forEach(r => {
    csvContent += r.map(cell => `"${cell}"`).join(',') + '\r\n';
  });

  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.setAttribute('href', url);
  link.setAttribute('download', 'نموذج_استيراد_مواد_7amoPOS.csv');
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
}

async function backupDatabase() {
  const res = await callBackend('backup_database');
  if (res && res.success) {
    alert(`✔ تم حفظ نسخة احتياطية من قاعدة البيانات بنجاح!\nالمسار: ${res.backupPath}`);
  } else {
    alert('تعذر أخذ نسخة احتياطية: ' + (res?.message || 'خطأ غير معروف'));
  }
}

// ========================================================
// 2. CUSTOMERS & CREDIT DEBTS (الزبائن وحسابات الآجل)
// ========================================================
let customersList = [];
let selectedCustomerForPayment = null;

async function loadCustomers() {
  const res = await callBackend('get_customers');
  if (res && res.success) {
    customersList = res.customers || [];
    renderCustomersTable(customersList);

    const totalDebts = customersList.reduce((acc, c) => acc + (c.totalDebt || 0), 0);
    const totalDebtsEl = document.getElementById('cust-totalDebts');
    if (totalDebtsEl) totalDebtsEl.innerText = `${Number(totalDebts).toLocaleString()} د.ع`;

    const totalCountEl = document.getElementById('cust-totalCount');
    if (totalCountEl) totalCountEl.innerText = `${customersList.length} زبون`;
  }
}

function renderCustomersTable(list) {
  const tbody = document.getElementById('customersTableBody');
  if (!tbody) return;

  if (list.length === 0) {
    tbody.innerHTML = '<tr><td colspan="6" class="text-center py-8 text-slate-400 font-bold">لا يوجد زبائن أو ديون مسجلة حالياً</td></tr>';
    return;
  }

  tbody.innerHTML = '';
  list.forEach((c, idx) => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40 transition';
    tr.innerHTML = `
      <td class="p-3 text-center text-slate-400 font-bold">${idx + 1}</td>
      <td class="p-3 font-bold text-slate-800 dark:text-white">${c.customerName}</td>
      <td class="p-3 font-mono text-slate-500">${c.phone || 'بدون هاتف'}</td>
      <td class="p-3 text-center font-black font-mono text-rose-500">${Number(c.totalDebt).toLocaleString()} د.ع</td>
      <td class="p-3 text-center text-slate-400 font-mono text-[11px]">${c.lastDebtDate || '--'}</td>
      <td class="p-3 text-center">
        <button onclick="openPayCustomerDebtModal('${c.customerName}', ${c.totalDebt})" class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-[11px] rounded-lg shadow-sm">
          💵 سداد دين
        </button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

function filterCustomersTable() {
  const q = (document.getElementById('cust-searchInput')?.value || '').toLowerCase().trim();
  const filtered = customersList.filter(c => 
    c.customerName.toLowerCase().includes(q) || (c.phone && c.phone.includes(q))
  );
  renderCustomersTable(filtered);
}

function openAddCustomerDebtModal() {
  document.getElementById('acd-name').value = '';
  document.getElementById('acd-phone').value = '';
  document.getElementById('acd-amount').value = '';
  document.getElementById('acd-notes').value = '';
  document.getElementById('addCustomerDebtModal')?.classList.remove('hidden');
}

function closeAddCustomerDebtModal() {
  document.getElementById('addCustomerDebtModal')?.classList.add('hidden');
}

async function saveNewCustomerDebt() {
  const name = document.getElementById('acd-name')?.value.trim();
  const phone = document.getElementById('acd-phone')?.value.trim();
  const amount = parseFloat(document.getElementById('acd-amount')?.value) || 0;
  const notes = document.getElementById('acd-notes')?.value.trim();

  if (!name || amount <= 0) {
    alert('يرجى إدخال اسم الزبون ومبلغ الدين بشكل صحيح!');
    return;
  }

  const res = await callBackend('add_customer_debt', { name, phone, amount, notes });
  if (res && res.success) {
    alert('✔ تم تسجيل دين الزبون بنجاح!');
    closeAddCustomerDebtModal();
    await loadCustomers();
  }
}

function openPayCustomerDebtModal(customerName, currentDebt) {
  selectedCustomerForPayment = customerName;
  const nameEl = document.getElementById('pcd-customerName');
  const debtEl = document.getElementById('pcd-currentDebt');
  const payInput = document.getElementById('pcd-payAmount');

  if (nameEl) nameEl.innerText = customerName;
  if (debtEl) debtEl.innerText = `${Number(currentDebt).toLocaleString()} د.ع`;
  if (payInput) {
    payInput.value = currentDebt;
    payInput.max = currentDebt;
  }

  document.getElementById('payCustomerDebtModal')?.classList.remove('hidden');
}

function closePayCustomerDebtModal() {
  document.getElementById('payCustomerDebtModal')?.classList.add('hidden');
}

async function confirmCustomerPayment() {
  if (!selectedCustomerForPayment) return;
  const amount = parseFloat(document.getElementById('pcd-payAmount')?.value) || 0;
  if (amount <= 0) {
    alert('يرجى إدخال مبلغ سداد صحيح!');
    return;
  }

  const res = await callBackend('pay_customer_debt', { customerName: selectedCustomerForPayment, amount });
  if (res && res.success) {
    alert('✔ تم تسجيل سداد الزبون بنجاح!');
    closePayCustomerDebtModal();
    await loadCustomers();
  }
}

// ========================================================
// 3. STOCK AUDIT (الجرد ومطابقة الرفوف المتطورة)
// ========================================================
let auditProductsList = [];
let auditFilteredList = [];
let auditActualCounts = {}; // Track user edits: { prodId: count }

async function loadStockAudit(showAlert = false) {
  const res = await callBackend('get_stock_audit');
  if (res && res.success) {
    auditProductsList = res.products || [];
    
    // Initialize actual counts if not yet modified
    auditProductsList.forEach(p => {
      const pid = p.id || p.Id;
      if (auditActualCounts[pid] === undefined) {
        auditActualCounts[pid] = p.stockQuantity;
      }
    });

    // Populate Category Filter
    const catFilter = document.getElementById('auditCategoryFilter');
    if (catFilter) {
      const cats = [...new Set(auditProductsList.map(p => p.category).filter(Boolean))];
      const curVal = catFilter.value;
      catFilter.innerHTML = '<option value="" data-i18n="audit_all_cats">جميع التصنيفات</option>';
      cats.forEach(c => {
        catFilter.innerHTML += `<option value="${c}">${c}</option>`;
      });
      if (cats.includes(curVal)) catFilter.value = curVal;
    }

    updateAuditKPIs();
    filterAuditTable();

    // Auto focus barcode gun scanner
    setTimeout(() => {
      document.getElementById('auditBarcodeScannerInput')?.focus();
    }, 100);

    if (showAlert) alert('✔ تم تحديث قائمة جرد الرفوف بنجاح!');
  }
}

function updateAuditKPIs() {
  let matched = 0;
  let shortage = 0;
  let surplus = 0;

  auditProductsList.forEach(p => {
    const pid = p.id || p.Id;
    const actual = auditActualCounts[pid] !== undefined ? auditActualCounts[pid] : p.stockQuantity;
    const diff = actual - p.stockQuantity;
    if (diff === 0) matched++;
    else if (diff < 0) shortage++;
    else surplus++;
  });

  const totalEl = document.getElementById('auditKpiTotal');
  if (totalEl) totalEl.innerText = `${auditProductsList.length.toLocaleString()} مادة`;

  const matchedEl = document.getElementById('auditKpiMatched');
  if (matchedEl) matchedEl.innerText = `${matched.toLocaleString()} مادة`;

  const shortEl = document.getElementById('auditKpiShortage');
  if (shortEl) shortEl.innerText = `${shortage.toLocaleString()} مادة`;

  const surplusEl = document.getElementById('auditKpiSurplus');
  if (surplusEl) surplusEl.innerText = `${surplus.toLocaleString()} مادة`;
}

function handleAuditBarcodeScan(e) {
  if (e.key === 'Enter') {
    e.preventDefault();
    const barcodeInput = document.getElementById('auditBarcodeScannerInput');
    if (!barcodeInput) return;
    const code = barcodeInput.value.trim();
    if (!code) return;

    const prod = auditProductsList.find(p => (p.barcode || p.Barcode) === code || (p.name || p.Name).toLowerCase() === code.toLowerCase());
    if (prod) {
      const pid = prod.id || prod.Id;
      const isAutoInc = document.getElementById('auditAutoIncrementToggle')?.checked ?? true;
      const currentCount = auditActualCounts[pid] !== undefined ? auditActualCounts[pid] : prod.stockQuantity;
      const newCount = isAutoInc ? (currentCount + 1) : currentCount;
      auditActualCounts[pid] = newCount;

      // Show Last Scanned Banner
      const banner = document.getElementById('auditLastScannedBanner');
      if (banner) {
        document.getElementById('auditScannedName').innerText = prod.name || prod.Name;
        document.getElementById('auditScannedBarcode').innerText = prod.barcode || prod.Barcode || '--';
        document.getElementById('auditScannedCount').innerText = `${newCount} قطعة`;
        banner.classList.remove('hidden');
      }

      updateAuditKPIs();
      filterAuditTable();

      // Highlight input in table if visible
      setTimeout(() => {
        const inputEl = document.getElementById(`audit-input-${pid}`);
        if (inputEl) {
          inputEl.classList.add('ring-2', 'ring-amber-500');
          setTimeout(() => inputEl.classList.remove('ring-2', 'ring-amber-500'), 1200);
        }
      }, 50);
    } else {
      alert(`⚠️ الباركود [${code}] غير مسجل ضمن مواد المخزن!`);
    }

    barcodeInput.value = '';
    barcodeInput.focus();
  }
}

function changeAuditCount(pid, delta) {
  const prod = auditProductsList.find(p => (p.id || p.Id) === pid);
  if (!prod) return;
  const cur = auditActualCounts[pid] !== undefined ? auditActualCounts[pid] : prod.stockQuantity;
  const next = Math.max(0, cur + delta);
  auditActualCounts[pid] = next;

  const input = document.getElementById(`audit-input-${pid}`);
  if (input) input.value = next;

  calcAuditDiff(pid, prod.stockQuantity);
}

function handleAuditDirectInput(pid, sysStock) {
  const input = document.getElementById(`audit-input-${pid}`);
  if (!input) return;
  const val = Math.max(0, parseFloat(input.value) || 0);
  auditActualCounts[pid] = val;
  calcAuditDiff(pid, sysStock);
}

function calcAuditDiff(pid, sysStock) {
  const prod = auditProductsList.find(p => (p.id || p.Id) === pid);
  const actual = auditActualCounts[pid] !== undefined ? auditActualCounts[pid] : sysStock;
  const diff = actual - sysStock;
  const cost = prod ? (prod.cost || 0) : 0;
  const diffVal = diff * cost;

  const diffEl = document.getElementById(`audit-diff-${pid}`);
  const valEl = document.getElementById(`audit-diff-val-${pid}`);
  const statusEl = document.getElementById(`audit-status-${pid}`);

  if (diffEl) {
    if (diff === 0) {
      diffEl.innerText = '0 (مطابق)';
      diffEl.className = 'font-bold font-mono text-xs text-slate-400';
    } else if (diff > 0) {
      diffEl.innerText = `+${diff} (زيادة)`;
      diffEl.className = 'font-black font-mono text-xs text-sky-600 dark:text-sky-400';
    } else {
      diffEl.innerText = `${diff} (عجز/نقص)`;
      diffEl.className = 'font-black font-mono text-xs text-rose-600 dark:text-rose-400';
    }
  }

  if (valEl) {
    if (diff === 0) {
      valEl.innerText = '0 د.ع';
      valEl.className = 'font-bold font-mono text-xs text-slate-400';
    } else if (diff > 0) {
      valEl.innerText = `+${Math.round(diffVal).toLocaleString()} د.ع`;
      valEl.className = 'font-black font-mono text-xs text-sky-600 dark:text-sky-400';
    } else {
      valEl.innerText = `${Math.round(diffVal).toLocaleString()} د.ع`;
      valEl.className = 'font-black font-mono text-xs text-rose-600 dark:text-rose-400';
    }
  }

  if (statusEl) {
    if (diff === 0) {
      statusEl.innerHTML = '<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-emerald-100 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400">مطابق ✔</span>';
    } else if (diff > 0) {
      statusEl.innerHTML = '<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-sky-100 text-sky-700 dark:bg-sky-950/60 dark:text-sky-400">زيادة ➕</span>';
    } else {
      statusEl.innerHTML = '<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-rose-100 text-rose-700 dark:bg-rose-950/60 dark:text-rose-400">عجز ⚠️</span>';
    }
  }

  updateAuditKPIs();
}

function filterAuditTable() {
  const q = (document.getElementById('audit-searchInput')?.value || '').toLowerCase().trim();
  const selCat = document.getElementById('auditCategoryFilter')?.value || '';
  const statusFilter = document.getElementById('auditStatusFilter')?.value || 'all';

  auditFilteredList = auditProductsList.filter(p => {
    const pid = p.id || p.Id;
    const actual = auditActualCounts[pid] !== undefined ? auditActualCounts[pid] : p.stockQuantity;
    const diff = actual - p.stockQuantity;

    const pName = (p.name || p.Name || '').toLowerCase();
    const pBar = (p.barcode || p.Barcode || '').toLowerCase();
    const pCat = (p.category || p.Category || '').toLowerCase();

    const matchesSearch = !q || pName.includes(q) || pBar.includes(q) || pCat.includes(q);
    const matchesCat = !selCat || (p.category === selCat || p.Category === selCat);

    let matchesStatus = true;
    if (statusFilter === 'diff') matchesStatus = diff !== 0;
    else if (statusFilter === 'shortage') matchesStatus = diff < 0;
    else if (statusFilter === 'surplus') matchesStatus = diff > 0;
    else if (statusFilter === 'matched') matchesStatus = diff === 0;

    return matchesSearch && matchesCat && matchesStatus;
  });

  renderAuditTable();
}

function renderAuditTable() {
  const tbody = document.getElementById('auditTableBody');
  const summaryEl = document.getElementById('auditCountSummary');
  const showAllBtn = document.getElementById('auditShowAllBtn');
  if (!tbody) return;

  if (auditFilteredList.length === 0) {
    tbody.innerHTML = '<tr><td colspan="9" class="text-center py-12 text-slate-400 font-bold">لا توجد مواد مطابقة للبحث أو الفلترة للجرد</td></tr>';
    if (summaryEl) summaryEl.innerText = 'يتم عرض 0 مادة';
    if (showAllBtn) showAllBtn.classList.add('hidden');
    return;
  }

  const limitVal = document.getElementById('auditDisplayLimit')?.value || '1000';
  let limit = auditFilteredList.length;
  if (limitVal !== 'all') {
    limit = parseInt(limitVal, 10) || 1000;
  }

  const displayItems = auditFilteredList.slice(0, limit);

  let rowsHtml = '';
  for (let i = 0; i < displayItems.length; i++) {
    const p = displayItems[i];
    const pid = p.id || p.Id;
    const sysStock = p.stockQuantity || 0;
    const actual = auditActualCounts[pid] !== undefined ? auditActualCounts[pid] : sysStock;
    const diff = actual - sysStock;
    const cost = p.cost || 0;
    const diffVal = diff * cost;

    const isMatched = diff === 0;
    const isSurplus = diff > 0;
    const isShortage = diff < 0;

    rowsHtml += `
      <tr class="hover:bg-slate-50 dark:hover:bg-slate-800/40 transition">
        <td class="p-2.5 text-center text-slate-400 font-bold">${i + 1}</td>
        <td class="p-2.5">
          <div class="font-black text-slate-900 dark:text-white text-xs">${p.name || p.Name}</div>
          <div class="text-[10px] font-mono text-sky-500 font-bold flex items-center gap-1">
            <span>🏷</span><span>${p.barcode || p.Barcode || '--'}</span>
          </div>
        </td>
        <td class="p-2.5">
          <span class="inline-block px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-800 text-[10px] font-bold text-slate-600 dark:text-slate-300">${p.category || 'عام'}</span>
        </td>
        <td class="p-2.5 text-center font-black font-mono text-slate-800 dark:text-white text-xs">${sysStock} قطعة</td>
        <td class="p-2.5 text-center">
          <div class="inline-flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-xl border border-slate-300 dark:border-slate-700">
            <button onclick="changeAuditCount('${pid}', -1)" class="w-6 h-6 rounded-lg bg-white dark:bg-slate-700 text-rose-500 hover:bg-rose-50 font-black text-xs flex items-center justify-center shadow-sm">-</button>
            <input id="audit-input-${pid}" type="number" min="0" value="${actual}" oninput="handleAuditDirectInput('${pid}', ${sysStock})" class="w-16 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-600 rounded-lg py-0.5 text-center font-black font-mono text-xs text-slate-900 dark:text-white">
            <button onclick="changeAuditCount('${pid}', 1)" class="w-6 h-6 rounded-lg bg-white dark:bg-slate-700 text-emerald-600 hover:bg-emerald-50 font-black text-xs flex items-center justify-center shadow-sm">+</button>
          </div>
        </td>
        <td class="p-2.5 text-center" id="audit-diff-${pid}">
          <span class="${isMatched ? 'font-bold font-mono text-xs text-slate-400' : isSurplus ? 'font-black font-mono text-xs text-sky-600 dark:text-sky-400' : 'font-black font-mono text-xs text-rose-600 dark:text-rose-400'}">
            ${isMatched ? '0 (مطابق)' : isSurplus ? `+${diff} (زيادة)` : `${diff} (عجز)`}
          </span>
        </td>
        <td class="p-2.5 text-center" id="audit-diff-val-${pid}">
          <span class="${isMatched ? 'font-bold font-mono text-xs text-slate-400' : isSurplus ? 'font-black font-mono text-xs text-sky-600 dark:text-sky-400' : 'font-black font-mono text-xs text-rose-600 dark:text-rose-400'}">
            ${isMatched ? '0 د.ع' : isSurplus ? `+${Math.round(diffVal).toLocaleString()} د.ع` : `${Math.round(diffVal).toLocaleString()} د.ع`}
          </span>
        </td>
        <td class="p-2.5 text-center" id="audit-status-${pid}">
          ${isMatched 
            ? '<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-emerald-100 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400">مطابق ✔</span>' 
            : isSurplus 
            ? '<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-sky-100 text-sky-700 dark:bg-sky-950/60 dark:text-sky-400">زيادة ➕</span>' 
            : '<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-rose-100 text-rose-700 dark:bg-rose-950/60 dark:text-rose-400">عجز ⚠️</span>'}
        </td>
        <td class="p-2.5 text-center">
          <button onclick="quickSaveAuditStock('${pid}')" class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-[11px] rounded-lg shadow-sm transition" title="حفظ هذه المادة فقط">
            💾
          </button>
        </td>
      </tr>
    `;
  }

  tbody.innerHTML = rowsHtml;

  if (summaryEl) {
    summaryEl.innerText = `يتم عرض ${displayItems.length.toLocaleString()} مادة من إجمالي ${auditFilteredList.length.toLocaleString()} مادة`;
  }

  if (showAllBtn) {
    if (auditFilteredList.length > displayItems.length) {
      showAllBtn.innerText = `👁 عرض كافة المواد (${auditFilteredList.length.toLocaleString()} مادة) دفعة واحدة`;
      showAllBtn.classList.remove('hidden');
    } else {
      showAllBtn.classList.add('hidden');
    }
  }
}

function showAllAuditItems() {
  const limitSelect = document.getElementById('auditDisplayLimit');
  if (limitSelect) limitSelect.value = 'all';
  renderAuditTable();
}

async function quickSaveAuditStock(pid) {
  const prod = auditProductsList.find(p => (p.id || p.Id) === pid);
  if (!prod) return;
  const actualStock = auditActualCounts[pid] !== undefined ? auditActualCounts[pid] : prod.stockQuantity;

  const res = await callBackend('update_stock_audit', { productId: pid, actualStock });
  if (res && res.success) {
    prod.stockQuantity = actualStock;
    alert(`✔ تم تحديث واعتماد رصيد [${prod.name || prod.Name}] في المخزن بنجاح!`);
    updateAuditKPIs();
    filterAuditTable();
    await loadInventory();
    await loadProducts();
  }
}

async function saveAllAuditChanges() {
  const modified = [];
  auditProductsList.forEach(p => {
    const pid = p.id || p.Id;
    const actual = auditActualCounts[pid] !== undefined ? auditActualCounts[pid] : p.stockQuantity;
    if (actual !== p.stockQuantity) {
      modified.push({ productId: pid, actualStock: actual });
    }
  });

  if (modified.length === 0) {
    alert('لا توجد أي فروقات معدلة لحفظها، جميع المواد مطابقة!');
    return;
  }

  if (!confirm(`هل أنت متأكد من حفظ واعتماد فروقات الجرد لـ (${modified.length}) مادة وتحديث أرصدة المخزن؟`)) {
    return;
  }

  const res = await callBackend('batch_update_stock_audit', { updates: modified });
  if (res && res.success) {
    alert(`✔ تم حفظ واعتماد فروقات الجرد لـ ${modified.length} مادة وتحديث المخزن بنجاح!`);
    await loadStockAudit();
    await loadInventory();
    await loadProducts();
  }
}

// ========================================================
// 4. DAMAGED & EXPIRED ITEMS (المواد التالفة والمنتهية)
// ========================================================
let damagedItemsList = [];

async function loadDamagedItems() {
  const res = await callBackend('get_damaged_items');
  if (res && res.success) {
    damagedItemsList = res.items || [];
    renderDamagedTable(damagedItemsList);

    const lossEl = document.getElementById('damaged-totalLoss');
    if (lossEl) lossEl.innerText = `${Number(res.totalLoss || 0).toLocaleString()} د.ع`;

    const countEl = document.getElementById('damaged-totalCount');
    if (countEl) countEl.innerText = `${damagedItemsList.length} مادة`;
  }
}

function renderDamagedTable(list) {
  const tbody = document.getElementById('damagedTableBody');
  if (!tbody) return;

  if (list.length === 0) {
    tbody.innerHTML = '<tr><td colspan="8" class="text-center py-8 text-slate-400 font-bold">لا توجد مواد تالفة مسجلة</td></tr>';
    return;
  }

  tbody.innerHTML = '';
  list.forEach((d, idx) => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40 transition';
    tr.innerHTML = `
      <td class="p-3 text-center text-slate-400 font-bold">${idx + 1}</td>
      <td class="p-3 font-bold text-slate-800 dark:text-white">${d.productName}</td>
      <td class="p-3 font-mono text-sky-500">${d.barcode || '--'}</td>
      <td class="p-3 text-center font-bold text-rose-500 font-mono">${d.quantity} قطعة</td>
      <td class="p-3 text-center font-bold text-rose-500 font-mono">${Number(d.lossAmount).toLocaleString()} د.ع</td>
      <td class="p-3 text-slate-600 dark:text-slate-300">${d.reason}</td>
      <td class="p-3 text-center"><span class="px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-800 text-[10px] font-bold">${d.actionTaken}</span></td>
      <td class="p-3 text-center text-slate-400 font-mono text-[11px]">${d.date}</td>
    `;
    tbody.appendChild(tr);
  });
}

function openAddDamagedModal() {
  const select = document.getElementById('adm-productSelect');
  if (select) {
    select.innerHTML = '<option value="">-- اختر مادة من المخزن --</option>';
    state.products.forEach(p => {
      select.innerHTML += `<option value="${p.id}">${p.name} (رصيد: ${p.stockQuantity})</option>`;
    });
  }
  document.getElementById('adm-quantity').value = '1';
  document.getElementById('addDamagedModal')?.classList.remove('hidden');
}

function closeAddDamagedModal() {
  document.getElementById('addDamagedModal')?.classList.add('hidden');
}

async function saveNewDamagedItem() {
  const prodId = document.getElementById('adm-productSelect')?.value;
  const qty = parseFloat(document.getElementById('adm-quantity')?.value) || 0;
  const reason = document.getElementById('adm-reason')?.value;
  const actionTaken = document.getElementById('adm-action')?.value;

  if (!prodId || qty <= 0) {
    alert('يرجى اختيار مادة وإدخال كمية صحيحة!');
    return;
  }

  const res = await callBackend('add_damaged_item', { productId: prodId, quantity: qty, reason, actionTaken });
  if (res && res.success) {
    alert('✔ تم تسجيل المادة التالفة وخصمها من رصيد المخزن بنجاح!');
    closeAddDamagedModal();
    await loadDamagedItems();
    await loadInventory();
    await loadProducts();
  }
}

// ========================================================
// 5. REPORTS & FINANCIAL ANALYTICS (التقارير والأرباح)
// ========================================================
async function loadReports() {
  const res = await callBackend('get_reports');
  if (res && res.success) {
    const todaySalesEl = document.getElementById('rep-todaySales');
    if (todaySalesEl) todaySalesEl.innerText = `${Number(res.todayTotal || 0).toLocaleString()} د.ع`;

    const todayProfitEl = document.getElementById('rep-todayProfit');
    if (todayProfitEl) todayProfitEl.innerText = `+${Number(res.todayProfit || 0).toLocaleString()} د.ع`;

    const todayInvoicesEl = document.getElementById('rep-todayInvoices');
    if (todayInvoicesEl) todayInvoicesEl.innerText = `${res.todayInvoicesCount || 0} فواتير`;

    const monthSalesEl = document.getElementById('rep-monthSales');
    if (monthSalesEl) monthSalesEl.innerText = `${Number(res.monthTotal || 0).toLocaleString()} د.ع`;

    const monthProfitEl = document.getElementById('rep-monthProfit');
    if (monthProfitEl) monthProfitEl.innerText = `+${Number(res.monthProfit || 0).toLocaleString()} د.ع`;

    const monthInvoicesEl = document.getElementById('rep-monthInvoices');
    if (monthInvoicesEl) monthInvoicesEl.innerText = `${res.monthInvoicesCount || 0} فواتير`;

    // Render Top Items
    const topTbody = document.getElementById('rep-topItemsTable');
    if (topTbody) {
      if (!res.topItems || res.topItems.length === 0) {
        topTbody.innerHTML = '<tr><td colspan="4" class="text-center py-6 text-slate-400">لا توجد مبيعات مسجلة حتى الآن</td></tr>';
      } else {
        topTbody.innerHTML = '';
        res.topItems.forEach((it, idx) => {
          topTbody.innerHTML += `
            <tr class="hover:bg-slate-50 dark:hover:bg-slate-800/40">
              <td class="p-3 text-center text-slate-400 font-bold">${idx + 1}</td>
              <td class="p-3 font-bold text-slate-800 dark:text-white">${it.name}</td>
              <td class="p-3 text-center font-bold text-emerald-500 font-mono">${it.qty} قطعة</td>
              <td class="p-3 text-center font-black text-sky-500 font-mono">${Number(it.total).toLocaleString()} د.ع</td>
            </tr>
          `;
        });
      }
    }
  }
}

// ========================================================
// 6. PRINTING & RECEIPT SETTINGS (إعدادات الطابعة والوصل)
// ========================================================
function loadPrintingSettings() {
  const saved = localStorage.getItem('pos_printer_settings');
  if (saved) {
    try {
      const config = JSON.parse(saved);
      if (config.marketName) document.getElementById('pr-marketName').value = config.marketName;
      if (config.marketSub) document.getElementById('pr-marketSub').value = config.marketSub;
      if (config.marketPhone) document.getElementById('pr-marketPhone').value = config.marketPhone;
      if (config.marketFooter) document.getElementById('pr-marketFooter').value = config.marketFooter;
      if (config.paperType) document.getElementById('pr-paperType').value = config.paperType;
    } catch { }
  }
  updateReceiptLivePreview();
}

function updateReceiptLivePreview() {
  const name = document.getElementById('pr-marketName')?.value || '7amo Market';
  const sub = document.getElementById('pr-marketSub')?.value || 'نظام إدارة السوبرماركت';
  const phone = document.getElementById('pr-marketPhone')?.value || '0750 000 0000';
  const footer = document.getElementById('pr-marketFooter')?.value || 'شكراً لزيارتكم!';

  const pName = document.getElementById('prev-marketName');
  const pSub = document.getElementById('prev-marketSub');
  const pPhone = document.getElementById('prev-marketPhone');
  const pFooter = document.getElementById('prev-marketFooter');

  if (pName) pName.innerText = name;
  if (pSub) pSub.innerText = sub;
  if (pPhone) pPhone.innerText = `هاتف: ${phone}`;
  if (pFooter) pFooter.innerText = footer;
}

function savePrintingSettings() {
  const config = {
    marketName: document.getElementById('pr-marketName')?.value,
    marketSub: document.getElementById('pr-marketSub')?.value,
    marketPhone: document.getElementById('pr-marketPhone')?.value,
    marketFooter: document.getElementById('pr-marketFooter')?.value,
    paperType: document.getElementById('pr-paperType')?.value
  };
  localStorage.setItem('pos_printer_settings', JSON.stringify(config));
  alert('✔ تم حفظ إعدادات وترويسة وصل البيع بنجاح!');
}

