using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HamoPos.Models;

namespace HamoPos.Services;

public interface ISaleService
{
    Task<string> GenerateNextInvoiceNumberAsync();
    Task<Sale> CompleteSaleAsync(Sale sale);
    Task<bool> ReturnSaleAsync(Guid saleId);
    Task<List<Sale>> GetAllInvoicesArchiveAsync(int take = 100);
    Task<(int TotalSalesCount, decimal TotalRevenue, decimal CashTotal, decimal CardTotal)> GetTodayStatsAsync();
    Task<(int MonthlySalesCount, decimal MonthlyRevenue)> GetMonthlyStatsAsync();
    Task<List<Sale>> GetRecentSalesAsync(int take = 10);
    Task<List<Sale>> GetSalesHistoryAsync(DateTime fromDate, DateTime toDate);
    Task<List<(string ProductName, decimal TotalQuantity, decimal TotalAmount)>> GetTopSellingProductsAsync(int take = 5);
}
