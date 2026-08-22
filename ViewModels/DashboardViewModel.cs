using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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

    // Visual Charts Collections
    public ObservableCollection<WeeklySalesBarItem> WeeklySalesBars { get; } = new();
    public ObservableCollection<PaymentDistributionItem> PaymentDistribution { get; } = new();
    public ObservableCollection<TopProductBarItem> TopProductsChart { get; } = new();
    public ObservableCollection<HourlyActivityItem> HourlyActivities { get; } = new();

    // Tables Collections
    public ObservableCollection<Product> LowStockProducts { get; } = new();
    public ObservableCollection<Sale> RecentSales { get; } = new();

    public ICommand RefreshCommand { get; }

    public DashboardViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(async () => await LoadDashboardDataAsync());
    }

    public async Task LoadDashboardDataAsync()
    {
        try
        {
            using var db = new AppDbContext();
            var saleService = new SaleService(db);
            var productService = new ProductService(db);

            // 1. KPI Top Stats
            var todayStats = await saleService.GetTodayStatsAsync();
            var monthStats = await saleService.GetMonthlyStatsAsync();
            int totalProducts = await productService.GetTotalProductsCountAsync();
            var lowStockList = await productService.GetLowStockProductsAsync(6);
            var recentSalesList = await saleService.GetRecentSalesAsync(5);

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                TodayRevenue = todayStats.TotalRevenue;
                TodayInvoicesCount = todayStats.TotalSalesCount;
                MonthlyRevenue = monthStats.MonthlyRevenue;
                TotalProductsCount = totalProducts;
                LowStockCount = lowStockList.Count;

                LowStockProducts.Clear();
                foreach (var item in lowStockList)
                {
                    LowStockProducts.Add(item);
                }

                RecentSales.Clear();
                foreach (var s in recentSalesList)
                {
                    RecentSales.Add(s);
                }
            });

            // 2. Visual Charts
            await LoadWeeklySalesChartAsync(db);
            await LoadPaymentDistributionChartAsync(db);
            await LoadTopSellingProductsChartAsync(db);
            await LoadHourlyActivityChartAsync(db);

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                OnPropertyChanged(nameof(Loc));
                OnPropertyChanged(nameof(TodayRevenue));
                OnPropertyChanged(nameof(TodayInvoicesCount));
                OnPropertyChanged(nameof(MonthlyRevenue));
                OnPropertyChanged(nameof(LowStockCount));
                OnPropertyChanged(nameof(TotalProductsCount));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] Error: {ex.Message}");
        }
    }

    private async Task LoadWeeklySalesChartAsync(AppDbContext db)
    {
        var today = DateTime.UtcNow.Date;
        var sevenDaysAgo = today.AddDays(-6);

        var pastWeekSales = await db.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= sevenDaysAgo && s.Status == "Completed")
            .ToListAsync();

        var dayGroups = pastWeekSales
            .GroupBy(s => s.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => new { Count = g.Count(), Total = g.Sum(x => x.TotalAmount) });

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
        var finalBars = new List<WeeklySalesBarItem>();
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
            finalBars.Add(d);
        }

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            WeeklySalesBars.Clear();
            foreach (var b in finalBars)
            {
                WeeklySalesBars.Add(b);
            }
        });
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

    private async Task LoadPaymentDistributionChartAsync(AppDbContext db)
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var sales = await db.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= startOfMonth && s.Status == "Completed")
            .ToListAsync();

        decimal totalMonth = sales.Sum(s => s.TotalAmount);

        var cashSales = sales.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount);
        var debtSales = sales.Where(s => s.PaymentMethod == "Debt").Sum(s => s.TotalAmount);
        var cardSales = sales.Where(s => s.PaymentMethod == "Card" || s.PaymentMethod == "Visa" || s.PaymentMethod == "MasterCard").Sum(s => s.TotalAmount);
        var partialSales = sales.Where(s => s.PaymentMethod == "Partial").Sum(s => s.TotalAmount);

        var finalDist = new List<PaymentDistributionItem>();
        if (totalMonth == 0)
        {
            finalDist.Add(new PaymentDistributionItem
            {
                MethodName = Loc.IsKurdish ? "نەقد (کاش)" : "نقداً (كاش)",
                Amount = 0,
                Percentage = 0,
                ColorHex = "#10B981",
                Icon = "💵"
            });
            finalDist.Add(new PaymentDistributionItem
            {
                MethodName = Loc.IsKurdish ? "قەرز (آجل)" : "آجل (ذمم ديون)",
                Amount = 0,
                Percentage = 0,
                ColorHex = "#F59E0B",
                Icon = "📝"
            });
        }
        else
        {
            if (cashSales > 0)
            {
                finalDist.Add(new PaymentDistributionItem
                {
                    MethodName = Loc.IsKurdish ? "نەقد (کاش)" : "نقداً (كاش)",
                    Amount = cashSales,
                    Percentage = (double)(cashSales / totalMonth) * 100.0,
                    ColorHex = "#10B981",
                    Icon = "💵"
                });
            }

            if (debtSales > 0)
            {
                finalDist.Add(new PaymentDistributionItem
                {
                    MethodName = Loc.IsKurdish ? "قەرز (آجل)" : "آجل (ذمم ديون)",
                    Amount = debtSales,
                    Percentage = (double)(debtSales / totalMonth) * 100.0,
                    ColorHex = "#F59E0B",
                    Icon = "📝"
                });
            }

            if (cardSales > 0)
            {
                finalDist.Add(new PaymentDistributionItem
                {
                    MethodName = Loc.IsKurdish ? "کارتی ئەلیکترۆنی" : "بطاقة دفع إلكتروني",
                    Amount = cardSales,
                    Percentage = (double)(cardSales / totalMonth) * 100.0,
                    ColorHex = "#3B82F6",
                    Icon = "💳"
                });
            }

            if (partialSales > 0)
            {
                finalDist.Add(new PaymentDistributionItem
                {
                    MethodName = Loc.IsKurdish ? "پارەی بەشەکی" : "دفع جزئي",
                    Amount = partialSales,
                    Percentage = (double)(partialSales / totalMonth) * 100.0,
                    ColorHex = "#8B5CF6",
                    Icon = "⚖️"
                });
            }
        }

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            PaymentDistribution.Clear();
            foreach (var item in finalDist)
            {
                PaymentDistribution.Add(item);
            }
        });
    }

    private async Task LoadTopSellingProductsChartAsync(AppDbContext db)
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var items = await db.SaleItems
            .AsNoTracking()
            .Include(i => i.Sale)
            .Where(i => i.Sale != null && i.Sale.CreatedAt >= startOfMonth && i.Sale.Status == "Completed")
            .GroupBy(i => i.ProductName)
            .Select(g => new
            {
                Name = g.Key,
                Qty = g.Sum(x => x.Quantity),
                Total = g.Sum(x => x.TotalPrice)
            })
            .OrderByDescending(x => x.Total)
            .Take(5)
            .ToListAsync();

        var finalProducts = new List<TopProductBarItem>();
        if (items.Any())
        {
            decimal maxTotal = items.Max(x => x.Total);
            string[] colors = { "#3B82F6", "#10B981", "#8B5CF6", "#F59E0B", "#EC4899" };
            string[] badges = { "🥇 #1", "🥈 #2", "🥉 #3", "4️⃣ #4", "5️⃣ #5" };

            for (int i = 0; i < items.Count; i++)
            {
                var p = items[i];
                double percent = maxTotal > 0 ? (double)(p.Total / maxTotal) * 100.0 : 20.0;
                finalProducts.Add(new TopProductBarItem
                {
                    Rank = i + 1,
                    RankBadge = badges[Math.Min(i, badges.Length - 1)],
                    ProductName = p.Name,
                    Quantity = p.Qty,
                    TotalAmount = p.Total,
                    BarPercent = Math.Max(12.0, percent),
                    BarColor = colors[i % colors.Length]
                });
            }
        }

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            TopProductsChart.Clear();
            foreach (var item in finalProducts)
            {
                TopProductsChart.Add(item);
            }
        });
    }

    private async Task LoadHourlyActivityChartAsync(AppDbContext db)
    {
        var today = DateTime.UtcNow.Date;
        var todaySales = await db.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= today && s.Status == "Completed")
            .ToListAsync();

        // 4 Time slots
        var slots = new[]
        {
            new { NameAr = "الصباح الباكر (06:00 - 12:00)", NameKu = "بەیانی زوو (06:00 - 12:00)", From = 6, To = 12, Icon = "🌅", Color = "#06B6D4" },
            new { NameAr = "فترة الظهيرة (12:00 - 17:00)", NameKu = "نیوەڕۆ (12:00 - 17:00)", From = 12, To = 17, Icon = "☀️", Color = "#F59E0B" },
            new { NameAr = "فترة المساء (17:00 - 22:00)", NameKu = "ئێوارە (17:00 - 22:00)", From = 17, To = 22, Icon = "🌇", Color = "#3B82F6" },
            new { NameAr = "الليل المتأخر (22:00 - 06:00)", NameKu = "شەوی درەنگ (22:00 - 06:00)", From = 22, To = 6, Icon = "🌙", Color = "#8B5CF6" }
        };

        decimal maxSlotAmount = 0;
        var results = new List<HourlyActivityItem>();

        foreach (var slot in slots)
        {
            var matched = todaySales.Where(s =>
            {
                int h = s.CreatedAt.Hour;
                if (slot.From < slot.To) return h >= slot.From && h < slot.To;
                return h >= slot.From || h < slot.To;
            }).ToList();

            decimal sum = matched.Sum(x => x.TotalAmount);
            if (sum > maxSlotAmount) maxSlotAmount = sum;

            results.Add(new HourlyActivityItem
            {
                TimeSlotName = Loc.IsKurdish ? slot.NameKu : slot.NameAr,
                Icon = slot.Icon,
                SalesCount = matched.Count,
                TotalAmount = sum,
                ColorHex = slot.Color
            });
        }

        foreach (var r in results)
        {
            r.ActivityPercent = maxSlotAmount > 0 ? Math.Max(12.0, (double)(r.TotalAmount / maxSlotAmount) * 100.0) : 15.0;
        }

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            HourlyActivities.Clear();
            foreach (var r in results)
            {
                HourlyActivities.Add(r);
            }
        });
    }
}
