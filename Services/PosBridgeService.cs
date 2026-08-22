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
                // 7. APP INFO & SYSTEM STATUS
                // ==========================================
                case "get_app_info":
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        appName = "7amo.pos",
                        version = "1.4.0",
                        portalUrl = "https://hama2002m2002-lab.github.io/mo74mmed.pos/",
                        localPortalUrl = "http://localhost:5000"
                    });
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
