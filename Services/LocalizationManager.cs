using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace HamoPos.Services;

/// <summary>
/// مدير اللغات التفاعلي (عربي / کوردی) للتبديل اللحظي لكافة النصوص والأزرار
/// </summary>
public class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => _instance.Value;

    private string _currentLanguage = "ar"; // "ar" = العربية, "ku" = کوردی (سۆرانی)

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsArabic));
                OnPropertyChanged(nameof(IsKurdish));
                OnPropertyChanged(nameof(CurrentLanguageDisplayName));
                OnPropertyChanged("Item[]"); // Notifies all XAML bindings {Binding [Key]}
                LanguageChanged?.Invoke(this, EventArgs.Empty);
                ApplyToApplicationResources();
            }
        }
    }

    public bool IsArabic => CurrentLanguage == "ar";
    public bool IsKurdish => CurrentLanguage == "ku";
    public string CurrentLanguageDisplayName => IsArabic ? "العربية" : "کوردی";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    private readonly Dictionary<string, string> _ar = new();
    private readonly Dictionary<string, string> _ku = new();

    public string this[string key] => Get(key);

    public LocalizationManager()
    {
        InitializeTranslations();
        ApplyToApplicationResources();
    }

    public void ToggleLanguage()
    {
        CurrentLanguage = CurrentLanguage == "ar" ? "ku" : "ar";
    }

    public void SetLanguage(string lang)
    {
        if (lang == "ar" || lang == "ku")
        {
            CurrentLanguage = lang;
        }
    }

    public string Get(string key)
    {
        if (CurrentLanguage == "ku" && _ku.TryGetValue(key, out var kuVal))
            return kuVal;

        if (_ar.TryGetValue(key, out var arVal))
            return arVal;

        return key;
    }

    private void ApplyToApplicationResources()
    {
        if (Application.Current != null)
        {
            var dict = CurrentLanguage == "ku" ? _ku : _ar;
            foreach (var kv in dict)
            {
                Application.Current.Resources[kv.Key] = kv.Value;
            }
        }
    }

    private void InitializeTranslations()
    {
        // =========================================================================
        // ARABIC TRANSLATIONS (العربية)
        // =========================================================================
        _ar["App_Title"] = "7amo.pos - نظام نقاط البيع والمخازن المتكامل";
        _ar["App_Subtitle"] = "نظام الكاشير والمخازن";
        _ar["App_Currency"] = "الدينار العراقي (د.ع)";
        _ar["App_Currency_Short"] = "د.ع";
        _ar["App_Offline_Mode"] = "محلي (Offline Mode)";
        _ar["App_Active_Cashier"] = "كاشير نشط";
        _ar["Nav_BackToMain"] = "العودة للرئيسية";
        _ar["Gen_Save"] = "حفظ";
        _ar["Gen_Cancel"] = "إلغاء";
        _ar["Add_EnableExpiry"] = "تفعيل تاريخ الصلاحية";
        _ar["Add_NoExpirySet"] = "بدون تاريخ صلاحية";

        // Sidebar Navigation
        _ar["Nav_Dashboard"] = "لوحة التحكم";
        _ar["Nav_Cashier"] = "نقطة البيع (الكاشير)";
        _ar["Nav_SalesHistory"] = "سجل المبيعات والفواتير";
        _ar["Nav_Purchases"] = "شراء مواد من مندوب";
        _ar["Nav_Warehouse"] = "المخزن";
        _ar["Nav_RemainingStock"] = "المتبقي في المخزن";
        _ar["Nav_StockAudit"] = "جرد المخزن";
        _ar["Nav_Inventory"] = "إدارة المخزن والمستودع";
        _ar["Nav_DamagedItems"] = "المواد التالفة والهالك";
        _ar["Nav_AddProduct"] = "إضافة مادة جديدة";
        _ar["Nav_Suppliers"] = "إدارة المناديب والموردين";
        _ar["Nav_SupplierOrders"] = "طلبيات المناديب والمحلات";
        _ar["Nav_UserAccounts"] = "حسابات الكاشير";
        _ar["Nav_Printing"] = "الطباعة والملصقات";
        _ar["Nav_Reports"] = "مركز التقارير الشامل";
        _ar["Nav_Settings"] = "الإعدادات والشبكة";

        // Dashboard & Analytics
        _ar["Dash_Title"] = "لوحة التحكم والإحصائيات العامة";
        _ar["Dash_Subtitle"] = "نظرة شاملة وسريعة على مبيعات اليوم، الشهر، وتنبيهات المخزون";
        _ar["Dash_Refresh"] = "تحديث البيانات";
        _ar["Dash_TodaySales"] = "مبيعات اليوم";
        _ar["Dash_TodayInvoices"] = "فواتير اليوم";
        _ar["Dash_MonthSales"] = "مبيعات الشهر الحالية";
        _ar["Dash_LowStock"] = "مواد أوشكت على النفاد";
        _ar["Dash_LowStockAlerts"] = "تنبيهات نقص المخزون";
        _ar["Dash_ItemsNeedOrder"] = "مواد بحاجة للطلب";
        _ar["Dash_InvoiceUnit"] = "فاتورة";
        _ar["Dash_QuickActions"] = "الوصول والإجراءات السريعة";
        _ar["Dash_NewSale"] = "وصل مبيعات جديد";
        _ar["Dash_BuyGoods"] = "شراء وتوريد مواد";
        _ar["Dash_QuickAudit"] = "جرد سريع للمخزن";
        _ar["Dash_SalesPerformance"] = "أداء المبيعات خلال الأسبوع";
        _ar["Dash_TopProducts"] = "المواد الأكثر طلباً ومبيعاً";
        _ar["Dash_RecentInvoices"] = "أحدث الفواتير المنفذة";
        _ar["Dash_ExpiringLowStock"] = "مواد قاربت على النفاد";
        _ar["Dash_Time"] = "الوقت";
        _ar["Dash_PaymentType"] = "طريقة الدفع";
        _ar["Dash_InvoiceAmount"] = "المبلغ الإجمالي";
        _ar["Dash_ChartWeeklySales"] = "مخطط حركة المبيعات اليومية (آخر 7 أيام)";
        _ar["Dash_ChartPaymentDist"] = "توزيع المبيعات حسب طرق الدفع";
        _ar["Dash_ChartTopProducts"] = "مخطط أكثر المواد طلباً ومبيعاً";
        _ar["Dash_ChartPeakHours"] = "أوقات الذروة ونشاط المبيعات";
        _ar["Dash_WeeklyTotal"] = "إجمالي مبيعات الأسبوع:";
        _ar["Dash_DailyAverage"] = "المعدل اليومي:";

        // Cashier & Table Elements
        _ar["en_Dele"] = "حذف";
        _ar["Gen_Total"] = "الإجمالي";
        _ar["Gen_Quantity"] = "الكمية";
        _ar["Cart_ItemsCount"] = "عدد الأصناف بالسلة:";
        _ar["Cart_TotalUnits"] = "مجموع المواد الكلي:";
        _ar["Cart_SubTotal"] = "المجموع الفرعي:";
        _ar["Cart_ExtraDiscount"] = "الخصم الإضافي (د.ع):";
        _ar["Cart_FinalRequired"] = "المبلغ النهائي المطلوب دفعه:";
        
        // Drawer Cash Management
        _ar["Drawer_Title"] = "درج الكاشير وحساب الصندوق";
        _ar["Drawer_OpeningBalance"] = "الرصيد الافتتاحي (المفتوح):";
        _ar["Drawer_CashSales"] = "مبيعات الكاشير النقدية:";
        _ar["Drawer_Deposits"] = "إجمالي الإيداعات:";
        _ar["Drawer_Withdrawals"] = "إجمالي المسحوبات:";
        _ar["Drawer_CurrentCash"] = "المبلغ الفعلي الموجود بالدرج:";
        _ar["Drawer_AddCashBtn"] = "📥 إيداع مال في الدرج";
        _ar["Drawer_TakeCashBtn"] = "📤 سحب مال من الدرج";
        _ar["Drawer_Amount"] = "المبلغ (د.ع)";
        _ar["Drawer_Reason"] = "السبب / البيان";
        _ar["Drawer_Confirm"] = "تأكيد العملية";
        _ar["Drawer_Cancel"] = "إلغاء";
        _ar["Drawer_MovementVoucher"] = "سند حركة نقدية بالصندوق";
        _ar["Drawer_PrintVoucher"] = "🖨️ طباعة السند";
        _ar["Drawer_VoucherNumber"] = "رقم السند:";
        _ar["Drawer_MovementType"] = "نوع الحركة:";
        _ar["Drawer_Cashier"] = "الكاشير:";
        _ar["Drawer_Date"] = "التاريخ والوقت:";
        _ar["Drawer_AmountLabel"] = "المبلغ المالي:";
        _ar["Drawer_ReasonLabel"] = "السبب / البيان:";
        _ar["Drawer_Close"] = "إغلاق";

        // Cashier POS
        _ar["Pos_Title"] = "نقطة البيع والكاشير السريع";
        _ar["Pos_SearchPlaceholder"] = "مسح أو كتابة الباركود / اسم المادة...";
        _ar["Pos_AddInvoice"] = "فاتورة جديدة";
        _ar["Pos_HoldInvoices"] = "سجل الوصلات والورديات";
        _ar["Pos_BrowseInventory"] = "المخزن / تصفح المواد";
        _ar["Pos_Drawer"] = "الدرج";
        _ar["Pos_BarcodeScanner"] = "قارئ الباركود:";
        _ar["Pos_AddManual"] = "إضافة للفاتورة ↵";
        _ar["Pos_CartContents"] = "محتويات فاتورة";
        _ar["Pos_ItemsUnit"] = "بنود";
        _ar["Pos_ClearCartBtn"] = "مسح / تفريغ الفاتورة";
        _ar["Pos_ItemNameBarcode"] = "اسم المادة والباركود";
        _ar["Pos_SaleType"] = "نوعية البيع (مباشر)";
        _ar["Pos_SaleTypeRetail"] = "مفرد";
        _ar["Pos_SaleTypeWholesale"] = "جملة";
        _ar["Pos_SaleTypeCarton"] = "كرتون";
        _ar["Pos_UnitPrice"] = "سعر الوحدة";
        _ar["Pos_PayCash"] = "دفع نقدي (F1 / Cash)";
        _ar["Pos_PayCard"] = "دفع بطاقة / ماستر (Card)";
        _ar["Pos_SummaryPayment"] = "ملخص الحساب والدفع";
        _ar["Pos_ExtraDiscount"] = "الخصم الإضافي (د.ع):";
        _ar["Pos_FinalRequiredAmount"] = "المبلغ النهائي المطلوب دفعه:";
        _ar["Pos_Cart"] = "سلة المشتريات";
        _ar["Pos_SubTotal"] = "المجموع الفرعي:";
        _ar["Pos_Discount"] = "الخصم الممنوح:";
        _ar["Pos_Total"] = "الإجمالي النهائي:";
        _ar["Pos_PaymentMethod"] = "طريقة الدفع";
        _ar["Pos_Cash"] = "نقداً";
        _ar["Pos_Card"] = "بطاقة إلكترونية";
        _ar["Pos_Debt"] = "آجل";
        _ar["Pos_CompleteSale"] = "إتمام البيع وطباعة الوصل";
        _ar["Pos_HoldInvoice"] = "تعليق الفاتورة";
        _ar["Pos_ClearCart"] = "تفريغ السلة";
        _ar["Pos_ItemsCount"] = "عدد المواد";
        _ar["Pos_PiecesCount"] = "إجمالي القطع";

        // Inventory & Warehouse Management (إدارة المخزن والمستودع)
        _ar["Inv_Title"] = "إدارة المخزن والمستودع وتفاصيل المواد";
        _ar["Inv_Subtitle"] = "مراقبة أسعار الشراء، قيمة المخزون الكلية، والكميات بالكراتين والمفرد";
        _ar["Inv_TotalCostValue"] = "إجمالي القيمة الشرائية للمخزن";
        _ar["Inv_TotalPieces"] = "إجمالي القطع بالمخزن (المفرد)";
        _ar["Inv_TotalCartons"] = "إجمالي الكراتين بالمخزن";
        _ar["Inv_TotalSellingValue"] = "إجمالي القيمة البيعية المتوقعة";
        _ar["Inv_TotalProfitValue"] = "صافي الأرباح المتوقعة";
        _ar["Inv_RegisteredProducts"] = "عدد الأصناف المسجلة";
        _ar["Inv_AddProduct"] = "إضافة مادة جديدة";
        _ar["Inv_Refresh"] = "تحديث البيانات";
        _ar["Inv_AllCategories"] = "جميع التصنيفات";
        _ar["Inv_SearchPlaceholder"] = "بحث باسم المادة، الباركود، أو المندوب...";
        _ar["Inv_Barcode"] = "الباركود";
        _ar["Inv_ProductName"] = "اسم المادة";
        _ar["Inv_Category"] = "التصنيف";
        _ar["Inv_Supplier"] = "المندوب المورد";
        _ar["Inv_SellingPrice"] = "سعر البيع (مفرد)";
        _ar["Inv_CostPrice"] = "سعر الشراء (التكلفة)";
        _ar["Inv_WholesalePrice"] = "سعر الجملة";
        _ar["Inv_StockPieces"] = "المخزون (قطع)";
        _ar["Inv_StockCartons"] = "الكراتين";
        _ar["Inv_TotalCostColumn"] = "إجمالي تكلفة المادة";
        _ar["Inv_Actions"] = "إجراءات";
        _ar["Inv_Edit"] = "✏️ تعديل";
        _ar["Inv_Delete"] = "🗑️ حذف";
        _ar["Inv_UnitPieces"] = "قطعة";
        _ar["Inv_UnitCartons"] = "كرتون";
        _ar["Inv_UnitTypes"] = "صنف";

        // Current Stock Viewer
        _ar["Stock_Title"] = "المخزون الحالي والمتبقي من المواد";
        _ar["Stock_TotalRegistered"] = "إجمالي عدد المواد المسجلة";
        _ar["Stock_TotalPieces"] = "إجمالي القطع المتبقية بالمخزن";
        _ar["Stock_TotalCartons"] = "إجمالي الكراتين المتبقية";
        _ar["Stock_ItemUnit"] = "مادة";
        _ar["Stock_PieceUnit"] = "قطعة";
        _ar["Stock_CartonUnit"] = "كرتون";
        _ar["Stock_Status"] = "حالة المخزون";

        // Stock Audit
        _ar["Audit_Title"] = "جرد وتدقيق المخزن الفعلي";
        _ar["Audit_SystemStock"] = "الرصيد بالنظام (المخزن)";
        _ar["Audit_ActualStock"] = "العدد الفعلي (الموجود)";
        _ar["Audit_Difference"] = "الفارق بين الجرد والمخزن";
        _ar["Audit_Apply"] = "اعتماد الجرد وتعديل الكميات";
        _ar["Audit_ShortageValue"] = "إجمالي تكلفة النقص (العجز):";
        _ar["Audit_SurplusValue"] = "إجمالي قيمة الزيادة (الفائض):";
        _ar["Audit_NetDifference"] = "صافي فارق الجرد المالي:";
        _ar["Audit_PriceCost"] = "تكلفة المادة (د.ع)";
        _ar["Audit_SearchBarcodeName"] = "قارئ الباركود أو بحث باسم المادة:";

        // Damaged Items
        _ar["Dam_Title"] = "المواد التالفة والهالك";
        _ar["Dam_AddDamaged"] = "تسجيل مادة تالفة جديدة";
        _ar["Dam_TotalLoss"] = "إجمالي قيمة الخسائر";
        _ar["Dam_Reason"] = "سبب الإتلاف";

        // Add Product Full
        _ar["Add_Title"] = "إضافة مادة جديدة للمخزن";
        _ar["Add_EditTitle"] = "تعديل بيانات المادة";
        _ar["Add_Save"] = "حفظ المادة في المخزن";
        _ar["Add_BackToMain"] = "العودة للقائمة الرئيسية";
        _ar["Add_BrowseInventory"] = "استعراض مواد المخزن للتعديل";
        _ar["Add_ClearFields"] = "تفريغ جميع الحقول";
        _ar["Add_SaveButton"] = "حفظ المادة (Save)";
        _ar["Add_ExpiryDate"] = "📅 تاريخ انتهاء الصلاحية:";
        _ar["Add_AlertDays"] = "أيام التحذير المسبق:";
        _ar["Add_FirstAdded"] = "أول إضافة:";
        _ar["Add_LastUpdated"] = "آخر تعديل:";
        _ar["Add_Barcode"] = "الباركود (نقر مرتين للتوليد):";
        _ar["Add_BarcodePrefix"] = "بادئة 200245";
        _ar["Add_ProductName"] = "اسم المادة:";
        _ar["Add_SupplierName"] = "اسم المندوب:";
        _ar["Add_SupplierDetails"] = "➕ تفاصيل المندوب";
        _ar["Add_CartonDetailsHeader"] = "تفاصيل الكرتون والكميات وسعر الشراء والتكلفة:";
        _ar["Add_CartonsCount"] = "عدد الكراتين:";
        _ar["Add_ItemsInCarton"] = "المواد داخل الكرتون:";
        _ar["Add_ExtraPieces"] = "قطع إضافية (مفرد):";
        _ar["Add_TotalStock"] = "مجموع المواد الكلي (تلقائي):";
        _ar["Add_CartonBuyPrice"] = "سعر شراء الكرتون (د.ع):";
        _ar["Add_PieceCost"] = "تكلفة المفرد (الكرتون ÷ المواد):";
        _ar["Add_SellingPricingHeader"] = "أسعار البيع وحساب الأرباح التلقائية (مفرد، جملة، كرتون):";
        _ar["Add_RetailPrice"] = "سعر بيع المفرد (د.ع):";
        _ar["Add_RetailProfitPiece"] = "ربح بيع المفرد للقطعة:";
        _ar["Add_RetailCartonProfit"] = "ربح الكرتون بالكامل بالمفرد:";
        _ar["Add_RetailMargin"] = "نسبة الربح (هامش المفرد):";
        _ar["Add_WholesalePrice"] = "سعر بيع الجملة (د.ع):";
        _ar["Add_WholesaleProfitPiece"] = "ربح بيع الجملة للقطعة:";
        _ar["Add_WholesaleMargin"] = "نسبة الربح (هامش الجملة):";
        _ar["Add_CartonSellPrice"] = "سعر بيع بالكرتون (د.ع):";
        _ar["Add_CartonProfitWhole"] = "ربح بيع الكرتون بالكامل:";
        _ar["Add_CartonMargin"] = "نسبة الربح (هامش الكرتون):";
        _ar["Add_BasicInfo"] = "البيانات الأساسية للمادة";
        _ar["Add_PricingInfo"] = "الأسعار والعبوة والتكلفة";
        _ar["Add_NewCategory"] = "➕ صنف جديد";
        _ar["Add_CategoryModalTitle"] = "إضافة صنف مادة جديد";
        _ar["Add_CategoryName"] = "اسم الصنف الجديد:";
        _ar["Add_TotalCalculated"] = "مجموع المواد الكلي (تلقائي):";
        _ar["Add_CostCalculated"] = "تكلفة المفرد (الكرتون ÷ المواد):";

        // Suppliers
        _ar["Sup_Title"] = "إدارة المناديب والشركات الموردة";
        _ar["Sup_CardsTitle"] = "دليل وبطاقات المناديب والشركات";
        _ar["Sup_CardMaterialsCount"] = "أصناف المواد الموردة";
        _ar["Sup_CardCompanyLoc"] = "الشركة والمكان:";
        _ar["Sup_CardPhone"] = "الهاتف:";
        _ar["Sup_CardDebtBalance"] = "الرصيد المستحق (الديون):";
        _ar["Sup_BackToCards"] = "العودة لبطاقات المناديب";
        _ar["Sup_InvoicesTab"] = "فواتير الشراء";
        _ar["Sup_ProductsTab"] = "المواد الموردة";
        _ar["Sup_LedgerTab"] = "كشف الحساب والمدفوعات";
        _ar["Sup_AttachReceipt"] = "إرفاق صورة الوصل";
        _ar["Sup_ViewReceipt"] = "عرض صورة الوصل";
        _ar["Sup_ReceiptModalTitle"] = "صورة الوصل المرفقة للمندوب";
        _ar["Sup_AddSupplier"] = "إضافة مندوب جديد";
        _ar["Sup_SupplierBalance"] = "الرصيد المالي الحالي";
        _ar["Sup_Statement"] = "كشف حساب المندوب";

        // User Accounts
        _ar["Users_Title"] = "إدارة وحسابات الكاشير والمبيعات اليومية";
        _ar["Users_AddUser"] = "إنشاء حساب كاشير جديد";
        _ar["Users_Role"] = "الصلاحية / الدور";

        // Purchases & Supplier Hub
        _ar["Pur_Title"] = "فاتورة شراء جديدة (Stock Entry)";
        _ar["Pur_Supplier"] = "المورد:";
        _ar["Pur_SupplierSelect"] = "اختر أو اكتب اسم المورد...";
        _ar["Pur_InvoiceNo"] = "الوصل:";
        _ar["Pur_InvoicePlaceholder"] = "رقم الوصل...";
        _ar["Pur_Date"] = "التاريخ:";
        _ar["Pur_NewInvoice"] = "جديدة";
        _ar["Pur_History"] = "السجل";
        _ar["Pur_ProductName"] = "• اسم المادة:";
        _ar["Pur_SearchPlaceholder"] = "ابحث عن مادة أو اكتب اسمها...";
        _ar["Pur_Quantity"] = "• العدد:";
        _ar["Pur_UnitType"] = "• نوع الوحدة والعبوة:";
        _ar["Pur_Single"] = "مفرد";
        _ar["Pur_Carton"] = "كرتون";
        _ar["Pur_OldCost"] = "الشراء القديم (للكرتون):";
        _ar["Pur_NewCost"] = "سعر الشراء الجديد (للكرتون):";
        _ar["Pur_TotalCost"] = "مجموع الشراء للمادة:";
        _ar["Pur_CalculatedPieceCost"] = "التكلفة المحسوبة للأرباح والتقارير:";
        _ar["Pur_BarcodePackage"] = "الباركود والعبوة";
        _ar["Pur_SellingPriceProfit"] = "سعر البيع والأرباح";
        _ar["Pur_CostMethod"] = "طريقة احتساب التكلفة (المتوسط المرجح)";
        _ar["Pur_AddToList"] = "إضافة للفاتورة";
        _ar["Pur_EmptyTable"] = "جدول المواد فارغ حالياً. استخدم الشريط أعلاه لإضافة المواد المشتراة.";
        _ar["Pur_TotalBill"] = "إجمالي فاتورة الشراء:";
        _ar["Pur_PaidAmount"] = "المبلغ المدفوع للمورد (د.ع):";
        _ar["Pur_RemainingDebt"] = "المتبقي (آجل بذمة المحل):";
        _ar["Pur_Clear"] = "إلغاء وتفريغ";
        _ar["Pur_Save"] = "اعتماد الفاتورة وتحديث المخزن";
        _ar["Pur_ModalTitle"] = "تعديل الباركود وعبوة الكرتون للمادة";
        _ar["Pur_ModalBarcode"] = "باركود المادة:";
        _ar["Pur_ModalCartonSize"] = "عدد القطع داخل الكرتون (العبوة):";
        _ar["Pur_ModalSave"] = "حفظ وتطبيق التعديل";
        _ar["Pur_ModalCancel"] = "إلغاء";

        // Reports
        _ar["Rep_Title"] = "مركز التقارير والإحصائيات الشاملة";
        _ar["Rep_Subtitle"] = "تحليلات دقيقة للمبيعات والأرباح والمخازن والديون";
        _ar["Rep_Refresh"] = "تحديث";
        _ar["Rep_Today"] = "اليوم";
        _ar["Rep_ThisMonth"] = "هذا الشهر";
        _ar["Rep_From"] = "من:";
        _ar["Rep_To"] = "إلى:";
        _ar["Rep_BackToHub"] = "العودة للتقارير";
        _ar["Rep_Tab_Sales"] = "المبيعات والأرباح";
        _ar["Rep_Tab_Damaged"] = "المواد التالفة والهالك";
        _ar["Rep_Tab_Returns"] = "المرتجعات والمستردات";
        _ar["Rep_Tab_Purchases"] = "المشتريات والتوريد";
        _ar["Rep_Tab_Inventory"] = "تقييم المخزون";
        _ar["Rep_Tab_Debts"] = "ديون العملاء والآجل";
        _ar["Rep_Tab_ShiftAudit"] = "تسليم الوردية والصندوق";
        _ar["Rep_Tab_Performance"] = "أداء الكاشير والذروة";
        _ar["Rep_Tab_StockMovement"] = "حركة ودوران البضائع";
        _ar["Rep_TotalSales"] = "إجمالي المبيعات";
        _ar["Rep_TotalProfits"] = "صافي الأرباح";
        _ar["Rep_TotalExpenses"] = "المصروفات والخسائر";

        // Sales Report Details (Arabic)
        _ar["Rep_Sales_SingleProfit"] = "أرباح بيع المفرد";
        _ar["Rep_Sales_SingleTag"] = "🟢 مفرد";
        _ar["Rep_Sales_SingleFormula"] = "(سعر بيع المفرد - تكلفة القطعة) × الكمية المباعة";
        _ar["Rep_Sales_WholesaleProfit"] = "أرباح بيع الجملة";
        _ar["Rep_Sales_WholesaleTag"] = "🔵 جملة";
        _ar["Rep_Sales_WholesaleFormula"] = "(سعر بيع الجملة - تكلفة القطعة) × الكمية المباعة";
        _ar["Rep_Sales_CartonProfit"] = "أرباح بيع الكراتين";
        _ar["Rep_Sales_CartonTag"] = "🟠 كرتون";
        _ar["Rep_Sales_CartonFormula"] = "(سعر بيع الكرتون - تكلفة الكرتون) × عدد الكراتين";
        _ar["Rep_Sales_TotalGrossProfit"] = "مجموع أرباح البيع (مفرد+كرتون+جملة)";
        _ar["Rep_Sales_Discounts"] = "الخصومات الممنوحة";
        _ar["Rep_Sales_NetProfit"] = "💎 إجمالي صافي الأرباح النهائي";
        _ar["Rep_InvoiceNo"] = "رقم الفاتورة";
        _ar["Rep_DateTime"] = "التاريخ والوقت";
        _ar["Rep_SubTotal"] = "المجموع الفرعي";
        _ar["Rep_Discount"] = "الخصم";
        _ar["Rep_GrandTotal"] = "الإجمالي النهائي";
        _ar["Rep_InvoiceNetProfit"] = "صافي ربح الفاتورة";
        _ar["Rep_Action"] = "الإجراء";
        _ar["Rep_ViewInvoice"] = "👁 عرض الوصل";

        // Damaged Report Details (Arabic)
        _ar["Rep_Damaged_TotalQty"] = "إجمالي كمية المواد التالفة";
        _ar["Rep_Damaged_TotalLoss"] = "إجمالي الخسائر المادية من التالف";
        _ar["Rep_Damaged_Date"] = "تاريخ الإتلاف";
        _ar["Rep_Barcode"] = "الباركود";
        _ar["Rep_ProductName"] = "اسم المادة";
        _ar["Rep_Quantity"] = "الكمية";
        _ar["Rep_UnitCost"] = "التكلفة/قطعة";
        _ar["Rep_LossValue"] = "قيمة الخسارة";
        _ar["Rep_Reason"] = "السبب والملاحظات";

        // Returns Report Details (Arabic)
        _ar["Rep_Returns_Count"] = "عدد الفواتير المسترجعة";
        _ar["Rep_Returns_TotalAmount"] = "إجمالي المبالغ المسترجعة للزبائن";
        _ar["Rep_Returns_InvoiceNo"] = "رقم الوصل المرتجع";
        _ar["Rep_Returns_Date"] = "تاريخ ووقت الاسترجاع";
        _ar["Rep_Returns_Amount"] = "المبلغ المسترجع";
        _ar["Rep_Returns_OrigMethod"] = "طريقة الدفع الأصلية";
        _ar["Rep_Status"] = "الحالة";

        // Purchases Report Details (Arabic)
        _ar["Rep_Pur_TotalInvoices"] = "إجمالي فواتير المشتريات";
        _ar["Rep_Pur_TotalPaid"] = "إجمالي المسدد للمناديب";
        _ar["Rep_Pur_TotalDebt"] = "الديون المتبقية للمناديب";
        _ar["Rep_Pur_InvoiceNo"] = "رقم فاتورة الشراء";
        _ar["Rep_Pur_Supplier"] = "المندوب / المورد";
        _ar["Rep_Pur_Date"] = "تاريخ التوريد";
        _ar["Rep_Pur_TotalAmount"] = "إجمالي الفاتورة";
        _ar["Rep_Pur_PaidCash"] = "المسدد نقداً";
        _ar["Rep_Pur_RemainingDebt"] = "المتبقي (آجل)";
        _ar["Rep_Notes"] = "ملاحظات";

        // Inventory Valuation Report Details (Arabic)
        _ar["Rep_Inv_CostValue"] = "قيمة المخزون بسعر الشراء (التكلفة)";
        _ar["Rep_Inv_SellingValue"] = "قيمة المخزون بسعر البيع (المتوقع)";
        _ar["Rep_Inv_ExpectedProfit"] = "الأرباح المتوقعة عند بيع المخزون";
        _ar["Rep_Inv_OutOfStock"] = "مواد نفد مخزونها (0 قطع)";
        _ar["Rep_Inv_StockBalance"] = "الرصيد المتبقي";
        _ar["Rep_Inv_CostPrice"] = "سعر التكلفة";
        _ar["Rep_Inv_SellingPrice"] = "سعر البيع";
        _ar["Rep_Inv_TotalCost"] = "إجمالي قيمة التكلفة";
        _ar["Rep_Inv_TotalSelling"] = "إجمالي قيمة البيع";
        _ar["Rep_Inv_StockStatus"] = "حالة المخزون";

        // Customer Debts Report Details (Arabic)
        _ar["Rep_Debts_TotalDue"] = "إجمالي ديون العملاء المسجلة";
        _ar["Rep_Debts_TotalCollected"] = "إجمالي المبالغ المسددة من العملاء";
        _ar["Rep_Debts_NetRemaining"] = "صافي الديون المتبقية بذمة العملاء";
        _ar["Rep_Debts_NewTitle"] = "➕ قيد دين جديد على عميل";
        _ar["Rep_Debts_CustomerName"] = "اسم العميل:";
        _ar["Rep_Debts_Phone"] = "رقم الهاتف:";
        _ar["Rep_Debts_Amount"] = "مبلغ الدين (د.ع):";
        _ar["Rep_Debts_Statement"] = "البيان والملاحظات:";
        _ar["Rep_Debts_SaveBtn"] = "تسجيل وحفظ الدين";
        _ar["Rep_Debts_TotalDebtCol"] = "إجمالي الدين";
        _ar["Rep_Debts_PaidCol"] = "المسدد";
        _ar["Rep_Debts_RemainingCol"] = "المتبقي";
        _ar["Rep_Debts_PayBtn"] = "✔ سداد";

        // Shift Audit Report Details (Arabic)
        _ar["Rep_Shift_Title"] = "🔒 التدقيق الأمني وإغلاق الوردية وتسليم المال";
        _ar["Rep_Shift_OpeningFloat"] = "العهدة النقدية الافتتاحية بالصندوق (د.ع):";
        _ar["Rep_Shift_SystemSales"] = "إجمالي المبيعات النقدية المسجلة بالنظام:";
        _ar["Rep_Shift_CountedCash"] = "المبلغ الفعلي المعدود بيدك في الدرج (د.ع):";
        _ar["Rep_Shift_MatchResult"] = "نتيجة المطابقة المحاسبية للنقد:";
        _ar["Rep_Shift_ReceiverName"] = "اسم المستلم / المشرف:";
        _ar["Rep_Shift_HandoverNotes"] = "ملاحظات التسليم والتسوية:";
        _ar["Rep_Shift_SubmitBtn"] = "اعتماد وإغلاق الوردية وطباعة التقرير";
        _ar["Rep_Shift_CloseTime"] = "تاريخ ووقت الإغلاق";
        _ar["Rep_Shift_Cashier"] = "الكاشير";
        _ar["Rep_Shift_Expected"] = "المتوقع بالدرج";
        _ar["Rep_Shift_Counted"] = "المعدود فعلياً";
        _ar["Rep_Shift_Diff"] = "الفارق المحاسبي";
        _ar["Rep_Shift_Receiver"] = "المستلم";

        // Performance Report Details (Arabic)
        _ar["Rep_Perf_PeakBanner"] = "🔥 ساعة الذروة والأكثر ازدحاماً في المحل:";
        _ar["Rep_Perf_HourSlot"] = "الفترة الزمنية (الساعة)";
        _ar["Rep_Perf_InvoicesCount"] = "عدد الفواتير";
        _ar["Rep_Perf_TotalSales"] = "مجموع المبيعات";
        _ar["Rep_Perf_PeakLevel"] = "مستوى الذروة";
        _ar["Rep_Perf_CashierName"] = "اسم الكاشير";
        _ar["Rep_Perf_CompletedInvoices"] = "الفواتير المنجزة";
        _ar["Rep_Perf_TotalRevenue"] = "إجمالي الإيراد";
        _ar["Rep_Perf_AvgSpeed"] = "متوسط السرعة";

        // Stock Movement Report Details (Arabic)
        _ar["Rep_Mov_FastTitle"] = "🔥 المواد الأكثر مبيعاً والأسرع حركة (Fast Moving)";
        _ar["Rep_Mov_DeadTitle"] = "❄️ البضائع الراكدة التي لم تباع (Stagnant Stock)";
        _ar["Rep_Mov_QtySold"] = "الكمية المباعة";
        _ar["Rep_Mov_Revenue"] = "الإيراد المحقق";
        _ar["Rep_Mov_Remaining"] = "المتبقي بالمخزن";
        _ar["Rep_Mov_Category"] = "التصنيف";
        _ar["Rep_Mov_PeriodSales"] = "المبيعات بالفترة";

        // Invoice Details Modal (Arabic)
        _ar["Rep_Modal_InvoiceDetails"] = "🧾 تفاصيل الوصل الكامل:";
        _ar["Rep_Modal_SaleDate"] = "تاريخ البيع:";
        _ar["Rep_Modal_PayMethod"] = "طريقة الدفع:";
        _ar["Rep_Modal_InvoiceStatus"] = "حالة الوصل:";
        _ar["Rep_Modal_Item"] = "المادة";
        _ar["Rep_Modal_UnitPrice"] = "سعر الوحدة";
        _ar["Rep_Modal_Quantity"] = "الكمية";
        _ar["Rep_Modal_Total"] = "المجموع";
        _ar["Rep_Modal_PaidTotal"] = "المبلغ الإجمالي المدفوع:";
        _ar["Rep_Modal_Discount"] = "الخصم الممنوح:";
        _ar["Rep_Modal_Close"] = "إغلاق";

        // Cash Drawer Extended Metrics (Arabic)
        _ar["Drawer_ItemsSold"] = "عدد المواد المباعة:";
        _ar["Drawer_GrossSales"] = "إجمالي المبيعات:";
        _ar["Drawer_ReturnsAmount"] = "إجمالي المرجوعات:";
        _ar["Drawer_NetSales"] = "صافي المبيعات (يخصم من المرجوعات):";

        // Return Mode & Sale Types & Cost Price (Arabic)
        _ar["Pos_ReturnModeBtn"] = "🔄 وضع الإرجاع";
        _ar["Pos_ReturnModeActive"] = "🔄 وضع إرجاع المواد (نشط)";
        _ar["Pos_CostPrice"] = "سعر الشراء";
        _ar["Pos_SaleTypeRetail"] = "مفرد";
        _ar["Pos_SaleTypeReturn"] = "إرجاع";

        // Sales History & Shift Archive Modal (Arabic)
        _ar["Shift_Title"] = "سجل الوصلات والورديات والفواتير المسترجعة";
        _ar["Shift_GrossSales"] = "إجمالي المبيعات:";
        _ar["Shift_Returns"] = "إجمالي المرجوعات:";
        _ar["Shift_NetSales"] = "صافي المبيعات (يخصم من المرجوعات):";
        _ar["Shift_InvoicesCount"] = "عدد الفواتير:";
        _ar["Shift_PrintReport"] = "🖨️ طباعة تقرير الوردية";
        _ar["Shift_EndShift"] = "🛑 إنهاء وقت الكاشير وإغلاق الوردية";

        // =========================================================================
        // KURDISH TRANSLATIONS (کوردی سۆرانی)
        // =========================================================================
        _ku["App_Title"] = "7amo.pos - سیستەمی پێشکەوتووی خاڵی فرۆشتن و کۆگا";
        _ku["App_Subtitle"] = "سیستەمی کاشێر و کۆگا";
        _ku["App_Currency"] = "دیناری عێراقی (د.ع)";
        _ku["App_Currency_Short"] = "د.ع";
        _ku["App_Offline_Mode"] = "لۆکاڵ (بێ ئینتەرنێت)";
        _ku["App_Active_Cashier"] = "کاشێری چالاک";
        _ku["Nav_BackToMain"] = "گەڕانەوە بۆ سەرەکی";
        _ku["Gen_Save"] = "پاشەکەوتکردن";
        _ku["Gen_Cancel"] = "پاشگەزبوونەوە";
        _ku["Add_EnableExpiry"] = "چالاککردنی بەرواری بەسەرچوون";
        _ku["Add_NoExpirySet"] = "بەبێ بەرواری بەسەرچوون";

        // Sidebar Navigation
        _ku["Nav_Dashboard"] = "تابلۆی سەرەکی (داشبۆرد)";
        _ku["Nav_Cashier"] = "خاڵی فرۆشتن (کاشێر)";
        _ku["Nav_SalesHistory"] = "مێژووی فرۆشتن و پسوولەکان";
        _ku["Nav_Purchases"] = "کڕینی کاڵا لە مەندووب";
        _ku["Nav_Warehouse"] = "کۆگا";
        _ku["Nav_RemainingStock"] = "متبقي و ماوە لە کۆگا";
        _ku["Nav_StockAudit"] = "جرد و ژماردنی کۆگا";
        _ku["Nav_Inventory"] = "بەڕێوەبردنی کۆگا و عەمبار";
        _ku["Nav_DamagedItems"] = "کاڵای تێکچوو و بەسەرچوو";
        _ku["Nav_AddProduct"] = "زیادکردنی کاڵای نوێ";
        _ku["Nav_Suppliers"] = "بەڕێوەبردنی مەندووب و دابینکەران";
        _ku["Nav_SupplierOrders"] = "داواکاریی مەندووب و مارکێتەکان";
        _ku["Nav_UserAccounts"] = "هەژمارەکانی کاشێر";
        _ku["Nav_Printing"] = "چاپ و لەزگەی نرخ و بارکۆد";
        _ku["Nav_Reports"] = "ناوەندی ڕاپۆرتە گشتگیرەکان";
        _ku["Nav_Settings"] = "ڕێکخستنەکان و تۆڕ";

        // Dashboard
        _ku["Dash_Title"] = "تابلۆی سەرەکی و ئامارە گشتییەکان";
        _ku["Dash_Subtitle"] = "تێڕوانینێکی گشتی و خێرا لە فرۆشتنی ئەمڕۆ، مانگ و ئاگادارییەکانی کۆگا";
        _ku["Dash_Refresh"] = "نوێکردنەوە";
        _ku["Dash_TodaySales"] = "فرۆشتنی ئەمڕۆ";
        _ku["Dash_TodayInvoices"] = "پسوولەکانی ئەمڕۆ";
        _ku["Dash_MonthSales"] = "فرۆشتنی ئەم مانگە";
        _ku["Dash_LowStock"] = "کاڵا نزیک لە تەواوبوون";
        _ku["Dash_LowStockAlerts"] = "ئاگاداری کەمبوونی کۆگا";
        _ku["Dash_ItemsNeedOrder"] = "کاڵای پێویست بۆ داواکردن";
        _ku["Dash_InvoiceUnit"] = "پسوولە";
        _ku["Dash_QuickActions"] = "کردار و دەستپێگەیشتنی خێرا";
        _ku["Dash_NewSale"] = "پسوولەی فرۆشتنی نوێ";
        _ku["Dash_BuyGoods"] = "کڕین و دابینکردنی کاڵا";
        _ku["Dash_QuickAudit"] = "ژماردنی خێرای کۆگا";
        _ku["Dash_SalesPerformance"] = "ئەدای فرۆشتن لە ماوەی هەفتەدا";
        _ku["Dash_TopProducts"] = "پڕفرۆشترین و داواکراوترین کاڵاکان";
        _ku["Dash_RecentInvoices"] = "نوێترین پسوولە ئەنجامدراوەکان";
        _ku["Dash_ExpiringLowStock"] = "کاڵا نزیک لە تەواوبوون";
        _ku["Dash_Time"] = "کات";
        _ku["Dash_PaymentType"] = "شێوازی پارەدان";
        _ku["Dash_InvoiceAmount"] = "بڕی گشتی";
        _ku["Dash_ChartWeeklySales"] = "هێڵکاری جوڵەی فرۆشتنی ڕۆژانە (٧ ڕۆژی ڕابردوو)";
        _ku["Dash_ChartPaymentDist"] = "دابەشبوونی فرۆشتن بەپێی شێوازی پارەدان";
        _ku["Dash_ChartTopProducts"] = "هێڵکاری پڕفرۆشترین کاڵاکان";
        _ku["Dash_ChartPeakHours"] = "کاتەکانی قەرەباڵغی و چالاکی فرۆشتن";
        _ku["Dash_WeeklyTotal"] = "کۆی گشتی فرۆشتنی هەفتە:";
        _ku["Dash_DailyAverage"] = "تێکڕای ڕۆژانە:";

        // Cashier & Table Elements (Kurdish)
        _ku["en_Dele"] = "سڕینەوە";
        _ku["Gen_Total"] = "کۆی گشتی";
        _ku["Gen_Quantity"] = "ژمارە";
        _ku["Cart_ItemsCount"] = "ژمارەی جۆرەکان:";
        _ku["Cart_TotalUnits"] = "کۆی گشتی دانەکان:";
        _ku["Cart_SubTotal"] = "کۆی فرۆشراو:";
        _ku["Cart_ExtraDiscount"] = "داشکاندنی زیاتر (د.ع):";
        _ku["Cart_FinalRequired"] = "بڕی کۆتایی پێویست بۆ دان:";

        // Drawer Cash Management (Kurdish)
        _ku["Drawer_Title"] = "سندووقی کاشێر و جوڵەی نەقد";
        _ku["Drawer_OpeningBalance"] = "پارەی دەستپێک (کراوە):";
        _ku["Drawer_CashSales"] = "فرۆشتنی نەقد (کاش):";
        _ku["Drawer_Deposits"] = "کۆی پارەی زیادکراو:";
        _ku["Drawer_Withdrawals"] = "کۆی پارەی راکێشراو:";
        _ku["Drawer_CurrentCash"] = "کۆی گشتی پارەی سندووق:";
        _ku["Drawer_AddCashBtn"] = "📥 دانانی پارە لە سندووق";
        _ku["Drawer_TakeCashBtn"] = "📤 راکێشانی پارە لە سندووق";
        _ku["Drawer_Amount"] = "بڕی پارە (د.ع)";
        _ku["Drawer_Reason"] = "هۆکار / تێبینی";
        _ku["Drawer_Confirm"] = "تەئکیدکردنەوە";
        _ku["Drawer_Cancel"] = "پاشگەزبوونەوە";
        _ku["Drawer_MovementVoucher"] = "سەندی جوڵەی پارەی سندووق";
        _ku["Drawer_PrintVoucher"] = "🖨️ چاپی سەند";
        _ku["Drawer_VoucherNumber"] = "ژمارەی سەند:";
        _ku["Drawer_MovementType"] = "جۆری جوڵە:";
        _ku["Drawer_Cashier"] = "کاشێر:";
        _ku["Drawer_Date"] = "بەروار و کات:";
        _ku["Drawer_AmountLabel"] = "بڕی پارە:";
        _ku["Drawer_ReasonLabel"] = "هۆکار و تێبینی:";
        _ku["Drawer_Close"] = "داخستن";

        // Cashier POS
        _ku["Pos_Title"] = "خاڵی فرۆشتن و کاشێری خێرا";
        _ku["Pos_SearchPlaceholder"] = "سکان یان نووسینی بارکۆد / ناوی کاڵا...";
        _ku["Pos_AddInvoice"] = "پسوولەی نوێ";
        _ku["Pos_HoldInvoices"] = "تۆماری پسوولە و شفتەکان";
        _ku["Pos_BrowseInventory"] = "کۆگا / گەڕان بەناو کاڵاکاندا";
        _ku["Pos_Drawer"] = "سندووق";
        _ku["Pos_BarcodeScanner"] = "خوێنەری بارکۆد:";
        _ku["Pos_AddManual"] = "زیادکردن بۆ پسوولە ↵";
        _ku["Pos_CartContents"] = "ناوەڕۆکی پسوولە";
        _ku["Pos_ItemsUnit"] = "دانە";
        _ku["Pos_ClearCartBtn"] = "سڕینەوە / بەتاڵکردنەوەی پسوولە";
        _ku["Pos_ItemNameBarcode"] = "ناوی کاڵا و بارکۆد";
        _ku["Pos_SaleType"] = "جۆری فرۆشتن (ڕاستەوخۆ)";
        _ku["Pos_SaleTypeRetail"] = "تاک";
        _ku["Pos_SaleTypeWholesale"] = "کۆ";
        _ku["Pos_SaleTypeCarton"] = "کارتۆن";
        _ku["Pos_UnitPrice"] = "نرخی تاک";
        _ku["Pos_PayCash"] = "پارەدانی کاش (F1 / Cash)";
        _ku["Pos_PayCard"] = "پارەدان بە کارت (Card)";
        _ku["Pos_SummaryPayment"] = "پوختەی هەژمار و پارەدان";
        _ku["Pos_ExtraDiscount"] = "داشکاندنی زیاتر (د.ع):";
        _ku["Pos_FinalRequiredAmount"] = "بڕی کۆتایی پێویست بۆ دان:";
        _ku["Pos_Cart"] = "سەبەتەی کڕین";
        _ku["Pos_SubTotal"] = "کۆی فرۆشراو:";
        _ku["Pos_Discount"] = "داشکاندنی دراو:";
        _ku["Pos_Total"] = "کۆی کۆتایی بۆ پارەدان:";
        _ku["Pos_PaymentMethod"] = "شێوازی پارەدان";
        _ku["Pos_Cash"] = "نەختینە (کاش)";
        _ku["Pos_Card"] = "کارت (ئەلیکترۆنی)";
        _ku["Pos_Debt"] = "قەرز (دواخراو)";
        _ku["Pos_CompleteSale"] = "تەواوکردنی فرۆشتن و چاپی پسوولە";
        _ku["Pos_HoldInvoice"] = "ڕاگرتنی پسوولە";
        _ku["Pos_ClearCart"] = "بەتاڵکردنەوەی سەبەتە";
        _ku["Pos_ItemsCount"] = "ژمارەی کاڵاکان";
        _ku["Pos_PiecesCount"] = "کۆی گشتی پارچەکان";

        // Inventory & Warehouse Management (بەڕێوەبردنی کۆگا و عەمبار)
        _ku["Inv_Title"] = "بەڕێوەبردنی کۆگا و عەمبار و وردەکاری کاڵاکان";
        _ku["Inv_Subtitle"] = "چاودێری نرخی کڕین، بەهای کۆگای گشتی، و بڕی کارتۆن و دانەکان";
        _ku["Inv_TotalCostValue"] = "کۆی گشتی بەهای کڕینی کۆگا";
        _ku["Inv_TotalPieces"] = "کۆی دانەکان لە کۆگادا (مفرد)";
        _ku["Inv_TotalCartons"] = "کۆی کارتۆنەکان لە کۆگا";
        _ku["Inv_TotalSellingValue"] = "کۆی بەهای فرۆشتنی چاوەڕوانکراو";
        _ku["Inv_TotalProfitValue"] = "پوختی قازانجی چاوەڕوانکراو";
        _ku["Inv_RegisteredProducts"] = "کۆی جۆری کاڵا تۆمارکراوەکان";
        _ku["Inv_AddProduct"] = "زیادکردنی کاڵای نوێ";
        _ku["Inv_Refresh"] = "نوێکردنەوەی زانیارییەکان";
        _ku["Inv_AllCategories"] = "هەموو پۆلەکان";
        _ku["Inv_SearchPlaceholder"] = "گەڕان بەپێی ناوی کاڵا، بارکۆد، یان مەندووب...";
        _ku["Inv_Barcode"] = "بارکۆد";
        _ku["Inv_ProductName"] = "ناوی کاڵا";
        _ku["Inv_Category"] = "پۆل";
        _ku["Inv_Supplier"] = "مەندووبی دابینکەر";
        _ku["Inv_SellingPrice"] = "نرخی فرۆشتنی تاک";
        _ku["Inv_CostPrice"] = "نرخی کڕین (تێچوو)";
        _ku["Inv_WholesalePrice"] = "نرخی کۆ";
        _ku["Inv_StockPieces"] = "مەوجوودی دانە";
        _ku["Inv_StockCartons"] = "مەوجوودی کارتۆن";
        _ku["Inv_TotalCostColumn"] = "کۆی تێچووی کاڵا";
        _ku["Inv_Actions"] = "کردارەکان";
        _ku["Inv_Edit"] = "✏️ دەستکاری";
        _ku["Inv_Delete"] = "🗑️ سڕینەوە";
        _ku["Inv_UnitPieces"] = "دانە";
        _ku["Inv_UnitCartons"] = "کارتۆن";
        _ku["Inv_UnitTypes"] = "جۆر";

        // Current Stock Viewer
        _ku["Stock_Title"] = "کۆگای ئێستا و کاڵا ماوەکان";
        _ku["Stock_TotalRegistered"] = "کۆی گشتی کاڵا تۆمارکراوەکان";
        _ku["Stock_TotalPieces"] = "کۆی گشتی پارچە ماوەکان لە کۆگا";
        _ku["Stock_TotalCartons"] = "کۆی گشتی کارتۆنە ماوەکان";
        _ku["Stock_ItemUnit"] = "کاڵا";
        _ku["Stock_PieceUnit"] = "دانە";
        _ku["Stock_CartonUnit"] = "کارتۆن";
        _ku["Stock_Status"] = "دۆخی کۆگا";

        // Stock Audit
        _ku["Audit_Title"] = "پشکنین و ژماردنی فیعلی کۆگا";
        _ku["Audit_SystemStock"] = "مەوجوودی لە سیستەمدا (کۆگا)";
        _ku["Audit_ActualStock"] = "ژمارەی فیعلی (ڕاستەقینە)";
        _ku["Audit_Difference"] = "جیاوازی نێوان جەرد و کۆگا";
        _ku["Audit_Apply"] = "پەسەندکردنی پشکنین و ڕاستکردنەوە";
        _ku["Audit_ShortageValue"] = "تێچووی کەمبوون (زەرەر):";
        _ku["Audit_SurplusValue"] = "کۆی بەهای زیادبوون:";
        _ku["Audit_NetDifference"] = "پوختی جیاوازی دارایی جەرد:";
        _ku["Audit_PriceCost"] = "تێچووی کاڵا (د.ع)";
        _ku["Audit_SearchBarcodeName"] = "خوێنەری بارکۆد یان گەڕان بە ناوی کاڵا:";

        // Damaged Items
        _ku["Dam_Title"] = "کاڵای تێکچوو و بەسەرچوو";
        _ku["Dam_AddDamaged"] = "تۆمارکردنی کاڵای تێکچووی نوێ";
        _ku["Dam_TotalLoss"] = "کۆی گشتی زەرەر و زیان";
        _ku["Dam_Reason"] = "هۆکاری تێکچوون";

        // Add Product Full (Kurdish)
        _ku["Add_Title"] = "زیادکردنی کاڵای نوێ بۆ کۆگا";
        _ku["Add_EditTitle"] = "دەستکاریکردنی زانیاری کاڵا";
        _ku["Add_Save"] = "پاشەکەوتکردن لە کۆگا";
        _ku["Add_BackToMain"] = "گەڕانەوە بۆ سەرەکی";
        _ku["Add_BrowseInventory"] = "پیشاندانی کاڵاکانی کۆگا بۆ دەستکاری";
        _ku["Add_ClearFields"] = "بەتاڵکردنەوەی هەموو خانەکان";
        _ku["Add_SaveButton"] = "پاشەکەوتکردنی کاڵا (Save)";
        _ku["Add_ExpiryDate"] = "📅 بەرواری بەسەرچوون:";
        _ku["Add_AlertDays"] = "ڕۆژانی ئاگاداری پێشوەختە:";
        _ku["Add_FirstAdded"] = "یەکەم زیادکردن:";
        _ku["Add_LastUpdated"] = "دواین دەستکاری:";
        _ku["Add_Barcode"] = "بارکۆد (دووجار کرتە بکە بۆ دروستکردن):";
        _ku["Add_BarcodePrefix"] = "پێشگری 200245";
        _ku["Add_ProductName"] = "ناوی کاڵا:";
        _ku["Add_SupplierName"] = "ناوی مەندووب:";
        _ku["Add_SupplierDetails"] = "➕ زانیاری مەندووب";
        _ku["Add_CartonDetailsHeader"] = "وردەکاری کارتۆن، بڕ، نرخی کڕین و تێچوو:";
        _ku["Add_CartonsCount"] = "ژمارەی کارتۆنەکان:";
        _ku["Add_ItemsInCarton"] = "دانەکانی ناو کارتۆن:";
        _ku["Add_ExtraPieces"] = "دانەی زیاتر (تاک):";
        _ku["Add_TotalStock"] = "کۆی گشتی دانەکان (خۆکار):";
        _ku["Add_CartonBuyPrice"] = "نرخی کڕینی کارتۆن (د.ع):";
        _ku["Add_PieceCost"] = "تێچووی دانەیەک (کارتۆن ÷ دانەکان):";
        _ku["Add_SellingPricingHeader"] = "نرخەکانی فرۆشتن و ئەژمارکردنی خۆکاری قازانج (تاک، کۆ، کارتۆن):";
        _ku["Add_RetailPrice"] = "نرخی فرۆشتنی تاک (د.ع):";
        _ku["Add_RetailProfitPiece"] = "قازانجی فرۆشتنی هەر دانەیەک:";
        _ku["Add_RetailCartonProfit"] = "قازانجی کارتۆن بە فرۆشتنی تاک:";
        _ku["Add_RetailMargin"] = "ڕێژەی قازانج (تاک):";
        _ku["Add_WholesalePrice"] = "نرخی فرۆشتنی کۆ (د.ع):";
        _ku["Add_WholesaleProfitPiece"] = "قازانجی فرۆشتنی کۆ بۆ دانەیەک:";
        _ku["Add_WholesaleMargin"] = "ڕێژەی قازانج (کۆ):";
        _ku["Add_CartonSellPrice"] = "نرخی فرۆشتن بە کارتۆن (د.ع):";
        _ku["Add_CartonProfitWhole"] = "قازانجی فرۆشتنی تەواوی کارتۆن:";
        _ku["Add_CartonMargin"] = "ڕێژەی قازانج (کارتۆن):";
        _ku["Add_BasicInfo"] = "زانیارییە سەرەکییەکانی کاڵا";
        _ku["Add_PricingInfo"] = "نرخەکان، پاکەت و تێچوو";
        _ku["Add_NewCategory"] = "➕ پۆلی نوێ";
        _ku["Add_CategoryModalTitle"] = "زیادکردنی پۆلی نوێی کاڵا";
        _ku["Add_CategoryName"] = "ناوی پۆلی نوێ:";
        _ku["Add_TotalCalculated"] = "کۆی گشتی کاڵا (خۆکار):";
        _ku["Add_CostCalculated"] = "تێچووی تاک (کارتۆن ÷ دانەکان):";

        // Suppliers
        _ku["Sup_Title"] = "بەڕێوەبردنی مەندووب و کۆمپانیا دابینکەرەکان";
        _ku["Sup_CardsTitle"] = "ڕێبەری کارتی مەندووب و کۆمپانیاکان";
        _ku["Sup_CardMaterialsCount"] = "جۆری کاڵاکان";
        _ku["Sup_CardCompanyLoc"] = "کۆمپانیا و شوێن:";
        _ku["Sup_CardPhone"] = "تەلەفۆن:";
        _ku["Sup_CardDebtBalance"] = "باڵانسی شایستە (قەرز):";
        _ku["Sup_BackToCards"] = "گەڕانەوە بۆ کارتی مەندووبەکان";
        _ku["Sup_InvoicesTab"] = "پسوولەکانی کڕین";
        _ku["Sup_ProductsTab"] = "کاڵا دابینکراوەکان";
        _ku["Sup_LedgerTab"] = "کەشف حیساب و پارەدان";
        _ku["Sup_AttachReceipt"] = "دانانی وێنەی پسوولە";
        _ku["Sup_ViewReceipt"] = "پیشاندانی وێنەی پسوولە";
        _ku["Sup_ReceiptModalTitle"] = "وێنەی پسوولەی هاوپێچکراوی مەندووب";
        _ku["Sup_AddSupplier"] = "زیادکردنی مەندووبی نوێ";
        _ku["Sup_SupplierBalance"] = "باڵانسی دارایی ئێستا";
        _ku["Sup_Statement"] = "کەشف حیسابی مەندووب";

        // User Accounts
        _ku["Users_Title"] = "بەڕێوەبردنی هەژمارەکانی کاشێر و فرۆشتنی ڕۆژانە";
        _ku["Users_AddUser"] = "دروستکردنی هەژماری کاشێری نوێ";
        _ku["Users_Role"] = "دەسەڵات / ڕۆڵ";

        // Purchases & Supplier Hub
        _ku["Pur_Title"] = "پسوولەی کڕینی نوێ (Stock Entry)";
        _ku["Pur_Supplier"] = "دابینکەر / مەندووب:";
        _ku["Pur_SupplierSelect"] = "ناوی مەندووب هەڵبژێرە یان بنووسە...";
        _ku["Pur_InvoiceNo"] = "پسوولە:";
        _ku["Pur_InvoicePlaceholder"] = "ژمارەی پسوولە بنووسە...";
        _ku["Pur_Date"] = "بەروار:";
        _ku["Pur_NewInvoice"] = "نوێ";
        _ku["Pur_History"] = "تۆمار";
        _ku["Pur_ProductName"] = "• ناوی کاڵا:";
        _ku["Pur_SearchPlaceholder"] = "گەڕان بۆ کاڵا یان ناوی بنووسە...";
        _ku["Pur_Quantity"] = "• ژمارە:";
        _ku["Pur_UnitType"] = "• جۆری یەکە و پاکەت:";
        _ku["Pur_Single"] = "تاک (مفرد)";
        _ku["Pur_Carton"] = "کارتۆن";
        _ku["Pur_OldCost"] = "نرخی کڕینی کۆن (بۆ کارتۆن):";
        _ku["Pur_NewCost"] = "نرخی کڕینی نوێ (بۆ کارتۆن):";
        _ku["Pur_TotalCost"] = "کۆی کڕینی کاڵاکە:";
        _ku["Pur_CalculatedPieceCost"] = "تێچووی ئەژمارکراو بۆ قازانج و ڕاپۆرت:";
        _ku["Pur_BarcodePackage"] = "بارکۆد و پاکەت";
        _ku["Pur_SellingPriceProfit"] = "نرخی فرۆشتن و قازانج";
        _ku["Pur_CostMethod"] = "شێوازی ئەژمارکردنی تێچوو (تێکڕای کێشراو)";
        _ku["Pur_AddToList"] = "زیادکردن بۆ پسوولە";
        _ku["Pur_EmptyTable"] = "خشتەی کاڵاکان بەتاڵە. شریتی سەرەوە بەکاربێنە بۆ زیادکردنی کاڵای کڕدراو.";
        _ku["Pur_TotalBill"] = "کۆی گشتی پسوولەی کڕین:";
        _ku["Pur_PaidAmount"] = "بڕی پارەی دراو بە مەندووب (د.ع):";
        _ku["Pur_RemainingDebt"] = "ماوە (قەرزی لای دوکان):";
        _ku["Pur_Clear"] = "هەڵوەشاندنەوە و بەتاڵکردن";
        _ku["Pur_Save"] = "پەسەندکردنی پسوولە و نوێکردنەوەی کۆگا";
        _ku["Pur_ModalTitle"] = "دەستکاریکردنی بارکۆد و قەبارەی کارتۆنی کاڵا";
        _ku["Pur_ModalBarcode"] = "بارکۆدی کاڵا:";
        _ku["Pur_ModalCartonSize"] = "ژمارەی دانە لەناو کارتۆندا (پاکەت):";
        _ku["Pur_ModalSave"] = "پاشەکەوت و جێبەجێکردنی دەستکاری";
        _ku["Pur_ModalCancel"] = "پاشگەزبوونەوە";

        // Reports (Kurdish)
        _ku["Rep_Title"] = "ناوەندی ڕاپۆرت و ئامارە گشتگیرەکان";
        _ku["Rep_Subtitle"] = "شیکاری وردی فرۆشتن، قازانج، کۆگا و قەرزەکان";
        _ku["Rep_Refresh"] = "نوێکردنەوە";
        _ku["Rep_Today"] = "ئەمڕۆ";
        _ku["Rep_ThisMonth"] = "ئەم مانگە";
        _ku["Rep_From"] = "لە:";
        _ku["Rep_To"] = "بۆ:";
        _ku["Rep_BackToHub"] = "گەڕانەوە بۆ ڕاپۆرتەکان";
        _ku["Rep_Tab_Sales"] = "فرۆشتن و قازانج";
        _ku["Rep_Tab_Damaged"] = "کاڵای تێکچوو و بەسەرچوو";
        _ku["Rep_Tab_Returns"] = "گەڕاوە و مسترجع";
        _ku["Rep_Tab_Purchases"] = "کڕین و دابینکردن";
        _ku["Rep_Tab_Inventory"] = "هەڵسەنگاندنی کۆگا";
        _ku["Rep_Tab_Debts"] = "قەرزی کڕیاران";
        _ku["Rep_Tab_ShiftAudit"] = "تەسلیمکردنی شفت و سندووق";
        _ku["Rep_Tab_Performance"] = "ئەدای کاشێر و قەرەباڵغی";
        _ku["Rep_Tab_StockMovement"] = "جوڵەی کاڵاکانی کۆگا";
        _ku["Rep_TotalSales"] = "کۆی گشتی فرۆشتن";
        _ku["Rep_TotalProfits"] = "پوختی قازانج";
        _ku["Rep_TotalExpenses"] = "خەرجی و زەرەرەکان";

        // Sales Report Details (Kurdish)
        _ku["Rep_Sales_SingleProfit"] = "قازانجی فرۆشتنی تاک";
        _ku["Rep_Sales_SingleTag"] = "🟢 تاک";
        _ku["Rep_Sales_SingleFormula"] = "(نرخی فرۆشتنی تاک - تێچووی دانە) × ژمارەی فرۆشراو";
        _ku["Rep_Sales_WholesaleProfit"] = "قازانجی فرۆشتنی کۆ";
        _ku["Rep_Sales_WholesaleTag"] = "🔵 کۆ";
        _ku["Rep_Sales_WholesaleFormula"] = "(نرخی فرۆشتنی کۆ - تێچووی دانە) × ژمارەی فرۆشراو";
        _ku["Rep_Sales_CartonProfit"] = "قازانجی فرۆشتنی کارتۆن";
        _ku["Rep_Sales_CartonTag"] = "🟠 کارتۆن";
        _ku["Rep_Sales_CartonFormula"] = "(نرخی فرۆشتنی کارتۆن - تێچووی کارتۆن) × ژمارەی کارتۆن";
        _ku["Rep_Sales_TotalGrossProfit"] = "کۆی قازانجی فرۆشتن (تاک+کارتۆن+کۆ)";
        _ku["Rep_Sales_Discounts"] = "داشکاندنە دراوەکان";
        _ku["Rep_Sales_NetProfit"] = "💎 پوختەی کۆتایی قازانج";
        _ku["Rep_InvoiceNo"] = "ژمارەی پسوولە";
        _ku["Rep_DateTime"] = "بەروار و کات";
        _ku["Rep_SubTotal"] = "کۆی فرۆشراو";
        _ku["Rep_Discount"] = "داشکاندن";
        _ku["Rep_GrandTotal"] = "کۆی گشتی";
        _ku["Rep_InvoiceNetProfit"] = "پوختەی قازانجی پسوولە";
        _ku["Rep_Action"] = "کردار";
        _ku["Rep_ViewInvoice"] = "👁 پیشاندانی پسوولە";

        // Damaged Report Details (Kurdish)
        _ku["Rep_Damaged_TotalQty"] = "کۆی گشتی کاڵای تێکچوو";
        _ku["Rep_Damaged_TotalLoss"] = "کۆی زەرەری دارایی لە تێکچوو";
        _ku["Rep_Damaged_Date"] = "بەرواری تێکچوون";
        _ku["Rep_Barcode"] = "بارکۆد";
        _ku["Rep_ProductName"] = "ناوی کاڵا";
        _ku["Rep_Quantity"] = "ژمارە";
        _ku["Rep_UnitCost"] = "تێچوو/دانە";
        _ku["Rep_LossValue"] = "بڕی زەرەر";
        _ku["Rep_Reason"] = "هۆکار و تێبینی";

        // Returns Report Details (Kurdish)
        _ku["Rep_Returns_Count"] = "ژمارەی پسوولە گەڕاوەکان";
        _ku["Rep_Returns_TotalAmount"] = "کۆی پارەی گەڕاوە بۆ کڕیاران";
        _ku["Rep_Returns_InvoiceNo"] = "ژمارەی پسوولەی گەڕاوە";
        _ku["Rep_Returns_Date"] = "بەروار و کاتی گەڕاندنەوە";
        _ku["Rep_Returns_Amount"] = "بڕی گەڕاوە";
        _ku["Rep_Returns_OrigMethod"] = "شێوازی پارەدانی سەرەکی";
        _ku["Rep_Status"] = "دۆخ";

        // Purchases Report Details (Kurdish)
        _ku["Rep_Pur_TotalInvoices"] = "کۆی پسوولەکانی کڕین";
        _ku["Rep_Pur_TotalPaid"] = "کۆی دراو بە مەندووب";
        _ku["Rep_Pur_TotalDebt"] = "قەرزی ماوە بۆ مەندووب";
        _ku["Rep_Pur_InvoiceNo"] = "ژمارەی پسوولەی کڕین";
        _ku["Rep_Pur_Supplier"] = "مەندووب / دابینکەر";
        _ku["Rep_Pur_Date"] = "بەرواری دابینکردن";
        _ku["Rep_Pur_TotalAmount"] = "کۆی پسوولە";
        _ku["Rep_Pur_PaidCash"] = "دراوی نەقد";
        _ku["Rep_Pur_RemainingDebt"] = "ماوە (قەرز)";
        _ku["Rep_Notes"] = "تێبینییەکان";

        // Inventory Valuation Report Details (Kurdish)
        _ku["Rep_Inv_CostValue"] = "بەهای کۆگا بەپێی نرخی کڕین (تێچوو)";
        _ku["Rep_Inv_SellingValue"] = "بەهای کۆگا بەپێی نرخی فرۆشتن";
        _ku["Rep_Inv_ExpectedProfit"] = "قازانجی چاوەڕوانکراو لە فرۆشتنی کۆگا";
        _ku["Rep_Inv_OutOfStock"] = "کاڵای تەواوبوو (٠ دانە)";
        _ku["Rep_Inv_StockBalance"] = "ماوە لە کۆگا";
        _ku["Rep_Inv_CostPrice"] = "نرخی کڕین (تێچوو)";
        _ku["Rep_Inv_SellingPrice"] = "نرخی فرۆشتن";
        _ku["Rep_Inv_TotalCost"] = "کۆی بەهای تێچوو";
        _ku["Rep_Inv_TotalSelling"] = "کۆی بەهای فرۆشتن";
        _ku["Rep_Inv_StockStatus"] = "دۆخی کۆگا";

        // Customer Debts Report Details (Kurdish)
        _ku["Rep_Debts_TotalDue"] = "کۆی گشتی قەرزی کڕیاران";
        _ku["Rep_Debts_TotalCollected"] = "کۆی پارەی وەرگیراو لە کڕیاران";
        _ku["Rep_Debts_NetRemaining"] = "پوختەی قەرزی ماوە لای کڕیاران";
        _ku["Rep_Debts_NewTitle"] = "➕ تۆمارکردنی قەرزی نوێ لەسەر کڕیار";
        _ku["Rep_Debts_CustomerName"] = "ناوی کڕیار:";
        _ku["Rep_Debts_Phone"] = "ژمارەی مۆبایل:";
        _ku["Rep_Debts_Amount"] = "بڕی قەرز (د.ع):";
        _ku["Rep_Debts_Statement"] = "ڕوونکردنەوە و تێبینی:";
        _ku["Rep_Debts_SaveBtn"] = "تۆمارکردن و پاشەکەوتی قەرز";
        _ku["Rep_Debts_TotalDebtCol"] = "کۆی قەرز";
        _ku["Rep_Debts_PaidCol"] = "دراو";
        _ku["Rep_Debts_RemainingCol"] = "ماوە";
        _ku["Rep_Debts_PayBtn"] = "✔ سداد";

        // Shift Audit Report Details (Kurdish)
        _ku["Rep_Shift_Title"] = "🔒 وردبینی ئەمنی و داخستنی شفت و تەسلیمکردنی پارە";
        _ku["Rep_Shift_OpeningFloat"] = "پارەی دەستپێکی سندووق (د.ع):";
        _ku["Rep_Shift_SystemSales"] = "کۆی فرۆشتنی نەقد لە سیستەم:";
        _ku["Rep_Shift_CountedCash"] = "بڕی پارەی ژمێردراوی ناو سندووق (د.ع):";
        _ku["Rep_Shift_MatchResult"] = "ئەنجامی بەراوردکاری ژمێریاری:";
        _ku["Rep_Shift_ReceiverName"] = "ناوی وەرگر / سەرپەرشتیار:";
        _ku["Rep_Shift_HandoverNotes"] = "تێبینی ڕادەستکردن:";
        _ku["Rep_Shift_SubmitBtn"] = "پەسەندکردن و داخستنی شفت و چاپی ڕاپۆرت";
        _ku["Rep_Shift_CloseTime"] = "بەروار و کاتی داخستن";
        _ku["Rep_Shift_Cashier"] = "کاشێر";
        _ku["Rep_Shift_Expected"] = "چاوەڕوانکراو لە سندووق";
        _ku["Rep_Shift_Counted"] = "ژمێردراوی ڕاستەقینە";
        _ku["Rep_Shift_Diff"] = "جیاوازی ژمێریاری";
        _ku["Rep_Shift_Receiver"] = "وەرگر";

        // Performance Report Details (Kurdish)
        _ku["Rep_Perf_PeakBanner"] = "🔥 کاتی قەرەباڵغی و پڕفرۆشترین کات لە دوکان:";
        _ku["Rep_Perf_HourSlot"] = "ماوەی کاتی (کاتژمێر)";
        _ku["Rep_Perf_InvoicesCount"] = "ژمارەی پسوولە";
        _ku["Rep_Perf_TotalSales"] = "کۆی فرۆشتن";
        _ku["Rep_Perf_PeakLevel"] = "ئاستی قەرەباڵغی";
        _ku["Rep_Perf_CashierName"] = "ناوی کاشێر";
        _ku["Rep_Perf_CompletedInvoices"] = "پسوولەی تەواوکراو";
        _ku["Rep_Perf_TotalRevenue"] = "کۆی داهات";
        _ku["Rep_Perf_AvgSpeed"] = "تێکڕای خێرایی";

        // Stock Movement Report Details (Kurdish)
        _ku["Rep_Mov_FastTitle"] = "🔥 پڕفرۆشترین و خێراترین کاڵاکان لە جوڵەدا (Fast Moving)";
        _ku["Rep_Mov_DeadTitle"] = "❄️ کاڵا مەند و سستەکان کە نەفرۆشراون (Stagnant Stock)";
        _ku["Rep_Mov_QtySold"] = "ژمارەی فرۆشراو";
        _ku["Rep_Mov_Revenue"] = "داهاتی بەدەستهاتوو";
        _ku["Rep_Mov_Remaining"] = "ماوە لە کۆگا";
        _ku["Rep_Mov_Category"] = "پۆلێن";
        _ku["Rep_Mov_PeriodSales"] = "فرۆشتن لە ماوەکەدا";

        // Invoice Details Modal (Kurdish)
        _ku["Rep_Modal_InvoiceDetails"] = "🧾 وردەکاری تەواوی پسوولە:";
        _ku["Rep_Modal_SaleDate"] = "بەرواری فرۆشتن:";
        _ku["Rep_Modal_PayMethod"] = "شێوازی پارەدان:";
        _ku["Rep_Modal_InvoiceStatus"] = "دۆخی پسوولە:";
        _ku["Rep_Modal_Item"] = "کاڵا";
        _ku["Rep_Modal_UnitPrice"] = "نرخی تاک";
        _ku["Rep_Modal_Quantity"] = "ژمارە";
        _ku["Rep_Modal_Total"] = "کۆی گشتی";
        _ku["Rep_Modal_PaidTotal"] = "کۆی پارەی دراو:";
        _ku["Rep_Modal_Discount"] = "داشکاندنی دراو:";
        _ku["Rep_Modal_Close"] = "داخستن";

        // Cash Drawer Extended Metrics (Kurdish)
        _ku["Drawer_ItemsSold"] = "ژمارەی کاڵا فرۆشراوەکان:";
        _ku["Drawer_GrossSales"] = "کۆی گشتی فرۆشتن:";
        _ku["Drawer_ReturnsAmount"] = "کۆی گشتی گەڕاوەکان:";
        _ku["Drawer_NetSales"] = "پوختەی فرۆشتن (دوای کەمکردنەوەی گەڕاوە):";

        // Return Mode & Sale Types & Cost Price (Kurdish)
        _ku["Pos_ReturnModeBtn"] = "🔄 دۆخی گەڕاندنەوە";
        _ku["Pos_ReturnModeActive"] = "🔄 دۆخی گەڕاندنەوەی کاڵا (چالاکە)";
        _ku["Pos_CostPrice"] = "نرخی کڕین";
        _ku["Pos_SaleTypeRetail"] = "تاک";
        _ku["Pos_SaleTypeWholesale"] = "کۆ";
        _ku["Pos_SaleTypeCarton"] = "کارتۆن";
        _ku["Pos_SaleTypeReturn"] = "گەڕاندنەوە";

        // Sales History & Shift Archive Modal (Kurdish)
        _ku["Shift_Title"] = "تۆماری وەسڵ و وردیە و گەڕاوەکان";
        _ku["Shift_GrossSales"] = "کۆی گشتی فرۆشتن:";
        _ku["Shift_Returns"] = "کۆی گشتی گەڕاوەکان:";
        _ku["Shift_NetSales"] = "پوختەی فرۆشتن (دوای کەمکردنەوەی گەڕاوە):";
        _ku["Shift_InvoicesCount"] = "ژمارەی وەسڵەکان:";
        _ku["Shift_PrintReport"] = "🖨️ چاپکردنی راپۆرتی وردیە";
        _ku["Shift_EndShift"] = "🛑 کۆتاییهێنان بە وردیە و دەرچوون";
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
