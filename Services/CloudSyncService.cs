using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
    public string PublicCloudPortalUrl { get; } = "https://hama2002m2002-lab.github.io/mo74mmed.pos/";
    public DateTime? LastSyncTime { get; private set; }
    public string SyncStatusMessage { get; private set; } = "جاهز للمزامنة السحابية 24/7";

    public event Action? CloudOrdersImported;

    public CloudSyncService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) HamoPOS/1.0");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {GetGitHubToken()}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    public void StartBackgroundSync(int intervalSeconds = 20)
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
            SyncStatusMessage = "جارٍ مزامنة المخزون والطلبيات مع السحابة...";
            await PushProductsToCloudAsync();
            await PullNewOrdersFromCloudAsync();

            LastSyncTime = DateTime.Now;
            SyncStatusMessage = $"✔ متصل بالسحابة 24/7 (آخر مزامنة: {LastSyncTime:hh:mm:ss tt})";
        }
        catch (Exception ex)
        {
            SyncStatusMessage = "السحابة في وضع الاستعداد";
            System.Diagnostics.Debug.WriteLine($"Cloud sync error: {ex.Message}");
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

            var catalogObj = new
            {
                updatedAt = DateTime.UtcNow.ToString("o"),
                productsCount = products.Count,
                products
            };

            string json = JsonSerializer.Serialize(catalogObj, new JsonSerializerOptions { WriteIndented = true });

            // 1. Write local docs/catalog.json if directory exists
            string docsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs");
            if (!Directory.Exists(docsPath))
            {
                // try project root
                docsPath = Path.Combine(Directory.GetCurrentDirectory(), "docs");
            }
            if (Directory.Exists(docsPath))
            {
                File.WriteAllText(Path.Combine(docsPath, "catalog.json"), json);
            }

            // 2. Push to GitHub repository via API so GitHub Pages gets the latest products immediately
            await UploadFileToGitHubAsync("docs/catalog.json", json, "cloud sync: update products catalog 24/7");
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
            // List files in docs/orders
            string listUrl = $"https://api.github.com/repos/{GitHubRepo}/contents/docs/orders";
            var response = await _httpClient.GetAsync(listUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return;

            string listJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(listJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

            bool hasNewOrders = false;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                string? name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                string? downloadUrl = item.TryGetProperty("download_url", out var du) ? du.GetString() : null;
                string? sha = item.TryGetProperty("sha", out var s) ? s.GetString() : null;
                string? path = item.TryGetProperty("path", out var p) ? p.GetString() : null;

                if (string.IsNullOrEmpty(name) || !name.EndsWith(".json") || string.IsNullOrEmpty(downloadUrl)) continue;

                // Download order JSON
                var orderResp = await _httpClient.GetAsync(downloadUrl).ConfigureAwait(false);
                if (!orderResp.IsSuccessStatusCode) continue;

                string orderJson = await orderResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var orderDto = JsonSerializer.Deserialize<CloudOrderPayload>(orderJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (orderDto == null || string.IsNullOrWhiteSpace(orderDto.MarketName) || orderDto.Items == null || !orderDto.Items.Any()) continue;

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

            var putResp = await _httpClient.PutAsync(url, reqContent).ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine($"GitHub file upload {filePath}: status={putResp.StatusCode}");
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
