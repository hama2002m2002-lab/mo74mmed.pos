using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;

namespace HamoPos.Services;

public class SaleService : ISaleService
{
    private readonly AppDbContext _context;

    public SaleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextInvoiceNumberAsync()
    {
        string datePrefix = DateTime.Now.ToString("yyyyMMdd");
        string prefix = $"INV-{datePrefix}-";

        int countToday = await _context.Sales
            .IgnoreQueryFilters()
            .CountAsync(s => s.InvoiceNumber.StartsWith(prefix));

        return $"{prefix}{(countToday + 1):D4}";
    }

    public async Task<Sale> CompleteSaleAsync(Sale sale)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(sale.InvoiceNumber))
            {
                sale.InvoiceNumber = await GenerateNextInvoiceNumberAsync();
            }

            sale.CreatedAt = DateTime.UtcNow;

            foreach (var item in sale.Items)
            {
                if (item.ProductId.HasValue && item.ProductId != Guid.Empty)
                {
                    var product = await _context.Products.FindAsync(item.ProductId.Value);
                    if (product != null)
                    {
                        // خصم دقيق للمفرد والكرتون وإعادة كميات المرتجع
                        if (item.ProductName.Contains("(إرجاع)") || item.TotalPrice < 0)
                        {
                            product.StockQuantity += item.Quantity;
                            if (product.ItemsPerCarton > 0)
                            {
                                product.CartonsCount = (int)(product.StockQuantity / product.ItemsPerCarton);
                            }
                        }
                        else if (item.ProductName.Contains("(كرتون)"))
                        {
                            decimal itemsPerCarton = product.ItemsPerCarton > 0 ? product.ItemsPerCarton : 1;
                            decimal totalPiecesDeducted = item.Quantity * itemsPerCarton;
                            product.StockQuantity -= totalPiecesDeducted;
                            product.CartonsCount -= (int)item.Quantity;
                        }
                        else
                        {
                            product.StockQuantity -= item.Quantity;
                            if (product.ItemsPerCarton > 0)
                            {
                                product.CartonsCount = (int)(product.StockQuantity / product.ItemsPerCarton);
                            }
                        }

                        product.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            await _context.Sales.AddAsync(sale);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return sale;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> ReturnSaleAsync(Guid saleId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var originalSale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == saleId);

            if (originalSale == null || originalSale.Status == "Returned" || originalSale.InvoiceNumber.StartsWith("RET-"))
                return false;

            string retInvoiceNumber = originalSale.InvoiceNumber.StartsWith("INV-")
                ? "RET-" + originalSale.InvoiceNumber.Substring(4)
                : "RET-" + originalSale.InvoiceNumber;

            // التأكد من عدم تكرار الإرجاع
            bool alreadyReturned = await _context.Sales.AnyAsync(s => s.InvoiceNumber == retInvoiceNumber);
            if (alreadyReturned)
                return false;

            // 1. الإبقاء على الوصل الأصلي كمبيعات مكتملة وتوثيق الإرجاع في ملاحظاته
            originalSale.Notes = string.IsNullOrWhiteSpace(originalSale.Notes)
                ? $"[تم الاسترجاع بالوصل {retInvoiceNumber}]"
                : $"{originalSale.Notes} [تم الاسترجاع بالوصل {retInvoiceNumber}]";
            originalSale.UpdatedAt = DateTime.UtcNow;

            // 2. إنشاء وصل إرجاع جديد منفصل ومستقل بتوقيت وتاريخ الإرجاع اللحظي
            var returnSale = new Sale
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = retInvoiceNumber,
                UserId = originalSale.UserId,
                SubTotal = originalSale.SubTotal,
                TaxAmount = originalSale.TaxAmount,
                DiscountAmount = originalSale.DiscountAmount,
                TotalAmount = originalSale.TotalAmount,
                PaidAmount = originalSale.PaidAmount,
                ChangeAmount = originalSale.ChangeAmount,
                PaymentMethod = originalSale.PaymentMethod,
                Status = "Returned",
                CustomerName = originalSale.CustomerName,
                Notes = $"وصل إرجاع للمبيعات رقم {originalSale.InvoiceNumber}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var item in originalSale.Items)
            {
                var returnItem = new SaleItem
                {
                    Id = Guid.NewGuid(),
                    SaleId = returnSale.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Barcode = item.Barcode,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice
                };
                returnSale.Items.Add(returnItem);
            }

            await _context.Sales.AddAsync(returnSale);

            // 3. استرجاع كميات المواد والكراتين بدقة للمخزن
            foreach (var item in originalSale.Items)
            {
                if (item.ProductId.HasValue && item.ProductId != Guid.Empty)
                {
                    var product = await _context.Products.FindAsync(item.ProductId.Value);
                    if (product != null)
                    {
                        if (item.ProductName.Contains("(كرتون)"))
                        {
                            decimal itemsPerCarton = product.ItemsPerCarton > 0 ? product.ItemsPerCarton : 1;
                            product.StockQuantity += (item.Quantity * itemsPerCarton);
                            product.CartonsCount += (int)item.Quantity;
                        }
                        else
                        {
                            product.StockQuantity += item.Quantity;
                            if (product.ItemsPerCarton > 0)
                            {
                                product.CartonsCount = (int)(product.StockQuantity / product.ItemsPerCarton);
                            }
                        }

                        product.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<List<Sale>> GetAllInvoicesArchiveAsync(int take = 100)
    {
        return await _context.Sales
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Items)
            .OrderByDescending(s => s.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<(int TotalSalesCount, decimal TotalRevenue, decimal CashTotal, decimal CardTotal)> GetTodayStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var todaySales = await _context.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= today)
            .ToListAsync();

        var completedSales = todaySales.Where(s => s.Status == "Completed").ToList();
        var returnedSales = todaySales.Where(s => s.Status == "Returned").ToList();

        int count = completedSales.Count;
        decimal completedRev = completedSales.Sum(s => s.TotalAmount);
        decimal returnedRev = returnedSales.Sum(s => s.TotalAmount);
        decimal total = completedRev - returnedRev;

        decimal cash = completedSales.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount)
                     - returnedSales.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount);

        decimal card = completedSales.Where(s => s.PaymentMethod == "Card").Sum(s => s.TotalAmount)
                     - returnedSales.Where(s => s.PaymentMethod == "Card").Sum(s => s.TotalAmount);

        return (count, total, cash, card);
    }

    public async Task<(int MonthlySalesCount, decimal MonthlyRevenue)> GetMonthlyStatsAsync()
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthSales = await _context.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= startOfMonth)
            .ToListAsync();

        var completedSales = monthSales.Where(s => s.Status == "Completed").ToList();
        var returnedSales = monthSales.Where(s => s.Status == "Returned").ToList();

        int count = completedSales.Count;
        decimal total = completedSales.Sum(s => s.TotalAmount) - returnedSales.Sum(s => s.TotalAmount);

        return (count, total);
    }

    public async Task<List<Sale>> GetRecentSalesAsync(int take = 10)
    {
        return await _context.Sales
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Items)
            .Where(s => s.Status == "Completed")
            .OrderByDescending(s => s.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<Sale>> GetSalesHistoryAsync(DateTime fromDate, DateTime toDate)
    {
        var fromUtc = fromDate.Date.ToUniversalTime();
        var toUtc = toDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

        return await _context.Sales
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Items)
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt <= toUtc)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<(string ProductName, decimal TotalQuantity, decimal TotalAmount)>> GetTopSellingProductsAsync(int take = 5)
    {
        var topItems = await _context.SaleItems
            .AsNoTracking()
            .Where(i => i.Sale != null && i.Sale.Status == "Completed")
            .GroupBy(i => i.ProductName)
            .Select(g => new
            {
                ProductName = g.Key,
                TotalQuantity = g.Sum(x => x.Quantity),
                TotalAmount = g.Sum(x => x.TotalPrice)
            })
            .OrderByDescending(x => x.TotalQuantity)
            .Take(take)
            .ToListAsync();

        return topItems.Select(x => (x.ProductName, x.TotalQuantity, x.TotalAmount)).ToList();
    }
}
