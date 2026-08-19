using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class WeeklySalesBarItem : BaseViewModel
{
    public string DayName { get; set; } = string.Empty;
    public string ShortDate { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int InvoicesCount { get; set; }
    public double BarHeight { get; set; } = 10;
    public string FormattedRevenue => $"{Revenue:N0} د.ع";
    public bool IsToday { get; set; }
}

public class PaymentDistributionItem : BaseViewModel
{
    public string MethodName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public double Percentage { get; set; }
    public string FormattedAmount => $"{Amount:N0} د.ع";
    public string FormattedPercentage => $"{Percentage:F1}%";
    public string ColorHex { get; set; } = "#3B82F6";
    public string Icon { get; set; } = "💳";
}

public class TopProductBarItem : BaseViewModel
{
    public int Rank { get; set; }
    public string RankBadge { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal TotalAmount { get; set; }
    public double BarPercent { get; set; } = 20;
    public string FormattedAmount => $"{TotalAmount:N0} د.ع";
    public string BarColor { get; set; } = "#3B82F6";
}

public class HourlyActivityItem : BaseViewModel
{
    public string TimeSlotName { get; set; } = string.Empty;
    public string Icon { get; set; } = "☀️";
    public int SalesCount { get; set; }
    public decimal TotalAmount { get; set; }
    public double ActivityPercent { get; set; } = 15;
    public string FormattedAmount => $"{TotalAmount:N0} د.ع";
    public string ColorHex { get; set; } = "#3B82F6";
}

public class DashboardViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly ISaleService _saleService;
    private readonly IProductService _productService;

    public decimal TodayRevenue { get => _todayRevenue; set => SetProperty(ref _todayRevenue, value); }
    private decimal _todayRevenue;

    public int TodayInvoicesCount { get => _todayInvoicesCount; set => SetProperty(ref _todayInvoicesCount, value); }
    private int _todayInvoicesCount;

    public decimal MonthlyRevenue { get => _monthlyRevenue; set => SetProperty(ref _monthlyRevenue, value); }
    private decimal _monthlyRevenue;

    public int TotalProductsCount { get => _totalProductsCount; set => SetProperty(ref _totalProductsCount, value); }
    private int _totalProductsCount;

    public int LowStockCount { get => _lowStockCount; set => SetProperty(ref _lowStockCount, value); }
    private int _lowStockCount;

    public decimal WeeklyTotalRevenue { get => _weeklyTotalRevenue; set => SetProperty(ref _weeklyTotalRevenue, value); }
    private decimal _weeklyTotalRevenue;

    public decimal DailyAverageRevenue { get => _dailyAverageRevenue; set => SetProperty(ref _dailyAverageRevenue, value); }
    private decimal _dailyAverageRevenue;

    // Charts Collections
    public ObservableCollection<WeeklySalesBarItem> WeeklySalesBars { get; } = new();
    public ObservableCollection<PaymentDistributionItem> PaymentDistributions { get; } = new();
    public ObservableCollection<TopProductBarItem> TopProductsChart { get; } = new();
    public ObservableCollection<HourlyActivityItem> HourlyActivities { get; } = new();

    // Tables Collections
    public ObservableCollection<Product> LowStockProducts { get; } = new();
    public ObservableCollection<Sale> RecentSales { get; } = new();

    public ICommand RefreshCommand { get; }

    public DashboardViewModel()
    {
        _context = new AppDbContext();
        _saleService = new SaleService(_context);
        _productService = new ProductService(_context);

        RefreshCommand = new AsyncRelayCommand(async () => await LoadDashboardDataAsync());
    }

    public async Task LoadDashboardDataAsync()
    {
        try
        {
            // 1. KPI Top Stats
            var todayStats = await _saleService.GetTodayStatsAsync();
            TodayRevenue = todayStats.TotalRevenue;
            TodayInvoicesCount = todayStats.TotalSalesCount;

            var monthStats = await _saleService.GetMonthlyStatsAsync();
            MonthlyRevenue = monthStats.MonthlyRevenue;

            TotalProductsCount = await _productService.GetTotalProductsCountAsync();

            var lowStockList = await _productService.GetLowStockProductsAsync(6);
            LowStockCount = lowStockList.Count;
            LowStockProducts.Clear();
            foreach (var item in lowStockList)
            {
                LowStockProducts.Add(item);
            }

            var recentSalesList = await _saleService.GetRecentSalesAsync(5);
            RecentSales.Clear();
            foreach (var s in recentSalesList)
            {
                RecentSales.Add(s);
            }

            // 2. Chart 1: Last 7 Days Sales Trend
            await LoadWeeklySalesChartAsync();

            // 3. Chart 2: Payment Distribution Breakdown
            await LoadPaymentDistributionChartAsync();

            // 4. Chart 3: Top 5 Best Selling Products
            await LoadTopSellingProductsChartAsync();

            // 5. Chart 4: Peak Hours Activity
            await LoadHourlyActivityChartAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] Error: {ex.Message}");
        }
    }

    private async Task LoadWeeklySalesChartAsync()
    {
        var today = DateTime.UtcNow.Date;
        var sevenDaysAgo = today.AddDays(-6);

        var pastWeekSales = await _context.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= sevenDaysAgo && s.Status == "Completed")
            .ToListAsync();

        var dayGroups = pastWeekSales
            .GroupBy(s => s.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => new { Count = g.Count(), Total = g.Sum(x => x.TotalAmount) });

        WeeklySalesBars.Clear();
        decimal maxRev = 0;
        decimal weekTotal = 0;

        var cultureAr = new CultureInfo("ar-IQ");
        var daysList = new List<WeeklySalesBarItem>();

        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            decimal rev = 0;
            int count = 0;

            if (dayGroups.TryGetValue(date, out var stats))
            {
                rev = stats.Total;
                count = stats.Count;
            }

            if (rev > maxRev) maxRev = rev;
            weekTotal += rev;

            string dayLabel;
            if (i == 0)
            {
                dayLabel = Loc.IsKurdish ? "ئەمڕۆ" : "اليوم";
            }
            else if (i == 1)
            {
                dayLabel = Loc.IsKurdish ? "دوێنێ" : "أمس";
            }
            else
            {
                dayLabel = Loc.IsKurdish ? GetKurdishDayName(date.DayOfWeek) : date.ToString("dddd", cultureAr);
            }

            daysList.Add(new WeeklySalesBarItem
            {
                DayName = dayLabel,
                ShortDate = date.ToString("MM/dd"),
                Revenue = rev,
                InvoicesCount = count,
                IsToday = (i == 0)
            });
        }

        WeeklyTotalRevenue = weekTotal;
        DailyAverageRevenue = weekTotal / 7m;

        // Scale bar heights nicely (between 14px min and 130px max)
        double maxBarH = 130.0;
        foreach (var d in daysList)
        {
            if (maxRev > 0)
            {
                d.BarHeight = Math.Max(16.0, (double)(d.Revenue / maxRev) * maxBarH);
            }
            else
            {
                d.BarHeight = 16.0;
            }
            WeeklySalesBars.Add(d);
        }
    }

    private string GetKurdishDayName(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Sunday => "یەکشەممە",
        DayOfWeek.Monday => "دووشەممە",
        DayOfWeek.Tuesday => "سێشەممە",
        DayOfWeek.Wednesday => "چوارشەممە",
        DayOfWeek.Thursday => "پێنجشەممە",
        DayOfWeek.Friday => "هەینی",
        _ => "شەممە"
    };

    private async Task LoadPaymentDistributionChartAsync()
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var sales = await _context.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= startOfMonth && s.Status == "Completed")
            .ToListAsync();

        decimal totalSalesAmount = sales.Sum(s => s.TotalAmount);
        decimal cashTotal = sales.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount);
        decimal cardTotal = sales.Where(s => s.PaymentMethod == "Card").Sum(s => s.TotalAmount);
        decimal debtTotal = sales.Where(s => s.PaymentMethod == "Debt" || s.PaymentMethod == "Partial").Sum(s => s.TotalAmount);

        PaymentDistributions.Clear();

        if (totalSalesAmount > 0)
        {
            PaymentDistributions.Add(new PaymentDistributionItem
            {
                MethodName = "نقد (Cash)",
                Amount = cashTotal,
                Percentage = (double)(cashTotal / totalSalesAmount) * 100.0,
                ColorHex = "#10B981",
                Icon = "💵"
            });

            PaymentDistributions.Add(new PaymentDistributionItem
            {
                MethodName = "بطاقة (Card)",
                Amount = cardTotal,
                Percentage = (double)(cardTotal / totalSalesAmount) * 100.0,
                ColorHex = "#3B82F6",
                Icon = "💳"
            });

            PaymentDistributions.Add(new PaymentDistributionItem
            {
                MethodName = "آجل (Debt)",
                Amount = debtTotal,
                Percentage = (double)(debtTotal / totalSalesAmount) * 100.0,
                ColorHex = "#F59E0B",
                Icon = "⏳"
            });
        }
        else
        {
            PaymentDistributions.Add(new PaymentDistributionItem { MethodName = "نقد (Cash)", Amount = 0, Percentage = 0, ColorHex = "#10B981", Icon = "💵" });
            PaymentDistributions.Add(new PaymentDistributionItem { MethodName = "بطاقة (Card)", Amount = 0, Percentage = 0, ColorHex = "#3B82F6", Icon = "💳" });
            PaymentDistributions.Add(new PaymentDistributionItem { MethodName = "آجل (Debt)", Amount = 0, Percentage = 0, ColorHex = "#F59E0B", Icon = "⏳" });
        }
    }

    private async Task LoadTopSellingProductsChartAsync()
    {
        var topItems = await _saleService.GetTopSellingProductsAsync(5);
        TopProductsChart.Clear();

        decimal maxQty = topItems.Count > 0 ? topItems.Max(x => x.TotalQuantity) : 1;
        if (maxQty == 0) maxQty = 1;

        string[] colors = { "#3B82F6", "#10B981", "#8B5CF6", "#F59E0B", "#EC4899" };
        string[] badges = { "🥇", "🥈", "🥉", "4", "5" };

        int rank = 1;
        foreach (var item in topItems)
        {
            double pct = Math.Max(15.0, (double)(item.TotalQuantity / maxQty) * 100.0);
            TopProductsChart.Add(new TopProductBarItem
            {
                Rank = rank,
                RankBadge = badges[Math.Min(rank - 1, badges.Length - 1)],
                ProductName = item.ProductName,
                Quantity = item.TotalQuantity,
                TotalAmount = item.TotalAmount,
                BarPercent = pct,
                BarColor = colors[(rank - 1) % colors.Length]
            });
            rank++;
        }
    }

    private async Task LoadHourlyActivityChartAsync()
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var sales = await _context.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= startOfMonth && s.Status == "Completed")
            .ToListAsync();

        var morningSales = sales.Where(s => s.CreatedAt.ToLocalTime().Hour >= 8 && s.CreatedAt.ToLocalTime().Hour < 12).ToList();
        var noonSales = sales.Where(s => s.CreatedAt.ToLocalTime().Hour >= 12 && s.CreatedAt.ToLocalTime().Hour < 16).ToList();
        var eveningSales = sales.Where(s => s.CreatedAt.ToLocalTime().Hour >= 16 && s.CreatedAt.ToLocalTime().Hour < 20).ToList();
        var nightSales = sales.Where(s => s.CreatedAt.ToLocalTime().Hour >= 20 || s.CreatedAt.ToLocalTime().Hour < 8).ToList();

        int maxCount = Math.Max(1, Math.Max(Math.Max(morningSales.Count, noonSales.Count), Math.Max(eveningSales.Count, nightSales.Count)));

        HourlyActivities.Clear();

        HourlyActivities.Add(new HourlyActivityItem
        {
            TimeSlotName = "الصباح (8ص - 12ظ)",
            Icon = "🌅",
            SalesCount = morningSales.Count,
            TotalAmount = morningSales.Sum(s => s.TotalAmount),
            ActivityPercent = Math.Max(10.0, ((double)morningSales.Count / maxCount) * 100.0),
            ColorHex = "#F59E0B"
        });

        HourlyActivities.Add(new HourlyActivityItem
        {
            TimeSlotName = "الظهيرة (12ظ - 4ع)",
            Icon = "☀️",
            SalesCount = noonSales.Count,
            TotalAmount = noonSales.Sum(s => s.TotalAmount),
            ActivityPercent = Math.Max(10.0, ((double)noonSales.Count / maxCount) * 100.0),
            ColorHex = "#EF4444"
        });

        HourlyActivities.Add(new HourlyActivityItem
        {
            TimeSlotName = "المساء (4ع - 8م)",
            Icon = "🌆",
            SalesCount = eveningSales.Count,
            TotalAmount = eveningSales.Sum(s => s.TotalAmount),
            ActivityPercent = Math.Max(10.0, ((double)eveningSales.Count / maxCount) * 100.0),
            ColorHex = "#3B82F6"
        });

        HourlyActivities.Add(new HourlyActivityItem
        {
            TimeSlotName = "الليل (8م - 12ل)",
            Icon = "🌙",
            SalesCount = nightSales.Count,
            TotalAmount = nightSales.Sum(s => s.TotalAmount),
            ActivityPercent = Math.Max(10.0, ((double)nightSales.Count / maxCount) * 100.0),
            ColorHex = "#8B5CF6"
        });
    }
}
