using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;

namespace HamoPos.Services;

public class CloudSyncService
{
    private static readonly Lazy<CloudSyncService> _instance = new(() => new CloudSyncService());
    public static CloudSyncService Instance => _instance.Value;

    private readonly HttpClient _httpClient;
    private DispatcherTimer? _syncTimer;
    private bool _isSyncing = false;

    public bool IsCloudSyncEnabled { get; set; } = true;
    public string CloudApiEndpoint { get; set; } = "https://hamopos-cloud-api.vercel.app/api"; // Configurable cloud hub
    public DateTime? LastSyncTime { get; private set; }
    public string SyncStatusMessage { get; private set; } = "جاهز للمزامنة السحابية 24/7";

    public event Action? CloudOrdersImported;

    public CloudSyncService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public void StartBackgroundSync(int intervalSeconds = 30)
    {
        if (_syncTimer != null)
        {
            _syncTimer.Stop();
        }

        _syncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(intervalSeconds)
        };
        _syncTimer.Tick += async (s, e) => await SyncAllAsync();
        _syncTimer.Start();

        // Initial sync on start
        _ = SyncAllAsync();
    }

    public void StopBackgroundSync()
    {
        _syncTimer?.Stop();
    }

    public async Task SyncAllAsync()
    {
        if (_isSyncing || !IsCloudSyncEnabled) return;
        _isSyncing = true;

        try
        {
            SyncStatusMessage = "جارٍ مزامنة المخزون والطلبيات مع السحابة...";
            await PushProductsToCloudAsync();
            await PullNewOrdersFromCloudAsync();

            LastSyncTime = DateTime.Now;
            SyncStatusMessage = $"✔ متصل بالسحابة 24/7 (آخر مزامنة: {LastSyncTime:hh:mm:ss tt})";
        }
        catch (Exception ex)
        {
            SyncStatusMessage = "السحابة في وضع الاستعداد (Local Sync Active)";
            System.Diagnostics.Debug.WriteLine($"Cloud sync note: {ex.Message}");
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public async Task PushProductsToCloudAsync()
    {
        try
        {
            using var db = new AppDbContext();
            var products = await db.Products
                .Where(p => p.IsActive)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    barcode = p.Barcode,
                    price = p.Price,
                    wholesalePrice = p.WholesalePrice,
                    cartonPrice = p.CartonSellingPrice,
                    stock = p.StockQuantity,
                    unit = p.Unit,
                    itemsPerCarton = p.ItemsPerCarton
                })
                .ToListAsync();

            // Store / Cache local catalog in cloud sync payload
            string json = JsonSerializer.Serialize(new { products, timestamp = DateTime.UtcNow });
            
            // If custom cloud endpoint is provided, push via HTTP
            if (!string.IsNullOrWhiteSpace(CloudApiEndpoint) && CloudApiEndpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _ = await _httpClient.PostAsync($"{CloudApiEndpoint}/catalog", content).ConfigureAwait(false);
            }
        }
        catch { }
    }

    public async Task PullNewOrdersFromCloudAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CloudApiEndpoint) || !CloudApiEndpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return;

            var response = await _httpClient.GetAsync($"{CloudApiEndpoint}/orders/pending").ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return;

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var cloudOrders = JsonSerializer.Deserialize<List<RepWebPortalService.CreateOrderDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (cloudOrders == null || !cloudOrders.Any()) return;

            using var db = new AppDbContext();
            bool hasNew = false;

            foreach (var dto in cloudOrders)
            {
                string orderNum = $"ORD-CLOUD-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";
                
                // Check duplicate
                bool exists = await db.SupplierOrders.AnyAsync(o => o.MarketName == dto.MarketName && o.RepresentativeName == dto.RepresentativeName && o.OrderDate > DateTime.Today);
                if (exists) continue;

                var order = new SupplierOrder
                {
                    OrderNumber = orderNum,
                    OrderDate = DateTime.Now,
                    MarketName = dto.MarketName.Trim(),
                    MarketPhone = dto.MarketPhone,
                    MarketAddress = dto.MarketAddress,
                    RepresentativeName = string.IsNullOrWhiteSpace(dto.RepresentativeName) ? "مندوب السحابة" : dto.RepresentativeName,
                    SupplierName = "مندوب 24/7",
                    Status = OrderStatus.Pending,
                    Notes = dto.Notes,
                    TotalAmount = dto.Items.Sum(i => i.Quantity * i.UnitPrice),
                    Items = dto.Items.Select(i => new SupplierOrderItem
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Barcode = i.Barcode,
                        Quantity = i.Quantity,
                        UnitType = i.UnitType,
                        UnitPrice = i.UnitPrice
                    }).ToList()
                };

                db.SupplierOrders.Add(order);
                hasNew = true;
            }

            if (hasNew)
            {
                await db.SaveChangesAsync();
                CloudOrdersImported?.Invoke();
            }
        }
        catch { }
    }
}
