using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace HamoPos.Models;

/// <summary>
/// نموذج الفاتورة / عملية البيع
/// </summary>
public class Sale : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid? UserId { get; set; }
    public virtual User? User { get; set; }

    public decimal SubTotal { get; set; } = 0.0m;
    public decimal TaxAmount { get; set; } = 0.0m;
    public decimal DiscountAmount { get; set; } = 0.0m;
    public decimal TotalAmount { get; set; } = 0.0m;
    
    public decimal PaidAmount { get; set; } = 0.0m;
    public decimal ChangeAmount { get; set; } = 0.0m;

    public string PaymentMethod { get; set; } = "Cash"; // "Cash", "Card", "Split"
    public string Status { get; set; } = "Completed";   // "Completed", "Held", "Cancelled", "Refunded"
    public string? CustomerName { get; set; }
    public string? Notes { get; set; }

    public virtual ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();

    [NotMapped]
    public bool IsReturnSale => Status == "Returned" || (!string.IsNullOrEmpty(InvoiceNumber) && InvoiceNumber.StartsWith("RET-"));

    [NotMapped]
    public bool IsAlreadyReturned => IsReturnSale || (!string.IsNullOrEmpty(Notes) && Notes.Contains("تم الاسترجاع"));

    [NotMapped]
    public decimal InvoiceNetProfit
    {
        get
        {
            decimal costSum = 0;
            foreach (var item in Items)
            {
                if (item.ProductName.Contains("(كرتون)"))
                {
                    decimal cartonCost = (item.Product != null && item.Product.CartonPurchasePrice > 0)
                        ? item.Product.CartonPurchasePrice
                        : ((item.Product?.Cost ?? 0m) * (item.Product?.ItemsPerCarton > 0 ? item.Product.ItemsPerCarton : 1));
                    costSum += (cartonCost * item.Quantity);
                }
                else
                {
                    decimal pieceCost = item.Product?.Cost ?? 0m;
                    costSum += (pieceCost * item.Quantity);
                }
            }
            return (TotalAmount - costSum);
        }
    }
}
