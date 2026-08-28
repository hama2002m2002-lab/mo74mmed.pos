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

                    var prodsList = await db.Products.AsNoTracking().Where(p => !p.IsDeleted).ToListAsync();
                    int totalProducts = prodsList.Count;
                    decimal totalStockPieces = prodsList.Sum(p => p.StockQuantity);
                    decimal totalStockCostValue = prodsList.Sum(p => p.Cost * p.StockQuantity);
                    decimal totalStockRetailValue = prodsList.Sum(p => p.Price * p.StockQuantity);

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        todayRevenue = todayStats.TotalRevenue,
                        todayInvoices = todayStats.TotalSalesCount,
                        monthlyRevenue = monthStats.MonthlyRevenue,
                        totalProducts = totalProducts,
                        totalStockPieces = totalStockPieces,
                        totalStockCostValue = totalStockCostValue,
                        totalStockRetailValue = totalStockRetailValue,
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
                        var saleType = it.TryGetProperty("saleType", out var st) ? st.GetString() ?? "retail" : "retail";
                        var ppc = it.TryGetProperty("piecesPerCarton", out var ppcProp) ? ppcProp.GetDecimal() : 1m;
                        var barcode = it.TryGetProperty("barcode", out var bc) ? bc.GetString() ?? "" : "";

                        decimal itemTotal = qty * price;
                        totalAmount += itemTotal;
                        totalCost += (qty * cost);

                        string displayName = name;
                        if (saleType == "carton" && !displayName.Contains("(كرتون)"))
                            displayName = $"{displayName} (كرتون)";
                        else if (saleType == "wholesale" && !displayName.Contains("(جملة)"))
                            displayName = $"{displayName} (جملة)";

                        saleItems.Add(new SaleItem
                        {
                            Id = Guid.NewGuid(),
                            ProductId = prodId,
                            ProductName = displayName,
                            Barcode = barcode,
                            Quantity = qty,
                            UnitPrice = price,
                            TotalPrice = itemTotal
                        });

                        // Deduct Stock: When selling by carton, deduct (qty * piecesPerCarton)
                        var dbProd = await db.Products.FindAsync(prodId);
                        if (dbProd != null)
                        {
                            decimal deduction = qty;
                            if (saleType == "carton")
                            {
                                decimal itemsInCarton = ppc > 0 ? ppc : (dbProd.ItemsPerCarton > 0 ? dbProd.ItemsPerCarton : 1m);
                                deduction = qty * itemsInCarton;
                            }
                            dbProd.StockQuantity = Math.Max(0, dbProd.StockQuantity - deduction);
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

                case "get_invoices":
                {
                    var sales = await db.Sales
                        .Include(s => s.Items)
                        .AsNoTracking()
                        .OrderByDescending(s => s.CreatedAt)
                        .Take(300)
                        .ToListAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        invoices = sales.Select(s => new
                        {
                            id = s.Id,
                            invoiceNumber = s.InvoiceNumber,
                            customerName = s.CustomerName,
                            paymentMethod = s.PaymentMethod,
                            subTotal = s.SubTotal,
                            discount = s.DiscountAmount,
                            totalAmount = s.TotalAmount,
                            status = s.Status,
                            createdAt = s.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"),
                            itemsCount = s.Items.Count,
                            items = s.Items.Select(i => new
                            {
                                id = i.Id,
                                productId = i.ProductId,
                                name = i.ProductName,
                                quantity = i.Quantity,
                                price = i.UnitPrice,
                                total = i.TotalPrice
                            })
                        })
                    });
                }

                case "return_invoice":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var root = doc.RootElement;
                    string invNum = root.TryGetProperty("invoiceNumber", out var inProp) ? inProp.GetString() ?? "" : "";

                    var sale = await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.InvoiceNumber == invNum);
                    if (sale == null)
                    {
                        return JsonSerializer.Serialize(new { success = false, message = "الفاتورة غير موجودة" });
                    }

                    if (sale.Status == "Returned")
                    {
                        return JsonSerializer.Serialize(new { success = false, message = "هذه الفاتورة تم إرجاعها مسبقاً" });
                    }

                    sale.Status = "Returned";
                    sale.UpdatedAt = DateTime.UtcNow;

                    // Restore Stock
                    foreach (var item in sale.Items)
                    {
                        var prod = await db.Products.FindAsync(item.ProductId);
                        if (prod != null)
                        {
                            decimal itemsToRestore = item.Quantity;
                            if (item.ProductName.Contains("(كرتون)"))
                            {
                                itemsToRestore = item.Quantity * (prod.ItemsPerCarton > 0 ? prod.ItemsPerCarton : 1m);
                            }
                            prod.StockQuantity += itemsToRestore;
                            prod.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    await db.SaveChangesAsync();
                    return JsonSerializer.Serialize(new { success = true, message = "تم إرجاع الفاتورة وإعادة الكميات للمخزن بنجاح" });
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
                            unit = p.Unit ?? "قطعة",
                            expiryDate = p.ExpiryDate.HasValue ? p.ExpiryDate.Value.ToString("yyyy/MM/dd") : null,
                            daysToExpiry = p.ExpiryDate.HasValue ? (int)(p.ExpiryDate.Value.Date - DateTime.Today).TotalDays : (int?)null,
                            createdAt = p.CreatedAt.ToString("yyyy/MM/dd")
                        })
                    });
                }

                case "get_expiring_products":
                {
                    var today = DateTime.Today;
                    var prods = await db.Products
                        .Include(p => p.Category)
                        .AsNoTracking()
                        .Where(p => !p.IsDeleted && p.ExpiryDate.HasValue)
                        .OrderBy(p => p.ExpiryDate)
                        .ToListAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        products = prods.Select(p => new
                        {
                            id = p.Id,
                            name = p.Name,
                            barcode = p.Barcode ?? "",
                            category = p.Category?.Name ?? "عام",
                            supplierId = p.SupplierId,
                            supplierName = p.SupplierName ?? "",
                            stockQuantity = p.StockQuantity,
                            cost = p.Cost,
                            price = p.Price,
                            totalLossValue = p.Cost * p.StockQuantity,
                            expiryDate = p.ExpiryDate!.Value.ToString("yyyy/MM/dd"),
                            daysRemaining = (int)(p.ExpiryDate.Value.Date - today).TotalDays,
                            isExpired = p.ExpiryDate.Value.Date < today,
                            isCritical = (p.ExpiryDate.Value.Date - today).TotalDays >= 0 && (p.ExpiryDate.Value.Date - today).TotalDays <= 30
                        })
                    });
                }

                case "save_product":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var r = doc.RootElement;
                    Guid? id = r.TryGetProperty("id", out var idProp) && !string.IsNullOrWhiteSpace(idProp.GetString()) 
                        ? Guid.Parse(idProp.GetString()!) 
                        : null;

                    string name = r.GetProperty("name").GetString() ?? "";
                    string barcode = r.TryGetProperty("barcode", out var bProp) ? bProp.GetString() ?? "" : "";
                    string categoryName = r.TryGetProperty("category", out var cProp) ? cProp.GetString() ?? "عام" : "عام";
                    string supplierName = r.TryGetProperty("supplierName", out var supProp) ? supProp.GetString() ?? "" : "";
                    string unit = r.TryGetProperty("unit", out var unitProp) ? unitProp.GetString() ?? "قطعة" : "قطعة";
                    decimal cost = r.TryGetProperty("cost", out var cst) ? cst.GetDecimal() : 0m;
                    decimal price = r.TryGetProperty("price", out var prc) ? prc.GetDecimal() : 0m;
                    decimal wholesale = r.TryGetProperty("wholesalePrice", out var wp) ? wp.GetDecimal() : 0m;
                    decimal cartonPurchase = r.TryGetProperty("cartonPurchasePrice", out var cpp) ? cpp.GetDecimal() : 0m;
                    decimal cartonSelling = r.TryGetProperty("cartonSellingPrice", out var csp) ? csp.GetDecimal() : 0m;
                    decimal stock = r.TryGetProperty("stockQuantity", out var sq) ? sq.GetDecimal() : 0m;
                    decimal cartonsCount = r.TryGetProperty("cartonsCount", out var ccp) ? ccp.GetDecimal() : 0m;
                    decimal minAlert = r.TryGetProperty("minStockAlert", out var ma) ? ma.GetDecimal() : 5m;
                    decimal itemsPerCarton = r.TryGetProperty("piecesPerCarton", out var ppc) ? ppc.GetDecimal() : 1m;

                    DateTime? expiryDate = null;
                    if (r.TryGetProperty("expiryDate", out var expProp) && !string.IsNullOrWhiteSpace(expProp.GetString()))
                    {
                        if (DateTime.TryParse(expProp.GetString(), out var expDt)) expiryDate = expDt;
                    }

                    // Ensure category
                    var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == categoryName);
                    if (category == null)
                    {
                        category = new Category { Id = Guid.NewGuid(), Name = categoryName, CreatedAt = DateTime.UtcNow };
                        await db.Categories.AddAsync(category);
                        await db.SaveChangesAsync();
                    }

                    // Look up or link supplier
                    Guid? supId = null;
                    if (!string.IsNullOrWhiteSpace(supplierName))
                    {
                        var sup = await db.Suppliers.FirstOrDefaultAsync(s => s.Name == supplierName && !s.IsDeleted);
                        if (sup == null)
                        {
                            sup = new Supplier
                            {
                                Id = Guid.NewGuid(),
                                Name = supplierName,
                                CreatedAt = DateTime.UtcNow
                            };
                            await db.Suppliers.AddAsync(sup);
                            await db.SaveChangesAsync();
                        }
                        supId = sup.Id;
                    }

                    if (id.HasValue)
                    {
                        var existing = await db.Products.FindAsync(id.Value);
                        if (existing != null)
                        {
                            existing.Name = name;
                            existing.Barcode = barcode;
                            existing.CategoryId = category.Id;
                            existing.SupplierId = supId;
                            existing.SupplierName = supplierName;
                            existing.Unit = unit;
                            existing.Cost = cost;
                            existing.Price = price;
                            existing.WholesalePrice = wholesale;
                            existing.CartonPurchasePrice = cartonPurchase;
                            existing.CartonSellingPrice = cartonSelling;
                            existing.StockQuantity = stock;
                            existing.CartonsCount = cartonsCount;
                            existing.MinStockAlert = minAlert;
                            existing.ItemsPerCarton = itemsPerCarton;
                            existing.ExpiryDate = expiryDate;
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
                            SupplierId = supId,
                            SupplierName = supplierName,
                            Unit = unit,
                            Cost = cost,
                            Price = price,
                            WholesalePrice = wholesale,
                            CartonPurchasePrice = cartonPurchase,
                            CartonSellingPrice = cartonSelling,
                            StockQuantity = stock,
                            CartonsCount = cartonsCount,
                            MinStockAlert = minAlert,
                            ItemsPerCarton = itemsPerCarton,
                            ExpiryDate = expiryDate,
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

                case "batch_update_stock_audit":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var r = doc.RootElement;
                    if (r.TryGetProperty("updates", out var updates) && updates.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in updates.EnumerateArray())
                        {
                            var prodId = item.GetProperty("productId").GetGuid();
                            decimal actualStock = item.GetProperty("actualStock").GetDecimal();
                            var prod = await db.Products.FindAsync(prodId);
                            if (prod != null)
                            {
                                prod.StockQuantity = actualStock;
                                if (prod.ItemsPerCarton > 0)
                                {
                                    prod.CartonsCount = Math.Floor(actualStock / prod.ItemsPerCarton);
                                }
                                prod.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                        await db.SaveChangesAsync();
                    }
                    return JsonSerializer.Serialize(new { success = true });
                }

                case "get_reports":
                case "get_comprehensive_reports":
                {
                    var now = DateTime.Now;
                    var today = DateTime.Today;
                    var startOfMonth = new DateTime(today.Year, today.Month, 1);
                    var startOfPrevMonth = startOfMonth.AddMonths(-1);
                    var endOfPrevMonth = startOfMonth.AddDays(-1);

                    // 1. Fetch Sales with Items
                    var sales = await db.Sales
                        .Include(s => s.Items)
                        .ThenInclude(i => i.Product)
                        .Include(s => s.User)
                        .AsNoTracking()
                        .OrderByDescending(s => s.CreatedAt)
                        .ToListAsync();

                    // 2. Fetch Products
                    var products = await db.Products
                        .Include(p => p.Category)
                        .AsNoTracking()
                        .Where(p => !p.IsDeleted)
                        .ToListAsync();

                    // 3. Fetch Purchases
                    var purchases = await db.PurchaseInvoices
                        .Include(p => p.Items)
                        .AsNoTracking()
                        .OrderByDescending(p => p.CreatedAt)
                        .ToListAsync();

                    // 4. Fetch Suppliers
                    var suppliers = await db.Suppliers
                        .AsNoTracking()
                        .Where(s => !s.IsDeleted)
                        .ToListAsync();

                    // 5. Fetch Customer Debts
                    var customerDebts = await db.CustomerDebts
                        .AsNoTracking()
                        .OrderByDescending(c => c.CreatedAt)
                        .ToListAsync();

                    // 6. Fetch Damaged Items
                    var damagedItems = await db.DamagedItems
                        .AsNoTracking()
                        .OrderByDescending(d => d.CreatedAt)
                        .ToListAsync();

                    // Filtered sets
                    var todaySales = sales.Where(s => s.CreatedAt.Date == today && !s.IsReturnSale).ToList();
                    var monthSales = sales.Where(s => s.CreatedAt >= startOfMonth && !s.IsReturnSale).ToList();
                    var prevMonthSales = sales.Where(s => s.CreatedAt >= startOfPrevMonth && s.CreatedAt <= endOfPrevMonth && !s.IsReturnSale).ToList();
                    var returnSales = sales.Where(s => s.IsReturnSale).ToList();

                    // Financial metrics
                    decimal todayTotal = todaySales.Sum(s => s.TotalAmount);
                    decimal todayProfit = todaySales.Sum(s => s.InvoiceNetProfit);
                    decimal monthTotal = monthSales.Sum(s => s.TotalAmount);
                    decimal monthProfit = monthSales.Sum(s => s.InvoiceNetProfit);
                    decimal prevMonthTotal = prevMonthSales.Sum(s => s.TotalAmount);

                    decimal totalRevenueAll = sales.Where(s => !s.IsReturnSale).Sum(s => s.TotalAmount);
                    decimal totalProfitAll = sales.Where(s => !s.IsReturnSale).Sum(s => s.InvoiceNetProfit);
                    decimal totalDiscountAll = sales.Sum(s => s.DiscountAmount);
                    decimal totalReturnsAmount = returnSales.Sum(s => s.TotalAmount);

                    // Profit breakdown by sale type (Retail, Wholesale, Carton)
                    decimal retailSalesTotal = 0m;
                    decimal retailProfitTotal = 0m;
                    decimal wholesaleSalesTotal = 0m;
                    decimal wholesaleProfitTotal = 0m;
                    decimal cartonSalesTotal = 0m;
                    decimal cartonProfitTotal = 0m;

                    foreach (var s in sales.Where(x => !x.IsReturnSale))
                    {
                        foreach (var itm in s.Items)
                        {
                            var p = itm.Product;
                            if (itm.ProductName.Contains("(كرتون)"))
                            {
                                decimal cartonCost = (p != null && p.CartonPurchasePrice > 0)
                                    ? p.CartonPurchasePrice
                                    : ((p?.Cost ?? 0m) * (p?.ItemsPerCarton > 0 ? p.ItemsPerCarton : 1m));
                                decimal itmProfit = itm.TotalPrice - (cartonCost * itm.Quantity);
                                cartonSalesTotal += itm.TotalPrice;
                                cartonProfitTotal += itmProfit;
                            }
                            else if (itm.ProductName.Contains("(جملة)"))
                            {
                                decimal pieceCost = p?.Cost ?? 0m;
                                decimal itmProfit = itm.TotalPrice - (pieceCost * itm.Quantity);
                                wholesaleSalesTotal += itm.TotalPrice;
                                wholesaleProfitTotal += itmProfit;
                            }
                            else
                            {
                                decimal pieceCost = p?.Cost ?? 0m;
                                decimal itmProfit = itm.TotalPrice - (pieceCost * itm.Quantity);
                                retailSalesTotal += itm.TotalPrice;
                                retailProfitTotal += itmProfit;
                            }
                        }
                    }

                    // Stock projected profits by mode
                    decimal projectedRetailProfit = products.Sum(p => p.StockQuantity * Math.Max(0, p.Price - p.Cost));
                    decimal projectedWholesaleProfit = products.Sum(p => p.StockQuantity * Math.Max(0, (p.WholesalePrice > 0 ? p.WholesalePrice : p.Price) - p.Cost));
                    decimal projectedCartonProfit = products.Sum(p => {
                        decimal cartons = p.ItemsPerCarton > 0 ? (p.StockQuantity / p.ItemsPerCarton) : 0;
                        decimal cartonCost = p.CartonPurchasePrice > 0 ? p.CartonPurchasePrice : (p.Cost * p.ItemsPerCarton);
                        decimal cartonSell = p.CartonSellingPrice > 0 ? p.CartonSellingPrice : (p.Price * p.ItemsPerCarton);
                        return cartons * Math.Max(0, cartonSell - cartonCost);
                    });

                    // Inventory valuation
                    decimal totalStockCostValue = products.Sum(p => p.StockQuantity * p.Cost);
                    decimal totalStockRetailValue = products.Sum(p => p.StockQuantity * p.Price);
                    decimal projectedGrossProfit = totalStockRetailValue - totalStockCostValue;
                    int lowStockCount = products.Count(p => p.StockQuantity <= (p.MinStockAlert > 0 ? p.MinStockAlert : 5));

                    // Top Selling Items (Fast Moving)
                    var topItems = sales.Where(s => !s.IsReturnSale).SelectMany(s => s.Items)
                        .GroupBy(i => new { i.ProductName, i.Barcode })
                        .Select(g => new
                        {
                            name = g.Key.ProductName,
                            barcode = g.Key.Barcode,
                            qty = g.Sum(x => x.Quantity),
                            total = g.Sum(x => x.TotalPrice)
                        })
                        .OrderByDescending(x => x.qty)
                        .Take(30)
                        .ToList();

                    // Slow Moving / Stagnant Items (High stock, low or 0 sales)
                    var soldProductNames = sales.Where(s => s.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                        .SelectMany(s => s.Items).Select(i => i.ProductName).ToHashSet();

                    var stagnantItems = products.Where(p => p.StockQuantity > 0 && !soldProductNames.Contains(p.Name))
                        .OrderByDescending(p => p.StockQuantity * p.Cost)
                        .Take(30)
                        .Select(p => new
                        {
                            p.Name,
                            p.Barcode,
                            category = p.Category?.Name ?? "عام",
                            p.StockQuantity,
                            costValue = p.StockQuantity * p.Cost,
                            p.Cost,
                            p.Price
                        })
                        .ToList();

                    // Damaged summary
                    decimal totalDamagedLoss = damagedItems.Sum(d => d.TotalLossAmount);

                    // Purchases & Suppliers summary
                    decimal totalPurchasesAll = purchases.Sum(p => p.TotalAmount);
                    decimal totalPurchasesCash = purchases.Where(p => p.PaymentMethod == "Cash").Sum(p => p.TotalAmount);
                    decimal totalPurchasesDebt = purchases.Where(p => p.PaymentMethod == "Debt" || p.PaymentMethod == "Partial").Sum(p => p.RemainingAmount);
                    decimal totalSupplierBalancesOwed = suppliers.Sum(s => s.Balance);

                    // Customers Debts summary
                    decimal totalCustomerDebtsOwed = customerDebts.Sum(c => c.RemainingBalance);
                    decimal totalCustomerPaid = customerDebts.Sum(c => c.TotalPaid);

                    // Payment Method Stats
                    var paymentStats = sales.Where(s => !s.IsReturnSale)
                        .GroupBy(s => string.IsNullOrWhiteSpace(s.PaymentMethod) ? "Cash" : s.PaymentMethod)
                        .Select(g => new
                        {
                            method = g.Key,
                            total = g.Sum(s => s.TotalAmount),
                            count = g.Count()
                        })
                        .ToList();

                    // Cashier Stats
                    var cashierStats = sales.Where(s => !s.IsReturnSale)
                        .GroupBy(s => s.User != null ? s.User.FullName : "الكاشير الرئيسي")
                        .Select(g => new
                        {
                            cashierName = g.Key,
                            totalSales = g.Sum(s => s.TotalAmount),
                            netProfit = g.Sum(s => s.InvoiceNetProfit),
                            invoicesCount = g.Count(),
                            avgInvoice = g.Count() > 0 ? Math.Round(g.Sum(s => s.TotalAmount) / g.Count(), 0) : 0
                        })
                        .ToList();

                    // Hourly Traffic (24 Hours)
                    var hourlyStats = new List<object>();
                    for (int h = 0; h < 24; h++)
                    {
                        var hSales = sales.Where(s => s.CreatedAt.Hour == h && !s.IsReturnSale).ToList();
                        hourlyStats.Add(new
                        {
                            hour = h,
                            label = $"{h:00}:00",
                            total = hSales.Sum(s => s.TotalAmount),
                            count = hSales.Count
                        });
                    }

                    // Security / Void / Suspicious operations (Cancelled, returned, zero price, heavy discounts)
                    var suspiciousSales = sales.Where(s => s.IsReturnSale || s.DiscountAmount > 5000 || s.TotalAmount <= 0)
                        .Take(30)
                        .Select(s => new
                        {
                            s.Id,
                            invoiceNumber = s.InvoiceNumber,
                            date = s.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"),
                            cashier = s.User?.FullName ?? "الكاشير",
                            s.TotalAmount,
                            s.DiscountAmount,
                            type = s.IsReturnSale ? "مرتجع / إلغاء" : (s.TotalAmount <= 0 ? "فاتورة صفرية" : "خصم استثنائي"),
                            itemsCount = s.Items.Count
                        })
                        .ToList();

                    // Basket size statistics
                    int validSalesCount = sales.Count(s => !s.IsReturnSale);
                    decimal avgBasketValue = validSalesCount > 0 ? Math.Round(totalRevenueAll / validSalesCount, 0) : 0;
                    decimal avgItemsPerBasket = validSalesCount > 0 ? Math.Round((decimal)sales.Where(s => !s.IsReturnSale).Sum(s => s.Items.Sum(i => i.Quantity)) / validSalesCount, 1) : 0;

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        // Quick KPI Cards
                        todayTotal,
                        todayProfit,
                        todayInvoicesCount = todaySales.Count,
                        monthTotal,
                        monthProfit,
                        monthInvoicesCount = monthSales.Count,
                        prevMonthTotal,
                        monthGrowthRate = prevMonthTotal > 0 ? Math.Round(((monthTotal - prevMonthTotal) / prevMonthTotal) * 100, 1) : 0,

                        // Detailed sections
                        // 1. Financial
                        totalRevenueAll,
                        totalProfitAll,
                        totalDiscountAll,
                        totalReturnsAmount,
                        retailSalesTotal,
                        retailProfitTotal,
                        wholesaleSalesTotal,
                        wholesaleProfitTotal,
                        cartonSalesTotal,
                        cartonProfitTotal,
                        projectedRetailProfit,
                        projectedWholesaleProfit,
                        projectedCartonProfit,
                        returnSalesCount = returnSales.Count,
                        salesList = sales.Take(50).Select(s => new
                        {
                            s.Id,
                            invoiceNumber = s.InvoiceNumber,
                            date = s.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"),
                            cashier = s.User?.FullName ?? "الكاشير",
                            customer = s.CustomerName ?? "زبون عام",
                            paymentMethod = s.PaymentMethod,
                            s.TotalAmount,
                            profit = s.InvoiceNetProfit,
                            s.DiscountAmount,
                            itemsCount = s.Items.Count,
                            status = s.Status
                        }),
                        returnsList = returnSales.Take(30).Select(s => new
                        {
                            s.Id,
                            invoiceNumber = s.InvoiceNumber,
                            date = s.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"),
                            cashier = s.User?.FullName ?? "الكاشير",
                            s.TotalAmount,
                            notes = s.Notes ?? "استرجاع بضاعة"
                        }),
                        paymentStats,
                        cashierStats,

                        // 2. Inventory & Damaged
                        totalProductsCount = products.Count,
                        totalStockCostValue,
                        totalStockRetailValue,
                        projectedGrossProfit,
                        lowStockCount,
                        topItems,
                        stagnantItems,
                        damagedItems = damagedItems.Take(50).Select(d => new
                        {
                            d.Id,
                            productName = d.ProductName,
                            barcode = d.Barcode ?? "",
                            d.Quantity,
                            lossAmount = d.TotalLossAmount,
                            reason = d.Reason,
                            actionTaken = d.Notes ?? "إتلاف",
                            date = d.CreatedAt.ToString("yyyy/MM/dd hh:mm tt")
                        }),
                        totalDamagedLoss,

                        // 3. Purchases & Suppliers
                        totalPurchasesAll,
                        totalPurchasesCash,
                        totalPurchasesDebt,
                        totalSupplierBalancesOwed,
                        purchasesList = purchases.Take(50).Select(p => new
                        {
                            p.Id,
                            invoiceNumber = p.InvoiceNumber,
                            supplierName = p.SupplierName,
                            p.TotalAmount,
                            p.PaidAmount,
                            remaining = p.RemainingAmount,
                            paymentMethod = p.PaymentMethod,
                            date = p.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"),
                            itemsCount = p.Items.Count
                        }),
                        suppliersSummary = suppliers.Select(s => new
                        {
                            s.Id,
                            s.Name,
                            company = s.Company ?? "شركة عامة",
                            phone = s.Phone ?? "",
                            balance = s.Balance
                        }),

                        // 4. Customers & Debts
                        totalCustomerDebtsOwed,
                        totalCustomerPaid,
                        debtorsCount = customerDebts.Count(c => c.RemainingBalance > 0),
                        customersDebts = customerDebts.Select(c => new
                        {
                            c.Id,
                            customerName = c.CustomerName,
                            phone = c.PhoneNumber ?? "",
                            totalDebt = c.TotalDebt,
                            totalPaid = c.TotalPaid,
                            remaining = c.RemainingBalance,
                            lastTransaction = c.LastTransactionType,
                            date = c.CreatedAt.ToString("yyyy/MM/dd")
                        }),

                        // 5. Security & Z-Report
                        suspiciousSales,
                        zReport = new
                        {
                            reportDate = today.ToString("yyyy/MM/dd"),
                            openingTime = "08:00 ص",
                            closingTime = now.ToString("hh:mm tt"),
                            totalCashSales = todaySales.Where(s => s.PaymentMethod == "Cash" || string.IsNullOrEmpty(s.PaymentMethod)).Sum(s => s.TotalAmount),
                            totalCreditSales = todaySales.Where(s => s.PaymentMethod == "Debt").Sum(s => s.TotalAmount),
                            totalDiscounts = todaySales.Sum(s => s.DiscountAmount),
                            netCashInDrawer = todaySales.Where(s => s.PaymentMethod == "Cash" || string.IsNullOrEmpty(s.PaymentMethod)).Sum(s => s.TotalAmount),
                            invoicesCount = todaySales.Count
                        },

                        // 6. Analytics & Operational
                        hourlyStats,
                        avgBasketValue,
                        avgItemsPerBasket
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
                            id = s.Id,
                            name = s.Name,
                            company = string.IsNullOrWhiteSpace(s.Company) ? "شركة عامة" : s.Company,
                            phone = s.Phone ?? "",
                            address = s.Address ?? "",
                            balance = s.Balance,
                            openingBalance = s.OpeningBalance,
                            notes = s.Notes ?? "",
                            productsCount = s.Products.Count
                        })
                    });
                }

                case "get_supplier_products":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var supId = doc.RootElement.GetProperty("supplierId").GetGuid();
                    var sup = await db.Suppliers.FindAsync(supId);
                    var supName = sup?.Name;

                    var prods = await db.Products
                        .Where(p => !p.IsDeleted && (p.SupplierId == supId || (supName != null && p.SupplierName == supName)))
                        .OrderBy(p => p.Name)
                        .ToListAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        products = prods.Select(p => new
                        {
                            id = p.Id,
                            name = p.Name,
                            barcode = p.Barcode,
                            price = p.Price,
                            cost = p.Cost,
                            stockQuantity = p.StockQuantity,
                            unit = p.Unit,
                            cartonsCount = p.CartonsCount,
                            itemsPerCarton = p.ItemsPerCarton,
                            cartonPurchasePrice = p.CartonPurchasePrice,
                            cartonSellingPrice = p.CartonSellingPrice
                        })
                    });
                }

                case "create_purchase_invoice":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var root = doc.RootElement;
                    var supId = root.GetProperty("supplierId").GetGuid();
                    var isPaid = root.TryGetProperty("isPaid", out var ipProp) && ipProp.GetBoolean();
                    var notes = root.TryGetProperty("notes", out var np) ? np.GetString() : "";
                    var invoiceNumber = root.TryGetProperty("invoiceNumber", out var inp) ? inp.GetString() : $"PUR-{DateTime.Now:yyyyMMddHHmmss}";

                    var sup = await db.Suppliers.FindAsync(supId);
                    if (sup == null) return JsonSerializer.Serialize(new { success = false, message = "المندوب غير موجود" });

                    decimal totalAmount = 0m;
                    var itemsList = new List<SupplierOrderItem>();

                    if (root.TryGetProperty("items", out var itemsElem) && itemsElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in itemsElem.EnumerateArray())
                        {
                            string pName = item.GetProperty("name").GetString() ?? "";
                            string barcode = item.TryGetProperty("barcode", out var bp) ? bp.GetString() ?? "" : "";
                            decimal qty = item.GetProperty("quantity").GetDecimal();
                            decimal unitCost = item.GetProperty("unitCost").GetDecimal();
                            string unit = item.TryGetProperty("unit", out var up) ? up.GetString() ?? "قطعة" : "قطعة";
                            decimal itemTotal = qty * unitCost;
                            totalAmount += itemTotal;

                            Guid? prodId = null;
                            if (item.TryGetProperty("productId", out var pidProp) && !string.IsNullOrEmpty(pidProp.GetString()))
                            {
                                if (Guid.TryParse(pidProp.GetString(), out var pid)) prodId = pid;
                            }

                            // Update product inventory stock in database!
                            Product? targetProd = null;
                            if (prodId.HasValue) targetProd = await db.Products.FindAsync(prodId.Value);
                            if (targetProd == null && !string.IsNullOrWhiteSpace(barcode))
                            {
                                targetProd = await db.Products.FirstOrDefaultAsync(p => p.Barcode == barcode && !p.IsDeleted);
                            }
                            if (targetProd == null && !string.IsNullOrWhiteSpace(pName))
                            {
                                targetProd = await db.Products.FirstOrDefaultAsync(p => p.Name == pName && !p.IsDeleted);
                            }

                            decimal cartonsCount = item.TryGetProperty("cartonsCount", out var ccp) ? ccp.GetDecimal() : 0m;
                            decimal cartonPurchasePrice = item.TryGetProperty("cartonPurchasePrice", out var cpp) ? cpp.GetDecimal() : 0m;
                            decimal itemsPerCarton = item.TryGetProperty("itemsPerCarton", out var ipc) ? ipc.GetDecimal() : 1m;

                            // 1. Calculate the new unit price per piece
                            decimal newPieceCost = unitCost;
                            if (cartonsCount > 0 && itemsPerCarton > 0 && cartonPurchasePrice > 0)
                            {
                                newPieceCost = cartonPurchasePrice / itemsPerCarton;
                            }

                            if (targetProd != null)
                            {
                                decimal currentStock = Math.Max(0, targetProd.StockQuantity);
                                decimal oldCost = targetProd.Cost > 0 ? targetProd.Cost : newPieceCost;
                                decimal purchasedQty = qty;

                                // 2. Calculate Weighted Average Cost (متوسط التكلفة المرجح)
                                decimal newWeightedCost = newPieceCost;
                                if (currentStock > 0 && (currentStock + purchasedQty) > 0)
                                {
                                    newWeightedCost = ((currentStock * oldCost) + (purchasedQty * newPieceCost)) / (currentStock + purchasedQty);
                                }

                                targetProd.Cost = Math.Round(newWeightedCost, 2);
                                targetProd.StockQuantity += purchasedQty;

                                if (cartonsCount > 0)
                                {
                                    targetProd.CartonsCount += cartonsCount;
                                    targetProd.CartonPurchasePrice = cartonPurchasePrice;
                                    if (itemsPerCarton > 1) targetProd.ItemsPerCarton = itemsPerCarton;
                                }

                                targetProd.SupplierId = sup.Id;
                                targetProd.SupplierName = sup.Name;
                                targetProd.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                // Create new product if it doesn't exist
                                var newP = new Product
                                {
                                    Id = Guid.NewGuid(),
                                    Name = pName,
                                    Barcode = string.IsNullOrWhiteSpace(barcode) ? DateTime.Now.Ticks.ToString() : barcode,
                                    Cost = Math.Round(newPieceCost, 2),
                                    Price = Math.Round(newPieceCost * 1.25m, 2),
                                    StockQuantity = qty,
                                    CartonsCount = cartonsCount,
                                    CartonPurchasePrice = cartonPurchasePrice,
                                    ItemsPerCarton = itemsPerCarton,
                                    Unit = unit,
                                    SupplierId = sup.Id,
                                    SupplierName = sup.Name,
                                    CreatedAt = DateTime.UtcNow
                                };
                                await db.Products.AddAsync(newP);
                                targetProd = newP;
                            }

                            itemsList.Add(new SupplierOrderItem
                            {
                                Id = Guid.NewGuid(),
                                ProductId = targetProd?.Id,
                                ProductName = pName,
                                Barcode = barcode,
                                Quantity = qty,
                                UnitPrice = unitCost,
                                UnitType = unit,
                                Notes = notes
                            });
                        }
                    }

                    var order = new SupplierOrder
                    {
                        Id = Guid.NewGuid(),
                        OrderNumber = invoiceNumber,
                        OrderDate = DateTime.UtcNow,
                        SupplierId = sup.Id,
                        SupplierName = sup.Name,
                        RepresentativeName = sup.Name,
                        MarketName = "7amo Market",
                        TotalAmount = totalAmount,
                        Notes = notes,
                        Status = OrderStatus.Delivered,
                        IsConvertedToInvoice = true,
                        Items = itemsList
                    };
                    await db.SupplierOrders.AddAsync(order);

                    // Record financial transaction on supplier account
                    var sService = new SupplierService(db);
                    if (!isPaid)
                    {
                        // Credit purchase: Add to supplier balance (Market owes supplier)
                        await sService.AddTransactionAsync(sup.Id, "Purchase", totalAmount, $"فاتورة شراء مواد - وصل رقم {invoiceNumber}", invoiceNumber);
                    }
                    else
                    {
                        // Paid in cash: record purchase and payment
                        await sService.AddTransactionAsync(sup.Id, "Purchase", totalAmount, $"فاتورة شراء مواد مسددة نقداً - وصل رقم {invoiceNumber}", invoiceNumber);
                        await sService.AddTransactionAsync(sup.Id, "Payment", totalAmount, $"تسديد نقدي مباشر عن وصل شراء رقم {invoiceNumber}", invoiceNumber);
                    }

                    await db.SaveChangesAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        orderId = order.Id,
                        invoiceNumber = order.OrderNumber,
                        totalAmount = order.TotalAmount
                    });
                }

                case "get_purchase_invoice_details":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    string invoiceNumber = "";
                    Guid orderId = Guid.Empty;
                    if (doc.RootElement.TryGetProperty("invoiceNumber", out var inp)) invoiceNumber = inp.GetString() ?? "";
                    if (doc.RootElement.TryGetProperty("orderId", out var oidProp) && !string.IsNullOrEmpty(oidProp.GetString()))
                    {
                        Guid.TryParse(oidProp.GetString(), out orderId);
                    }

                    var order = await db.SupplierOrders
                        .Include(o => o.Items)
                        .Include(o => o.Supplier)
                        .FirstOrDefaultAsync(o => (orderId != Guid.Empty && o.Id == orderId) || (!string.IsNullOrEmpty(invoiceNumber) && o.OrderNumber == invoiceNumber));

                    if (order == null) return JsonSerializer.Serialize(new { success = false, message = "الوصل غير موجود" });

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        order = new
                        {
                            id = order.Id,
                            invoiceNumber = order.OrderNumber,
                            date = order.OrderDate.ToString("yyyy-MM-dd HH:mm"),
                            supplierName = order.Supplier?.Name ?? order.SupplierName,
                            company = order.Supplier?.Company ?? "شركة عامة",
                            phone = order.Supplier?.Phone ?? "--",
                            totalAmount = order.TotalAmount,
                            notes = order.Notes,
                            items = order.Items.Select(it => new
                            {
                                productName = it.ProductName,
                                barcode = it.Barcode,
                                quantity = it.Quantity,
                                unitPrice = it.UnitPrice,
                                totalPrice = it.Quantity * it.UnitPrice,
                                unitType = it.UnitType
                            })
                        }
                    });
                }

                case "save_supplier":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var root = doc.RootElement;
                    Guid supId = Guid.Empty;
                    if (root.TryGetProperty("id", out var idProp) && !string.IsNullOrEmpty(idProp.GetString()))
                    {
                        Guid.TryParse(idProp.GetString(), out supId);
                    }
                    string name = root.GetProperty("name").GetString() ?? "";
                    string? company = root.TryGetProperty("company", out var cp) ? cp.GetString() : null;
                    string? phone = root.TryGetProperty("phone", out var pp) ? pp.GetString() : null;
                    string? address = root.TryGetProperty("address", out var ap) ? ap.GetString() : null;
                    string? notes = root.TryGetProperty("notes", out var ntp) ? ntp.GetString() : null;
                    decimal balance = root.TryGetProperty("balance", out var bp) ? bp.GetDecimal() : 0.0m;

                    Supplier? sup = null;
                    if (supId != Guid.Empty)
                    {
                        sup = await db.Suppliers.FindAsync(supId);
                    }

                    if (sup == null)
                    {
                        sup = new Supplier
                        {
                            Id = supId != Guid.Empty ? supId : Guid.NewGuid(),
                            Name = name,
                            Company = company,
                            Phone = phone,
                            Address = address,
                            Notes = notes,
                            OpeningBalance = balance,
                            Balance = balance,
                            CreatedAt = DateTime.UtcNow
                        };
                        await db.Suppliers.AddAsync(sup);
                    }
                    else
                    {
                        sup.Name = name;
                        sup.Company = company;
                        sup.Phone = phone;
                        sup.Address = address;
                        sup.Notes = notes;
                        sup.Balance = balance;
                        sup.UpdatedAt = DateTime.UtcNow;
                        sup.IsDeleted = false;
                    }

                    await db.SaveChangesAsync();
                    return JsonSerializer.Serialize(new { success = true, supplierId = sup.Id });
                }

                case "delete_supplier":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var supId = doc.RootElement.GetProperty("id").GetGuid();
                    var sup = await db.Suppliers.FindAsync(supId);
                    if (sup != null)
                    {
                        sup.IsDeleted = true;
                        sup.UpdatedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }
                    return JsonSerializer.Serialize(new { success = true });
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

                case "return_to_supplier":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var root = doc.RootElement;
                    var supId = root.GetProperty("supplierId").GetGuid();
                    var prodId = root.TryGetProperty("productId", out var pidProp) && !string.IsNullOrEmpty(pidProp.GetString()) && Guid.TryParse(pidProp.GetString(), out var pid) ? pid : (Guid?)null;
                    string barcode = root.TryGetProperty("barcode", out var bp) ? bp.GetString() ?? "" : "";
                    string prodName = root.TryGetProperty("productName", out var pnp) ? pnp.GetString() ?? "" : "";
                    decimal qty = root.GetProperty("quantity").GetDecimal();
                    decimal unitCost = root.GetProperty("unitCost").GetDecimal();
                    string reason = root.TryGetProperty("reason", out var rp) ? rp.GetString() ?? "إرجاع بضاعة لمندوب" : "إرجاع بضاعة لمندوب";
                    string financialAction = root.TryGetProperty("financialAction", out var fap) ? fap.GetString() ?? "deduct_balance" : "deduct_balance";
                    string notes = root.TryGetProperty("notes", out var np) ? np.GetString() ?? "" : "";
                    string returnNumber = root.TryGetProperty("returnNumber", out var rnp) ? rnp.GetString() ?? $"RET-{DateTime.Now:yyyyMMddHHmmss}" : $"RET-{DateTime.Now:yyyyMMddHHmmss}";

                    var sup = await db.Suppliers.FindAsync(supId);
                    if (sup == null) return JsonSerializer.Serialize(new { success = false, message = "المندوب غير موجود" });

                    Product? targetProd = null;
                    if (prodId.HasValue) targetProd = await db.Products.FindAsync(prodId.Value);
                    if (targetProd == null && !string.IsNullOrWhiteSpace(barcode))
                        targetProd = await db.Products.FirstOrDefaultAsync(p => p.Barcode == barcode && !p.IsDeleted);
                    if (targetProd == null && !string.IsNullOrWhiteSpace(prodName))
                        targetProd = await db.Products.FirstOrDefaultAsync(p => p.Name == prodName && !p.IsDeleted);

                    if (targetProd != null)
                    {
                        targetProd.StockQuantity = Math.Max(0, targetProd.StockQuantity - qty);
                        if (targetProd.ItemsPerCarton > 0)
                            targetProd.CartonsCount = Math.Floor(targetProd.StockQuantity / targetProd.ItemsPerCarton);
                        targetProd.UpdatedAt = DateTime.UtcNow;
                        prodName = targetProd.Name;
                    }

                    decimal totalAmount = qty * unitCost;
                    var sService = new SupplierService(db);

                    if (financialAction == "cash_refund")
                    {
                        await sService.AddTransactionAsync(supId, "ReturnCash", totalAmount, $"إرجاع بضاعة ({prodName} × {qty}) - استرداد نقدي فوري - وصل {returnNumber} ({reason})", returnNumber);
                    }
                    else
                    {
                        // Deduct from supplier debt / balance
                        await sService.AddTransactionAsync(supId, "Return", totalAmount, $"إرجاع بضاعة ({prodName} × {qty}) - خصم من حساب المورد - وصل {returnNumber} ({reason})", returnNumber);
                    }

                    await db.SaveChangesAsync();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        returnNumber,
                        supplierName = sup.Name,
                        productName = prodName,
                        quantity = qty,
                        unitCost = unitCost,
                        totalAmount = totalAmount,
                        financialAction = financialAction,
                        message = $"تم تسجيل إرجاع {qty} من ({prodName}) للمندوب ({sup.Name}) بنجاح!"
                    });
                }

                case "get_supplier_transactions":
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    var supId = doc.RootElement.GetProperty("supplierId").GetGuid();
                    var sService = new SupplierService(db);
                    var trans = await sService.GetSupplierTransactionsAsync(supId);
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        transactions = trans.Select(t => new
                        {
                            t.Id,
                            t.TransactionType,
                            t.Amount,
                            t.Description,
                            t.InvoiceNumber,
                            date = t.TransactionDate.ToString("yyyy-MM-dd HH:mm")
                        })
                    });
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
                    var asmVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                    string verStr = asmVer != null ? $"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}" : "1.4.0";
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        appName = "7amo.pos",
                        version = $"v{verStr} Pro",
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
