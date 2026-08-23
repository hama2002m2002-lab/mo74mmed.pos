using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.Services;

public class PosBridgeService
{
    private static PosBridgeService? _instance;
    public static PosBridgeService Instance => _instance ??= new PosBridgeService();

    public async Task<string> HandleMessageAsync(string action, string payloadJson)
    {
        try
        {
            using var db = new AppDbContext();

            switch (action)
            {
                // ==========================================
                // 1. DASHBOARD & STATS
                // ==========================================
                case "get_dashboard_data":
                {
                    var saleService = new SaleService(db);
                    var prodService = new ProductService(db);

                    var todayStats = await saleService.GetTodayStatsAsync();
                    var monthStats = await saleService.GetMonthlyStatsAsync();
                    int totalProducts = await prodService.GetTotalProductsCountAsync();
                    var lowStock = await prodService.GetLowStockProductsAsync(8);
                    var recentSales = await saleService.GetRecentSalesAsync(6);

                    // Weekly sales trend (last 7 days)
                    var today = DateTime.UtcNow.Date;
                    var sevenDaysAgo = today.AddDays(-6);
                    var pastWeekSales = await db.Sales.AsNoTracking()
                        .Where(s => s.CreatedAt >= sevenDaysAgo && s.Status == "Completed")
                        .ToListAsync();

                    var weeklyBars = new List<object>();
                    for (int i = 6; i >= 0; i--)
                    {
                        var d = today.AddDays(-i);
                        var daySales = pastWeekSales.Where(s => s.CreatedAt.Date == d).ToList();
                        weeklyBars.Add(new
                        {
                            dayName = d.ToString("ddd", new System.Globalization.CultureInfo("ar-IQ")),
                            shortDate = d.ToString("MM/dd"),
                            revenue = daySales.Sum(s => s.TotalAmount),
                            count = daySales.Count,
                            isToday = (i == 0)
                        });
                    }

                    // Payment breakdown
                    var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    var monthSales = await db.Sales.AsNoTracking()
                        .Where(s => s.CreatedAt >= startOfMonth && s.Status == "Completed")
                        .ToListAsync();

                    decimal cashMonth = monthSales.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount);
                    decimal cardMonth = monthSales.Where(s => s.PaymentMethod == "Card" || s.PaymentMethod == "Visa").Sum(s => s.TotalAmount);
                    decimal debtMonth = monthSales.Where(s => s.PaymentMethod == "Debt").Sum(s => s.TotalAmount);

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        todayRevenue = todayStats.TotalRevenue,
                        todayInvoices = todayStats.TotalSalesCount,
                        monthlyRevenue = monthStats.MonthlyRevenue,
                        totalProducts = totalProducts,
                        lowStockCount = lowStock.Count,
                        lowStockProducts = lowStock.Select(p => new { p.Id, p.Name, p.StockQuantity, p.MinStockAlert, p.Price, p.Cost }),
                        recentSales = recentSales.Select(s => new { s.Id, s.InvoiceNumber, s.TotalAmount, s.PaymentMethod, createdAt = s.CreatedAt.ToString("yyyy/MM/dd hh:mm tt") }),
                        weeklyTrend = weeklyBars,
                        payments = new { cash = cashMonth, card = cardMonth, debt = debtMonth }
                    });
                }

