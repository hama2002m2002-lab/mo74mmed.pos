using System;
using System.Collections.Generic;

namespace HamoPos.Models;

/// <summary>
/// نموذج فاتورة الشراء والتوريد من المندوب
/// </summary>
public class PurchaseInvoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public virtual Supplier? Supplier { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount => TotalAmount - PaidAmount;
    public string PaymentMethod { get; set; } = "Cash"; // Cash, Debt, Partial
    public string? Notes { get; set; }
    public string? ReceiptImagePath { get; set; } // مسار صورة وصل المندوب المرفقة

    public virtual ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
}

/// <summary>
/// بنود ومواد فاتورة الشراء
/// </summary>
public class PurchaseInvoiceItem : BaseEntity
{
    public Guid PurchaseInvoiceId { get; set; }
    public virtual PurchaseInvoice? PurchaseInvoice { get; set; }

    public Guid ProductId { get; set; }
    public virtual Product? Product { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal TotalCost => Quantity * UnitCost;
    public bool IsCarton { get; set; }
}
