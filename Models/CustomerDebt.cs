using System;

namespace HamoPos.Models;

/// <summary>
/// نموذج حسابات ديون العملاء والآجل
/// </summary>
public class CustomerDebt : BaseEntity
{
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal TotalDebt { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RemainingBalance => TotalDebt - TotalPaid;
    public string LastTransactionType { get; set; } = "دين مشتريات"; // دين مشتريات, سداد دفعة
    public string? Notes { get; set; }
}
