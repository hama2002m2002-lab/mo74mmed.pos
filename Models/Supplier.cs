using System.Collections.Generic;

namespace HamoPos.Models;

/// <summary>
/// نموذج المندوب / المورد مع سجل المواد الموردة وحسابات المشتريات والدفعات
/// </summary>
public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Company { get; set; }
    public string? Address { get; set; }
    public decimal OpeningBalance { get; set; } = 0.0m;
    public decimal Balance { get; set; } = 0.0m;
    public string? Notes { get; set; }

    // قائمة المواد المرتبطة بهذا المندوب
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    // سجل المعاملات المالية وفواتير الشراء والسدادات
    public virtual ICollection<SupplierTransaction> Transactions { get; set; } = new List<SupplierTransaction>();
}
