using System;

namespace HamoPos.Models;

/// <summary>
/// نموذج تسجيل المواد التالفة أو المنتهية الصلاحية أو الهالكة
/// </summary>
public class DamagedItem : BaseEntity
{
    public Guid ProductId { get; set; }
    public virtual Product? Product { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1.0m;
    public decimal UnitCost { get; set; } = 0.0m;
    public decimal TotalLossAmount => Quantity * UnitCost;

    public string Reason { get; set; } = "تالف"; // "منتهي الصلاحية", "تالف / كسر", "سوء تخزين", "أخرى"
    public string? Notes { get; set; }
}
