using System;

namespace HamoPos.Models;

/// <summary>
/// نموذج المادة / المنتج مع ربط مباشر بالمندوب، وتفاصيل الكرتون والأسعار والأرباح والصلاحية
/// </summary>
public class Product : BaseEntity
{
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? CategoryId { get; set; }
    public virtual Category? Category { get; set; }

    // ربط المندوب والمورد
    public Guid? SupplierId { get; set; }
    public virtual Supplier? Supplier { get; set; }
    public string? SupplierName { get; set; }

    // تفاصيل الكرتون والكميات
    public decimal CartonsCount { get; set; } = 0.0m;
    public decimal ItemsPerCarton { get; set; } = 1.0m;
    public decimal StockQuantity { get; set; } = 0.0m; // مجموع المواد الكلي
    public decimal MinStockAlert { get; set; } = 5.0m;
    public string Unit { get; set; } = "قطعة";

    // أسعار الشراء والتكلفة
    public decimal CartonPurchasePrice { get; set; } = 0.0m; // سعر شراء الكرتون
    public decimal Cost { get; set; } = 0.0m; // تكلفة القطعة المفردة

    // أسعار البيع
    public decimal Price { get; set; } = 0.0m; // سعر بيع المفرد
    public decimal WholesalePrice { get; set; } = 0.0m; // سعر بيع الجملة
    public decimal CartonSellingPrice { get; set; } = 0.0m; // سعر بيع الكرتون

    // الأرباح التقديرية
    public decimal RetailProfit => Math.Max(0, Price - Cost);
    public decimal WholesaleProfit => Math.Max(0, WholesalePrice - Cost);
    public decimal CartonProfit => Math.Max(0, CartonSellingPrice - CartonPurchasePrice);

    // تواريخ الصلاحية والتحذيرات
    public DateTime? ExpiryDate { get; set; }
    public int ExpiryAlertDays { get; set; } = 30; // عدد أيام التحذير المسبق

    public decimal TaxRate { get; set; } = 0.0m;
    public string? ImageUrl { get; set; }
    public string? ColorHex { get; set; } = "#1E293B";
    public bool IsActive { get; set; } = true;
}
