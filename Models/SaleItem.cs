using System;

namespace HamoPos.Models;

/// <summary>
/// بند أو صنف داخل الفاتورة
/// </summary>
public class SaleItem : BaseEntity
{
    public Guid SaleId { get; set; }
    public virtual Sale? Sale { get; set; }

    public Guid? ProductId { get; set; }
    public virtual Product? Product { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; } = 0.0m;
    public decimal Quantity { get; set; } = 1.0m;
    public decimal DiscountAmount { get; set; } = 0.0m;
    public decimal TaxRate { get; set; } = 0.15m;
    public decimal TaxAmount { get; set; } = 0.0m;
    public decimal TotalPrice { get; set; } = 0.0m;
}