                // ==========================================
                // 2. POS CASHIER & PRODUCTS
                // ==========================================
                case "get_pos_products":
                {
                    var prods = await db.Products
                        .Include(p => p.Category)
                        .AsNoTracking()
                        .Where(p => !p.IsDeleted)
                        .OrderBy(p => p.Name)
                        .ToListAsync();

                    var categories = prods.Select(p => p.Category?.Name).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        products = prods.Select(p => new
                        {
                            p.Id,
                            p.Name,
                            p.Barcode,
                            p.Price,
                            p.WholesalePrice,
                            p.Cost,
                            p.StockQuantity,
                            p.Unit,
                            piecesPerCarton = p.ItemsPerCarton,
                            category = p.Category?.Name ?? "عام",
                            imagePath = p.ImageUrl
                        }),
                        categories
                    });
                }

                case "find_product":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    string query = doc.RootElement.TryGetProperty("query", out var qp) ? qp.GetString()?.Trim() ?? "" : "";
                    if (string.IsNullOrEmpty(query)) return JsonSerializer.Serialize(new { success = false, found = false });

                    var prod = await db.Products
                        .Include(p => p.Category)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => !p.IsDeleted && (
                            p.Barcode == query ||
                            (p.Barcode != null && p.Barcode.Trim() == query) ||
                            p.Name.ToLower() == query.ToLower() ||
                            p.Name.Contains(query) ||
                            (p.Barcode != null && p.Barcode.Contains(query))
                        ));

                    if (prod != null)
                    {
                        return JsonSerializer.Serialize(new
                        {
                            success = true,
                            found = true,
                            product = new
                            {
                                prod.Id,
                                prod.Name,
                                prod.Barcode,
                                prod.Price,
                                prod.WholesalePrice,
                                prod.Cost,
                                prod.StockQuantity,
                                prod.Unit,
                                piecesPerCarton = prod.ItemsPerCarton,
                                category = prod.Category?.Name ?? "عام",
                                imagePath = prod.ImageUrl
                            }
                        });
                    }
                    return JsonSerializer.Serialize(new { success = true, found = false });
                }

                case "complete_sale":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var root = doc.RootElement;

                    string invoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}-{new Random().Next(10, 99)}";
                    string paymentMethod = root.TryGetProperty("paymentMethod", out var pm) ? pm.GetString() ?? "Cash" : "Cash";
                    decimal discount = root.TryGetProperty("discount", out var ds) ? ds.GetDecimal() : 0m;
                    string notes = root.TryGetProperty("notes", out var nt) ? nt.GetString() ?? "" : "";
                    string customerName = root.TryGetProperty("customerName", out var cn) ? cn.GetString() ?? "زبون نقدي" : "زبون نقدي";

                    var itemsJson = root.GetProperty("items");
                    var saleItems = new List<SaleItem>();
                    decimal totalAmount = 0;
                    decimal totalCost = 0;

                    foreach (var it in itemsJson.EnumerateArray())
                    {
                        var prodId = it.GetProperty("id").GetGuid();
                        var qty = it.GetProperty("qty").GetDecimal();
                        var price = it.GetProperty("price").GetDecimal();
                        var cost = it.TryGetProperty("cost", out var cs) ? cs.GetDecimal() : 0m;
                        var name = it.GetProperty("name").GetString() ?? "";

                        decimal itemTotal = qty * price;
                        totalAmount += itemTotal;
                        totalCost += (qty * cost);

                        saleItems.Add(new SaleItem
                        {
                            Id = Guid.NewGuid(),
                            ProductId = prodId,
                            ProductName = name,
                            Quantity = qty,
                            UnitPrice = price,
                            TotalPrice = itemTotal
                        });

                        // Deduct Stock
                        var dbProd = await db.Products.FindAsync(prodId);
                        if (dbProd != null)
                        {
                            dbProd.StockQuantity = Math.Max(0, dbProd.StockQuantity - qty);
                            dbProd.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    decimal finalAmount = Math.Max(0, totalAmount - discount);

                    var sale = new Sale
                    {
                        Id = Guid.NewGuid(),
                        InvoiceNumber = invoiceNumber,
                        SubTotal = totalAmount,
                        TotalAmount = finalAmount,
                        DiscountAmount = discount,
                        PaidAmount = finalAmount,
                        PaymentMethod = paymentMethod,
                        Notes = notes,
                        CustomerName = customerName,
                        Status = "Completed",
                        CreatedAt = DateTime.UtcNow,
                        Items = saleItems
                    };

                    await db.Sales.AddAsync(sale);
                    await db.SaveChangesAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        invoiceNumber = sale.InvoiceNumber,
                        total = sale.TotalAmount,
                        discount = sale.DiscountAmount,
                        itemsCount = saleItems.Count,
                        date = sale.CreatedAt.ToString("yyyy/MM/dd hh:mm tt")
                    });
                }

                case "get_receipts_and_returns":
                {
                    var allSales = await db.Sales
                        .Include(s => s.Items)
                        .AsNoTracking()
                        .OrderByDescending(s => s.CreatedAt)
                        .Take(500)
                        .ToListAsync();

                    var completedSales = allSales.Where(s => s.Status == "Completed" && !s.IsReturnSale).ToList();
                    var returnedSales = allSales.Where(s => s.Status == "Returned" || s.IsReturnSale).ToList();

                    decimal totalSalesAmount = completedSales.Sum(s => s.TotalAmount);
                    int totalSalesCount = completedSales.Count;

                    decimal totalReturnedAmount = returnedSales.Sum(s => s.TotalAmount);
                    int totalReturnedCount = returnedSales.Count;

                    var receiptsList = allSales.Select(s => new
                    {
                        id = s.Id,
                        invoiceNumber = s.InvoiceNumber,
                        customerName = s.CustomerName ?? "زبون نقدي",
                        totalAmount = s.TotalAmount,
                        subTotal = s.SubTotal,
                        discountAmount = s.DiscountAmount,
                        paymentMethod = s.PaymentMethod ?? "Cash",
                        status = s.Status,
                        isReturn = s.Status == "Returned" || s.IsReturnSale,
                        itemsCount = s.Items.Count,
                        date = s.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"),
                        items = s.Items.Select(i => new
                        {
                            name = i.ProductName,
                            qty = i.Quantity,
                            price = i.UnitPrice,
                            total = i.TotalPrice
                        })
                    }).ToList();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        totalSalesAmount,
                        totalSalesCount,
                        totalReturnedAmount,
                        totalReturnedCount,
                        receipts = receiptsList
                    });
                }

                case "return_receipt":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    string invNum = doc.RootElement.TryGetProperty("invoiceNumber", out var ip) ? ip.GetString()?.Trim() ?? "" : "";
                    
                    var targetSale = await db.Sales
                        .Include(s => s.Items)
                        .FirstOrDefaultAsync(s => s.InvoiceNumber == invNum);

                    if (targetSale == null)
                    {
                        return JsonSerializer.Serialize(new { success = false, message = "لم يتم العثور على الفاتورة المطلوبة" });
                    }

                    if (targetSale.Status == "Returned")
                    {
                        return JsonSerializer.Serialize(new { success = false, message = "هذه الفاتورة تم إرجاعها مسبقاً!" });
                    }

                    // Restore stock quantities
                    foreach (var item in targetSale.Items)
                    {
                        if (item.ProductId.HasValue)
                        {
                            var prod = await db.Products.FindAsync(item.ProductId.Value);
                            if (prod != null)
                            {
                                prod.StockQuantity += item.Quantity;
                                prod.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }

                    targetSale.Status = "Returned";
                    targetSale.Notes = (targetSale.Notes ?? "") + " [تم الاسترجاع بالكامل]";
                    await db.SaveChangesAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        invoiceNumber = targetSale.InvoiceNumber,
                        refundAmount = targetSale.TotalAmount
                    });
                }

                // ==========================================
                // 3. INVENTORY & STOCK
                // ==========================================
                case "get_inventory":
                {
                    var prods = await db.Products
                        .Include(p => p.Category)
                        .AsNoTracking()
                        .Where(p => !p.IsDeleted)
                        .OrderByDescending(p => p.CreatedAt)
                        .ToListAsync();

                    decimal totalCostVal = prods.Sum(p => p.Cost * p.StockQuantity);
                    decimal totalSellVal = prods.Sum(p => p.Price * p.StockQuantity);
                    decimal totalProfit = Math.Max(0, totalSellVal - totalCostVal);
                    decimal totalPieces = prods.Sum(p => p.StockQuantity);

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        totalCostValue = totalCostVal,
                        totalSellingValue = totalSellVal,
                        expectedProfit = totalProfit,
                        totalPieces = totalPieces,
                        products = prods.Select(p => new
                        {
                            id = p.Id,
                            name = p.Name,
                            barcode = p.Barcode ?? "",
                            category = p.Category?.Name ?? "عام",
                            supplierName = p.SupplierName ?? "",
                            cartonsCount = p.CartonsCount > 0 ? p.CartonsCount : (p.ItemsPerCarton > 0 ? Math.Floor(p.StockQuantity / p.ItemsPerCarton) : 0),
                            piecesPerCarton = p.ItemsPerCarton,
                            stockQuantity = p.StockQuantity,
                            minStockAlert = p.MinStockAlert,
                            cartonPurchasePrice = p.CartonPurchasePrice,
                            cost = p.Cost,
                            price = p.Price,
                            wholesalePrice = p.WholesalePrice,
                            cartonSellingPrice = p.CartonSellingPrice,
                            retailProfit = p.RetailProfit,
                            wholesaleProfit = p.WholesaleProfit,
                            cartonProfit = p.CartonProfit,
                            totalCost = p.Cost * p.StockQuantity,
                            totalRetailValue = p.Price * p.StockQuantity,
                            createdAt = p.CreatedAt.ToString("yyyy/MM/dd")
                        })
                    });
                }

                case "save_product":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var r = doc.RootElement;
                    Guid? id = r.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String && Guid.TryParse(idProp.GetString(), out var g) ? g : null;

                    string name = r.GetProperty("name").GetString() ?? "";
                    string barcode = r.TryGetProperty("barcode", out var bp) ? bp.GetString() ?? "" : "";
                    string categoryName = r.TryGetProperty("category", out var cp) ? cp.GetString() ?? "عام" : "عام";
                    string supplierName = r.TryGetProperty("supplierName", out var supProp) ? supProp.GetString() ?? "" : "";
                    decimal cost = r.TryGetProperty("cost", out var cst) ? cst.GetDecimal() : 0m;
                    decimal price = r.TryGetProperty("price", out var prc) ? prc.GetDecimal() : 0m;
                    decimal wholesale = r.TryGetProperty("wholesalePrice", out var wp) ? wp.GetDecimal() : 0m;
                    decimal cartonPurchase = r.TryGetProperty("cartonPurchasePrice", out var cpp) ? cpp.GetDecimal() : 0m;
                    decimal cartonSelling = r.TryGetProperty("cartonSellingPrice", out var csp) ? csp.GetDecimal() : 0m;
                    decimal stock = r.TryGetProperty("stockQuantity", out var sq) ? sq.GetDecimal() : 0m;
                    decimal cartonsCount = r.TryGetProperty("cartonsCount", out var ccp) ? ccp.GetDecimal() : 0m;
                    decimal minAlert = r.TryGetProperty("minStockAlert", out var ma) ? ma.GetDecimal() : 5m;
                    decimal itemsPerCarton = r.TryGetProperty("piecesPerCarton", out var ppc) ? ppc.GetDecimal() : 1m;

                    // Ensure category
                    var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == categoryName);
                    if (category == null)
                    {
                        category = new Category { Id = Guid.NewGuid(), Name = categoryName, CreatedAt = DateTime.UtcNow };
                        await db.Categories.AddAsync(category);
                        await db.SaveChangesAsync();
                    }

                    if (id.HasValue)
                    {
                        var existing = await db.Products.FindAsync(id.Value);
                        if (existing != null)
                        {
                            existing.Name = name;
                            existing.Barcode = barcode;
                            existing.CategoryId = category.Id;
                            existing.SupplierName = supplierName;
                            existing.Cost = cost;
                            existing.Price = price;
                            existing.WholesalePrice = wholesale;
                            existing.CartonPurchasePrice = cartonPurchase;
                            existing.CartonSellingPrice = cartonSelling;
                            existing.StockQuantity = stock;
                            existing.CartonsCount = cartonsCount;
                            existing.MinStockAlert = minAlert;
                            existing.ItemsPerCarton = itemsPerCarton;
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        var newProd = new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = name,
                            Barcode = string.IsNullOrWhiteSpace(barcode) ? DateTime.Now.Ticks.ToString() : barcode,
                            CategoryId = category.Id,
                            SupplierName = supplierName,
                            Cost = cost,
                            Price = price,
                            WholesalePrice = wholesale,
                            CartonPurchasePrice = cartonPurchase,
                            CartonSellingPrice = cartonSelling,
                            StockQuantity = stock,
                            CartonsCount = cartonsCount,
                            MinStockAlert = minAlert,
                            ItemsPerCarton = itemsPerCarton,
                            CreatedAt = DateTime.UtcNow
                        };
                        await db.Products.AddAsync(newProd);
                    }

                    await db.SaveChangesAsync();
                    return JsonSerializer.Serialize(new { success = true });
                }

                case "delete_product":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var id = doc.RootElement.GetProperty("id").GetGuid();
                    var p = await db.Products.FindAsync(id);
                    if (p != null)
                    {
                        p.IsDeleted = true;
                        p.UpdatedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }
                    return JsonSerializer.Serialize(new { success = true });
                }

                case "get_categories":
                {
                    var cats = await db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
                    return JsonSerializer.Serialize(new { success = true, categories = cats });
                }

                case "add_category":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    string catName = doc.RootElement.GetProperty("name").GetString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(catName))
                    {
                        var exists = await db.Categories.AnyAsync(c => c.Name == catName);
                        if (!exists)
                        {
                            db.Categories.Add(new Category { Id = Guid.NewGuid(), Name = catName, CreatedAt = DateTime.UtcNow });
                            await db.SaveChangesAsync();
                        }
                    }
                    var cats = await db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
                    return JsonSerializer.Serialize(new { success = true, categories = cats });
                }

                case "delete_category":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    string catName = doc.RootElement.GetProperty("name").GetString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(catName) && catName != "عام")
                    {
                        var cat = await db.Categories.FirstOrDefaultAsync(c => c.Name == catName);
                        if (cat != null)
                        {
                            db.Categories.Remove(cat);
                            await db.SaveChangesAsync();
                        }
                    }
                    var cats = await db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
                    return JsonSerializer.Serialize(new { success = true, categories = cats });
                }

                // ==========================================
                // 4. REP ORDERS & NOTIFICATIONS
                // ==========================================
                case "get_supplier_orders":
                {
                    var orders = await db.SupplierOrders
                        .Include(o => o.Items)
                        .OrderByDescending(o => o.OrderDate)
                        .ToListAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        orders = orders.Select(o => new
                        {
                            o.Id,
                            o.OrderNumber,
                            o.RepresentativeName,
                            o.MarketName,
                            o.MarketPhone,
                            marketCity = o.MarketAddress,
                            o.TotalAmount,
                            status = o.Status.ToString(),
                            date = o.OrderDate.ToString("yyyy/MM/dd hh:mm tt"),
                            itemsCount = o.Items.Count,
                            items = o.Items.Select(i => new { i.ProductName, i.Quantity, i.UnitPrice, i.TotalPrice })
                        })
                    });
                }

                case "create_rep_account":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var r = doc.RootElement;
                    string name = r.GetProperty("name").GetString()?.Trim() ?? "";
                    string phone = r.TryGetProperty("phone", out var pp) ? pp.GetString() ?? "" : "";
                    string company = r.TryGetProperty("company", out var cp) ? cp.GetString() ?? "" : "";
                    string address = r.TryGetProperty("address", out var ap) ? ap.GetString() ?? "" : "";
                    decimal balance = r.TryGetProperty("balance", out var bp) ? bp.GetDecimal() : 0m;
                    string notes = r.TryGetProperty("notes", out var np) ? np.GetString() ?? "" : "";

                    if (!string.IsNullOrEmpty(name))
                    {
                        var sup = new Supplier
                        {
                            Id = Guid.NewGuid(),
                            Name = name,
                            Phone = phone,
                            Company = company,
                            Address = address,
                            OpeningBalance = balance,
                            Balance = balance,
                            Notes = notes,
                            CreatedAt = DateTime.UtcNow
                        };
                        await db.Suppliers.AddAsync(sup);
                        await db.SaveChangesAsync();
                    }
                    return JsonSerializer.Serialize(new { success = true });
                }

                case "get_rep_order_details":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var orderId = doc.RootElement.GetProperty("id").GetGuid();
                    var ord = await db.SupplierOrders
                        .Include(o => o.Items)
                        .Include(o => o.Supplier)
                        .FirstOrDefaultAsync(o => o.Id == orderId);

                    if (ord == null) return JsonSerializer.Serialize(new { success = false, message = "الطلبية غير موجودة" });

                    decimal previousMarketDebt = ord.Supplier?.Balance ?? 0m;
                    decimal totalWithDebt = previousMarketDebt + ord.TotalAmount;

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        order = new
                        {
                            ord.Id,
                            ord.OrderNumber,
                            ord.MarketName,
                            ord.MarketPhone,
                            ord.MarketAddress,
                            ord.RepresentativeName,
                            ord.SupplierName,
                            ord.TotalAmount,
                            status = ord.Status.ToString(),
                            date = ord.OrderDate.ToString("yyyy/MM/dd hh:mm tt"),
                            notes = ord.Notes ?? "",
                            previousDebt = previousMarketDebt,
                            totalWithDebt = totalWithDebt,
                            items = ord.Items.Select(i => new
                            {
                                i.Id,
                                i.ProductName,
                                i.Barcode,
                                i.Quantity,
                                i.UnitPrice,
                                totalPrice = i.Quantity * i.UnitPrice
                            })
                        }
                    });
                }

                case "save_rep_order_items":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var r = doc.RootElement;
                    var orderId = r.GetProperty("id").GetGuid();
                    string statusStr = r.TryGetProperty("status", out var stp) ? stp.GetString() ?? "InPreparation" : "InPreparation";
                    string notes = r.TryGetProperty("notes", out var ntp) ? ntp.GetString() ?? "" : "";

                    var ord = await db.SupplierOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
                    if (ord != null)
                    {
                        if (Enum.TryParse<OrderStatus>(statusStr, out var status)) ord.Status = status;
                        ord.Notes = notes;
                        ord.UpdatedAt = DateTime.UtcNow;

                        if (r.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                        {
                            decimal newTotal = 0;
                            foreach (var it in itemsProp.EnumerateArray())
                            {
                                var itemId = it.GetProperty("id").GetGuid();
                                var qty = it.GetProperty("quantity").GetDecimal();
                                var price = it.GetProperty("unitPrice").GetDecimal();
                                var item = ord.Items.FirstOrDefault(i => i.Id == itemId);
                                if (item != null)
                                {
                                    item.Quantity = qty;
                                    item.UnitPrice = price;
                                    item.UpdatedAt = DateTime.UtcNow;
                                }
                                newTotal += (qty * price);
                            }
                            ord.TotalAmount = newTotal;
                        }

                        await db.SaveChangesAsync();
                    }
                    return JsonSerializer.Serialize(new { success = true });
                }

                case "accept_rep_order":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var r = doc.RootElement;
                    var orderId = r.GetProperty("id").GetGuid();
                    string statusStr = r.TryGetProperty("status", out var stp) ? stp.GetString() ?? "Delivered" : "Delivered";

                    var ord = await db.SupplierOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
                    if (ord != null)
                    {
                        if (Enum.TryParse<OrderStatus>(statusStr, out var status)) ord.Status = status;
                        ord.UpdatedAt = DateTime.UtcNow;

                        // Deduct items from warehouse stock if delivered
                        if (ord.Status == OrderStatus.Delivered)
                        {
                            foreach (var item in ord.Items)
                            {
                                Product? prod = null;
                                if (item.ProductId.HasValue)
                                {
                                    prod = await db.Products.FindAsync(item.ProductId.Value);
                                }
                                if (prod == null && !string.IsNullOrWhiteSpace(item.Barcode))
                                {
                                    prod = await db.Products.FirstOrDefaultAsync(p => p.Barcode == item.Barcode && !p.IsDeleted);
                                }
                                if (prod == null && !string.IsNullOrWhiteSpace(item.ProductName))
                                {
                                    prod = await db.Products.FirstOrDefaultAsync(p => p.Name == item.ProductName && !p.IsDeleted);
                                }

                                if (prod != null)
                                {
                                    prod.StockQuantity = Math.Max(0, prod.StockQuantity - item.Quantity);
                                    if (prod.ItemsPerCarton > 0)
                                    {
                                        prod.CartonsCount = Math.Floor(prod.StockQuantity / prod.ItemsPerCarton);
                                    }
                                    prod.UpdatedAt = DateTime.UtcNow;
                                }
                            }
                        }

                        await db.SaveChangesAsync();

                        // Trigger cloud sync to update rep mobile app catalog in real-time
                        _ = Task.Run(async () =>
                        {
                            await CloudSyncService.Instance.PushProductsToCloudAsync();
                        });
                    }
                    return JsonSerializer.Serialize(new { success = true });
                }

                case "sync_cloud_orders":
                {
                    await CloudSyncService.Instance.SyncAllAsync();
                    var orders = await db.SupplierOrders
                        .Include(o => o.Items)
                        .OrderByDescending(o => o.OrderDate)
                        .ToListAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        orders = orders.Select(o => new
                        {
                            o.Id,
                            o.OrderNumber,
                            o.RepresentativeName,
                            o.MarketName,
                            o.MarketPhone,
                            marketCity = o.MarketAddress,
                            o.TotalAmount,
                            status = o.Status.ToString(),
                            date = o.OrderDate.ToString("yyyy/MM/dd hh:mm tt"),
                            itemsCount = o.Items.Count,
                            items = o.Items.Select(i => new { i.ProductName, i.Quantity, i.UnitPrice, i.TotalPrice })
                        })
                    });
                }

                // ==========================================
                // 5. SUPPLIERS, CUSTOMERS, AUDIT, DAMAGED & REPORTS
                // ==========================================
                case "get_customers":
                {
                    var debts = await db.CustomerDebts
                        .OrderByDescending(d => d.CreatedAt)
                        .ToListAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        customers = debts.Select(d => new
                        {
                            customerName = d.CustomerName,
                            phone = d.PhoneNumber ?? "",
                            totalDebt = d.RemainingBalance,
                            totalPaid = d.TotalPaid,
                            lastDebtDate = d.CreatedAt.ToString("yyyy/MM/dd"),
                            lastType = d.LastTransactionType,
                            notes = d.Notes ?? ""
                        })
                    });
                }

                case "add_customer_debt":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var r = doc.RootElement;
                    string cName = r.GetProperty("name").GetString()?.Trim() ?? "";
                    string phone = r.TryGetProperty("phone", out var pp) ? pp.GetString()?.Trim() ?? "" : "";
                    decimal amount = r.GetProperty("amount").GetDecimal();
                    string notes = r.TryGetProperty("notes", out var np) ? np.GetString()?.Trim() ?? "" : "";

                    var existing = await db.CustomerDebts.FirstOrDefaultAsync(d => d.CustomerName == cName);
                    if (existing != null)
                    {
                        existing.TotalDebt += amount;
                        if (!string.IsNullOrEmpty(phone)) existing.PhoneNumber = phone;
                        existing.LastTransactionType = "دين جديد";
                        existing.Notes = notes;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        var newDebt = new CustomerDebt
                        {
                            Id = Guid.NewGuid(),
                            CustomerName = cName,
                            PhoneNumber = phone,
                            TotalDebt = amount,
                            TotalPaid = 0m,
                            LastTransactionType = "دين مشتريات",
                            Notes = notes,
                            CreatedAt = DateTime.UtcNow
                        };
                        await db.CustomerDebts.AddAsync(newDebt);
                    }
                    await db.SaveChangesAsync();
                    return JsonSerializer.Serialize(new { success = true });
                }

                case "pay_customer_debt":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var r = doc.RootElement;
                    string cName = r.GetProperty("customerName").GetString()?.Trim() ?? "";
                    decimal payAmount = r.GetProperty("amount").GetDecimal();

                    var debt = await db.CustomerDebts.FirstOrDefaultAsync(d => d.CustomerName == cName);
                    if (debt != null)
                    {
                        debt.TotalPaid += payAmount;
                        debt.LastTransactionType = "سداد دفعة";
                        debt.UpdatedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }
                    return JsonSerializer.Serialize(new { success = true });
                }

                case "get_damaged_items":
                {
                    var items = await db.DamagedItems
                        .OrderByDescending(d => d.CreatedAt)
                        .ToListAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        totalLoss = items.Sum(i => i.TotalLossAmount),
                        items = items.Select(i => new
                        {
                            i.Id,
                            productName = i.ProductName,
                            barcode = i.Barcode ?? "",
                            i.Quantity,
                            lossAmount = i.TotalLossAmount,
                            reason = i.Reason,
                            actionTaken = i.Notes ?? "إتلاف",
                            date = i.CreatedAt.ToString("yyyy/MM/dd hh:mm tt")
                        })
                    });
                }

                case "add_damaged_item":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var r = doc.RootElement;
                    var prodId = r.GetProperty("productId").GetGuid();
                    decimal qty = r.GetProperty("quantity").GetDecimal();
                    string reasonStr = r.TryGetProperty("reason", out var rp) ? rp.GetString() ?? "تالف" : "تالف";
                    string actionStr = r.TryGetProperty("actionTaken", out var ap) ? ap.GetString() ?? "إتلاف" : "إتلاف";

                    var prod = await db.Products.FindAsync(prodId);
                    if (prod != null)
                    {
                        prod.StockQuantity = Math.Max(0, prod.StockQuantity - qty);
                        if (prod.ItemsPerCarton > 0) prod.CartonsCount = Math.Floor(prod.StockQuantity / prod.ItemsPerCarton);

                        var damaged = new DamagedItem
                        {
                            Id = Guid.NewGuid(),
                            ProductId = prod.Id,
                            ProductName = prod.Name,
                            Barcode = prod.Barcode ?? "",
                            Quantity = qty,
                            UnitCost = prod.Cost,
                            Reason = reasonStr,
                            Notes = actionStr,
                            CreatedAt = DateTime.UtcNow
                        };
                        await db.DamagedItems.AddAsync(damaged);
                        await db.SaveChangesAsync();
                    }
                    return JsonSerializer.Serialize(new { success = true });
                }

                case "get_stock_audit":
                {
                    var prods = await db.Products
                        .Include(p => p.Category)
                        .Where(p => !p.IsDeleted)
                        .OrderBy(p => p.Name)
                        .ToListAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        products = prods.Select(p => new
                        {
                            p.Id,
                            p.Name,
                            p.Barcode,
                            category = p.Category?.Name ?? "عام",
                            p.StockQuantity,
                            p.CartonsCount,
                            p.ItemsPerCarton,
                            p.Cost,
                            p.Price
                        })
                    });
                }

                case "update_stock_audit":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var r = doc.RootElement;
                    var prodId = r.GetProperty("productId").GetGuid();
                    decimal actualStock = r.GetProperty("actualStock").GetDecimal();

                    var prod = await db.Products.FindAsync(prodId);
                    if (prod != null)
                    {
                        prod.StockQuantity = actualStock;
                        if (prod.ItemsPerCarton > 0)
                        {
                            prod.CartonsCount = Math.Floor(actualStock / prod.ItemsPerCarton);
                        }
                        prod.UpdatedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }
                    return JsonSerializer.Serialize(new { success = true });
                }

                case "get_reports":
                {
                    var today = DateTime.Today;
                    var sales = await db.Sales
                        .Include(s => s.Items)
                        .ThenInclude(i => i.Product)
                        .AsNoTracking()
                        .ToListAsync();

                    var todaySales = sales.Where(s => s.CreatedAt.Date == today).ToList();
                    var monthSales = sales.Where(s => s.CreatedAt.Year == today.Year && s.CreatedAt.Month == today.Month).ToList();

                    decimal todayTotal = todaySales.Sum(s => s.TotalAmount);
                    decimal todayProfit = todaySales.Sum(s => s.InvoiceNetProfit);

                    decimal monthTotal = monthSales.Sum(s => s.TotalAmount);
                    decimal monthProfit = monthSales.Sum(s => s.InvoiceNetProfit);

                    var topItems = sales.SelectMany(s => s.Items)
                        .GroupBy(i => i.ProductName)
                        .Select(g => new
                        {
                            name = g.Key,
                            qty = g.Sum(x => x.Quantity),
                            total = g.Sum(x => x.TotalPrice)
                        })
                        .OrderByDescending(x => x.qty)
                        .Take(6)
                        .ToList();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        todayTotal,
                        todayProfit,
                        todayInvoicesCount = todaySales.Count,
                        monthTotal,
                        monthProfit,
                        monthInvoicesCount = monthSales.Count,
                        topItems
                    });
                }

                // ==========================================
                // 5. SUPPLIERS & DEBT
                // ==========================================
                case "get_suppliers":
                {
                    var sups = await db.Suppliers
                        .Include(s => s.Products.Where(p => !p.IsDeleted))
                        .Where(s => !s.IsDeleted)
                        .OrderBy(s => s.Name)
                        .ToListAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        suppliers = sups.Select(s => new
                        {
                            s.Id,
                            s.Name,
                            s.Company,
                            s.Phone,
                            s.Address,
                            s.Balance,
                            productsCount = s.Products.Count
                        })
                    });
                }

                case "add_supplier_payment":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var supId = doc.RootElement.GetProperty("supplierId").GetGuid();
                    decimal amount = doc.RootElement.GetProperty("amount").GetDecimal();
                    string notes = doc.RootElement.TryGetProperty("notes", out var np) ? np.GetString() ?? "" : "";
                    string recNo = doc.RootElement.TryGetProperty("receiptNumber", out var rp) ? rp.GetString() ?? "" : $"PAY-{DateTime.Now.Ticks}";

                    var sService = new SupplierService(db);
                    await sService.AddTransactionAsync(supId, "Payment", amount, notes, recNo);
                    return JsonSerializer.Serialize(new { success = true });
                }

                // ==========================================
                // 6. CASHIER USERS & SHIFTS
                // ==========================================
                case "get_users":
                {
                    var users = await db.Users.OrderBy(u => u.FullName).ToListAsync();
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        users = users.Select(u => new
                        {
                            u.Id,
                            u.FullName,
                            u.Username,
                            u.Role,
                            u.IsActive
                        })
                    });
                }

                // ==========================================
                // 7. APP INFO, UPDATES, EXCEL IMPORT & BACKUP
                // ==========================================
                case "get_app_info":
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        appName = "7amo.pos",
                        version = "2.5.0 Pro",
                        storeId = StoreSettingsService.Instance.Settings.StoreId,
                        portalUrl = "https://hama2002m2002-lab.github.io/mo74mmed.pos/",
                        localPortalUrl = "http://localhost:5000"
                    });
                }

                case "check_for_updates":
                {
                    UpdateService.Instance.CheckForUpdates(true);
                    return JsonSerializer.Serialize(new { success = true });
                }

                case "import_excel_products":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var items = doc.RootElement.GetProperty("products");
                    int count = 0;
                    foreach (var it in items.EnumerateArray())
                    {
                        string name = it.TryGetProperty("name", out var np) ? np.GetString()?.Trim() ?? "" : "";
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        string barcode = it.TryGetProperty("barcode", out var bp) ? bp.GetString()?.Trim() ?? "" : "";
                        string catName = it.TryGetProperty("category", out var cp) ? cp.GetString()?.Trim() ?? "عام" : "عام";
                        string supplier = it.TryGetProperty("supplierName", out var sp) ? sp.GetString()?.Trim() ?? "" : "";
                        decimal cost = it.TryGetProperty("cost", out var cst) ? cst.GetDecimal() : 0m;
                        decimal price = it.TryGetProperty("price", out var prc) ? prc.GetDecimal() : 0m;
                        decimal wholesale = it.TryGetProperty("wholesalePrice", out var wp) ? wp.GetDecimal() : 0m;
                        decimal cartonPurchase = it.TryGetProperty("cartonPurchasePrice", out var cpp) ? cpp.GetDecimal() : 0m;
                        decimal cartonSelling = it.TryGetProperty("cartonSellingPrice", out var csp) ? csp.GetDecimal() : 0m;
                        decimal itemsPerCarton = it.TryGetProperty("piecesPerCarton", out var ppc) ? Math.Max(1, ppc.GetDecimal()) : 1m;
                        decimal cartonsCount = it.TryGetProperty("cartonsCount", out var ccp) ? Math.Max(0, ccp.GetDecimal()) : 0m;
                        decimal stock = it.TryGetProperty("stockQuantity", out var sq) ? sq.GetDecimal() : (itemsPerCarton * cartonsCount);
                        decimal minAlert = it.TryGetProperty("minStockAlert", out var ma) ? ma.GetDecimal() : 5m;

                        // Find or create category
                        var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == catName);
                        if (category == null)
                        {
                            category = new Category { Id = Guid.NewGuid(), Name = catName, CreatedAt = DateTime.UtcNow };
                            await db.Categories.AddAsync(category);
                            await db.SaveChangesAsync();
                        }

                        // Check if product exists by barcode or name
                        Product? existing = null;
                        if (!string.IsNullOrWhiteSpace(barcode))
                        {
                            existing = await db.Products.FirstOrDefaultAsync(p => p.Barcode == barcode && !p.IsDeleted);
                        }
                        if (existing == null)
                        {
                            existing = await db.Products.FirstOrDefaultAsync(p => p.Name == name && !p.IsDeleted);
                        }

                        if (existing != null)
                        {
                            existing.Name = name;
                            if (!string.IsNullOrWhiteSpace(barcode)) existing.Barcode = barcode;
                            existing.CategoryId = category.Id;
                            existing.SupplierName = supplier;
                            existing.Cost = cost;
                            existing.Price = price;
                            existing.WholesalePrice = wholesale;
                            existing.CartonPurchasePrice = cartonPurchase;
                            existing.CartonSellingPrice = cartonSelling;
                            existing.ItemsPerCarton = itemsPerCarton;
                            existing.CartonsCount = cartonsCount;
                            existing.StockQuantity = stock;
                            existing.MinStockAlert = minAlert;
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                        else
                        {
                            var newProd = new Product
                            {
                                Id = Guid.NewGuid(),
                                Name = name,
                                Barcode = string.IsNullOrWhiteSpace(barcode) ? DateTime.Now.Ticks.ToString() : barcode,
                                CategoryId = category.Id,
                                SupplierName = supplier,
                                Cost = cost,
                                Price = price,
                                WholesalePrice = wholesale,
                                CartonPurchasePrice = cartonPurchase,
                                CartonSellingPrice = cartonSelling,
                                ItemsPerCarton = itemsPerCarton,
                                CartonsCount = cartonsCount,
                                StockQuantity = stock,
                                MinStockAlert = minAlert,
                                CreatedAt = DateTime.UtcNow
                            };
                            await db.Products.AddAsync(newProd);
                        }
                        count++;
                    }
                    await db.SaveChangesAsync();
                    return JsonSerializer.Serialize(new { success = true, importedCount = count });
                }

                case "backup_database":
                {
                    string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pos_data.db");
                    if (!File.Exists(dbPath))
                    {
                        dbPath = Path.Combine(Directory.GetCurrentDirectory(), "pos_data.db");
                    }

                    string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "نسخ_احتياطية_7amoPOS");
                    Directory.CreateDirectory(backupDir);

                    string backupFile = Path.Combine(backupDir, $"نسخة_احتياطية_{DateTime.Now:yyyy_MM_dd_HHmmss}.db");
                    if (File.Exists(dbPath))
                    {
                        File.Copy(dbPath, backupFile, true);
                    }

                    return JsonSerializer.Serialize(new { success = true, backupPath = backupFile });
                }

                default:
                    return JsonSerializer.Serialize(new { success = false, message = "Unknown action" });
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}
