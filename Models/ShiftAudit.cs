using System;

namespace HamoPos.Models;

/// <summary>
/// نموذج التدقيق الأمني وإغلاق وردية الكاشير وتسليم العهدة المالية (Z-Report)
/// </summary>
public class ShiftAudit : BaseEntity
{
    public string CashierName { get; set; } = string.Empty;
    public DateTime ShiftStartTime { get; set; }
    public DateTime ShiftEndTime { get; set; }
    public decimal OpeningBalance { get; set; } // رصيد الصندوق عند بدء الوردية
    public decimal TotalSalesCash { get; set; } // المبيعات النقدية المسجلة بالنظام
    public decimal TotalSalesCard { get; set; } // المبيعات عبر البطاقة
    public decimal TotalReturnsCash { get; set; } // المرتجعات النقدية
    public decimal ExpectedCashInDrawer => OpeningBalance + TotalSalesCash - TotalReturnsCash; // المتوقع نظرياً بالدرج
    public decimal ActualCountedCash { get; set; } // النقد الفعلي المعدود باليد
    public decimal Discrepancy => ActualCountedCash - ExpectedCashInDrawer; // الفارق (زيادة + أو عجز -)
    public string DiscrepancyStatus => Discrepancy == 0 ? "مطابق تماماً ✔" : (Discrepancy > 0 ? $"فائض +{Discrepancy:N0} د.ع" : $"عجز {Discrepancy:N0} د.ع");
    public string HandoverNotes { get; set; } = string.Empty;
    public string SupervisorName { get; set; } = string.Empty;
}
