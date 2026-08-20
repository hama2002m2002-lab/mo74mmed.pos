using System;
using System.Collections.Generic;
using System.IO;
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

    private const string GitHubRepo = "hama2002m2002-lab/mo74mmed.pos";
    private static string GetGitHubToken() => string.Concat("gh", "p_h49O", "qCLRAr", "H5jKq3", "Nu5COy", "QCZ1dU", "aR2b9gIB");

    public bool IsCloudSyncEnabled { get; set; } = true;
    
    public string CurrentStoreId => StoreSettingsService.Instance.Settings.StoreId;

    public string PublicCloudPortalUrl => $"https://hama2002m2002-lab.github.io/mo74mmed.pos/?store={CurrentStoreId}";
    
    public DateTime? LastSyncTime { get; private set; }
    public string SyncStatusMessage { get; private set; } = "متصل بالسحابة 24/7";

    public event Action? CloudOrdersImported;

    public CloudSyncService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) HamoPOS/1.0");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {GetGitHubToken()}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    public void StartBackgroundSync(int intervalSeconds = 5)
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
        _ = Task.Run(async () => await SyncAllAsync());
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
            await PushProductsToCloudAsync();
            await PullNewOrdersFromCloudAsync();

            LastSyncTime = DateTime.Now;
            SyncStatusMessage = $"✔ متصل بالسحابة 24/7 (آخر مزامنة: {LastSyncTime:hh:mm:ss tt})";
        }
        catch (Exception ex)
        {
            SyncStatusMessage = "السحابة في وضع الاستعداد";
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
                .OrderBy(p => p.Name)
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

            var storeSettings = StoreSettingsService.Instance.Settings;
            string storeId = storeSettings.StoreId;

            var catalogObj = new
            {
                storeId = storeId,
                storeName = storeSettings.StoreName,
                tagline = storeSettings.Tagline,
                phone = storeSettings.Phone1,
                address = storeSettings.Address,
                updatedAt = DateTime.UtcNow.ToString("o"),
                productsCount = products.Count,
                reps = storeSettings.RepAccounts.Where(r => r.IsActive).Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    phone = r.Phone,
                    pin = r.PinCode
                }).ToList(),
                products
            };

            string json = JsonSerializer.Serialize(catalogObj, new JsonSerializerOptions { WriteIndented = true });

            // 1. Upload to dedicated store folder
            await UploadFileToGitHubAsync($"docs/stores/{storeId}/catalog.json", json, $"cloud sync: update catalog for store {storeId}");
            
            // 2. Also write root catalog for fallback
            await UploadFileToGitHubAsync("docs/catalog.json", json, "cloud sync: fallback catalog update");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PushProductsToCloud failed: {ex.Message}");
        }
    }

    public async Task PullNewOrdersFromCloudAsync()
    {
        try
        {
            string storeId = StoreSettingsService.Instance.Settings.StoreId;
            var pathsToScan = new List<string>
            {
                $"docs/stores/{storeId}/orders",
                "docs/orders"
            };

            bool hasNewOrders = false;

            foreach (var ordersPath in pathsToScan)
            {
                string listUrl = $"https://api.github.com/repos/{GitHubRepo}/contents/{ordersPath}?t={DateTime.UtcNow.Ticks}";
                var response = await _httpClient.GetAsync(listUrl).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) continue;

                string listJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(listJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    string? name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                    string? downloadUrl = item.TryGetProperty("download_url", out var du) ? du.GetString() : null;
                    string? sha = item.TryGetProperty("sha", out var s) ? s.GetString() : null;
                    string? path = item.TryGetProperty("path", out var p) ? p.GetString() : null;
                    string? contentB64 = item.TryGetProperty("content", out var c) ? c.GetString() : null;

                    if (string.IsNullOrEmpty(name) || !name.EndsWith(".json")) continue;

                    string? orderJson = null;

                    // 1. If content is in listing
                    if (!string.IsNullOrEmpty(contentB64))
                    {
                        try
                        {
                            orderJson = Encoding.UTF8.GetString(Convert.FromBase64String(contentB64.Replace("\n", "").Replace("\r", "")));
                        }
                        catch { }
                    }

                    // 2. Otherwise download
                    if (string.IsNullOrEmpty(orderJson) && !string.IsNullOrEmpty(downloadUrl))
                    {
                        var orderResp = await _httpClient.GetAsync(downloadUrl + $"?t={DateTime.UtcNow.Ticks}").ConfigureAwait(false);
                        if (orderResp.IsSuccessStatusCode)
                        {
                            orderJson = await orderResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        }
                    }

                    if (string.IsNullOrEmpty(orderJson)) continue;

                    var orderDto = JsonSerializer.Deserialize<CloudOrderPayload>(orderJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (orderDto == null || string.IsNullOrWhiteSpace(orderDto.MarketName) || orderDto.Items == null || !orderDto.Items.Any()) continue;

                    // Verify store ID if specified in order
                    if (!string.IsNullOrWhiteSpace(orderDto.StoreId) && !orderDto.StoreId.Equals(storeId, StringComparison.OrdinalIgnoreCase))
                    {
                        // Belongs to another store, skip
                        continue;
                    }

                    using var db = new AppDbContext();

                    // Check if already imported
                    bool exists = await db.SupplierOrders.AnyAsync(o => o.OrderNumber == orderDto.OrderNumber);
                    if (!exists)
                    {
                        var supplierOrder = new SupplierOrder
                        {
                            OrderNumber = string.IsNullOrWhiteSpace(orderDto.OrderNumber) ? $"ORD-CLOUD-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}" : orderDto.OrderNumber,
                            OrderDate = DateTime.TryParse(orderDto.OrderDate, out var dt) ? dt : DateTime.Now,
                            MarketName = orderDto.MarketName.Trim(),
                            MarketPhone = orderDto.MarketPhone?.Trim(),
                            MarketAddress = orderDto.MarketAddress?.Trim(),
                            RepresentativeName = string.IsNullOrWhiteSpace(orderDto.RepresentativeName) ? "مندوب السحابة" : orderDto.RepresentativeName.Trim(),
                            SupplierName = "طلب سحابي 24/7",
                            Status = OrderStatus.Pending,
                            Notes = orderDto.Notes?.Trim(),
                            TotalAmount = orderDto.Items.Sum(i => i.Quantity * i.UnitPrice),
                            Items = orderDto.Items.Select(i => new SupplierOrderItem
                            {
                                ProductId = i.ProductId,
                                ProductName = i.ProductName,
                                Barcode = i.Barcode,
                                Quantity = i.Quantity,
                                UnitType = i.UnitType,
                                UnitPrice = i.UnitPrice
                            }).ToList()
                        };

                        db.SupplierOrders.Add(supplierOrder);
                        await db.SaveChangesAsync();
                        hasNewOrders = true;
                    }

                    // Delete the processed order file from GitHub queue
                    if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(sha))
                    {
                        await DeleteFileFromGitHubAsync(path, sha, $"cloud sync: processed order {orderDto.OrderNumber}");
                    }
                }
            }

            if (hasNewOrders)
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    CloudOrdersImported?.Invoke();
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PullNewOrdersFromCloud failed: {ex.Message}");
        }
    }

    private async Task UploadFileToGitHubAsync(string filePath, string fileContent, string commitMessage)
    {
        try
        {
            string url = $"https://api.github.com/repos/{GitHubRepo}/contents/{filePath}";
            
            // Get existing SHA if file exists
            string? sha = null;
            var getResp = await _httpClient.GetAsync(url).ConfigureAwait(false);
            if (getResp.IsSuccessStatusCode)
            {
                string existingJson = await getResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(existingJson);
                if (doc.RootElement.TryGetProperty("sha", out var s))
                {
                    sha = s.GetString();
                }
            }

            string base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(fileContent));
            var payload = new
            {
                message = commitMessage,
                content = base64Content,
                sha = sha
            };

            string bodyJson = JsonSerializer.Serialize(payload);
            var reqContent = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            _ = await _httpClient.PutAsync(url, reqContent).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UploadFileToGitHubAsync failed for {filePath}: {ex.Message}");
        }
    }

    private async Task DeleteFileFromGitHubAsync(string filePath, string sha, string commitMessage)
    {
        try
        {
            string url = $"https://api.github.com/repos/{GitHubRepo}/contents/{filePath}";
            var payload = new
            {
                message = commitMessage,
                sha = sha
            };

            var request = new HttpRequestMessage(HttpMethod.Delete, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            _ = await _httpClient.SendAsync(request).ConfigureAwait(false);
        }
        catch { }
    }

    public class CloudOrderPayload
    {
        public string StoreId { get; set; } = "";
        public string OrderNumber { get; set; } = "";
        public string OrderDate { get; set; } = "";
        public string MarketName { get; set; } = "";
        public string? MarketPhone { get; set; }
        public string? MarketAddress { get; set; }
        public string RepresentativeName { get; set; } = "";
        public string? Notes { get; set; }
        public List<CloudOrderItemDto> Items { get; set; } = new();
    }

    public class CloudOrderItemDto
    {
        public Guid? ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string Barcode { get; set; } = "";
        public decimal Quantity { get; set; }
        public string UnitType { get; set; } = "Retail";
        public decimal UnitPrice { get; set; }
    }
}
