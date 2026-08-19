using System;

namespace HamoPos.Models;

/// <summary>
/// سند مالي أو معاملة شراء/سداد للمندوب أو المورد
/// </summary>
public class SupplierTransaction : BaseEntity
{
    public Guid SupplierId { get; set; }
    public virtual Supplier? Supplier { get; set; }

    public string TransactionType { get; set; } = "Payment"; // "Purchase", "Payment"
    public decimal Amount { get; set; } = 0.0m;
    public string? Description { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}
