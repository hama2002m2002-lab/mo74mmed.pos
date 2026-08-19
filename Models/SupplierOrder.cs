using System;
using System.Collections.Generic;

namespace HamoPos.Models;

public enum OrderStatus
{
    Pending,        // قيد الانتظار / نوێ
    InPreparation,  // جاري التجهيز / لە ئامادەکردندا
    Delivered,      // تم التوصيل والتوريد / گەیەندرا
    Cancelled       // ملغية / هەڵوەشاوەتەوە
}

/// <summary>
/// نموذج طلبية واردة من مندوب / ماركت
/// </summary>
public class SupplierOrder : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; } = DateTime.Now;

    // معلومات الماركت / العميل
    public string MarketName { get; set; } = string.Empty;
    public string? MarketPhone { get; set; }
    public string? MarketAddress { get; set; }

    // معلومات المندوب / المورد المسؤول
    public Guid? SupplierId { get; set; }
    public virtual Supplier? Supplier { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string RepresentativeName { get; set; } = string.Empty;

    // الحالة والمبالغ
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; } = 0.0m;
    public string? Notes { get; set; }
    public bool IsConvertedToInvoice { get; set; } = false;

    // بنود ومواد الطلبية
    public virtual ICollection<SupplierOrderItem> Items { get; set; } = new List<SupplierOrderItem>();
}

/// <summary>
/// مواد وبنود طلبية المندوب
/// </summary>
public class SupplierOrderItem : BaseEntity
{
    public Guid SupplierOrderId { get; set; }
    public virtual SupplierOrder? SupplierOrder { get; set; }

    public Guid? ProductId { get; set; }
    public virtual Product? Product { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;
    public string UnitType { get; set; } = "Retail"; // Retail, Wholesale, Carton
    public decimal UnitPrice { get; set; } = 0.0m;
    public decimal TotalPrice => Quantity * UnitPrice;
    public string? Notes { get; set; }
}
