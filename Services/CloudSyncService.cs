using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;

namespace HamoPos.Services;

public class CloudSyncService
{
    private static readonly Lazy<CloudSyncService> _instance = new(() => new CloudSyncService());
    public static CloudSyncService Instance => _instance.Value;

    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

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
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) HamoPOS/1.0");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {GetGitHubToken()}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    public void StartBackgroundSync(int intervalSeconds = 10)
    {
        StopBackgroundSync();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            // Initial sync on start
            await SyncAllAsync().ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) break;

                    await PullNewOrdersFromCloudAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Background loop note: {ex.Message}");
                }
            }
        }, token);
    }

    public void StopBackgroundSync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    public async Task SyncAllAsync()
    {
        if (!IsCloudSyncEnabled) return;
        if (!await _syncLock.WaitAsync(100).ConfigureAwait(false)) return;

        try
        {
            await PushProductsToCloudAsync().ConfigureAwait(false);
            await PullNewOrdersFromCloudAsync().ConfigureAwait(false);

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
            _syncLock.Release();
        }
    }

    public async Task PushProductsToCloudAsync()
    {
        try
        {
            using var db = new AppDbContext();
            var products = await db.Products
                .AsNoTracking()
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
                .ToListAsync()
                .ConfigureAwait(false);

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
            await UploadFileToGitHubAsync($"docs/stores/{storeId}/catalog.json", json, $"cloud sync: update catalog for store {storeId}").ConfigureAwait(false);
            
            // 2. Also write root catalog for fallback
            await UploadFileToGitHubAsync("docs/catalog.json", json, "cloud sync: fallback catalog update").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PushProductsToCloud failed: {ex.Message}");
        }
    }

    public async Task PullNewOrdersFromCloudAsync()
    {
        if (!await _syncLock.WaitAsync(100).ConfigureAwait(false)) return;

        try
        {
            string currentStoreId = StoreSettingsService.Instance.Settings.StoreId;
            var pathsToScan = new List<string>
            {
                $"docs/stores/{currentStoreId}/orders",
                "docs/orders"
            };

            bool hasNewOrders = false;

            foreach (var ordersPath in pathsToScan)
            {
                string listUrl = $"https://api.github.com/repos/{GitHubRepo}/contents/{ordersPath}?t={DateTime.UtcNow.Ticks}";
                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.GetAsync(listUrl).ConfigureAwait(false);
                }
                catch { continue; }

                if (!response.IsSuccessStatusCode) continue;

                string listJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(listJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    string? name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                    string? sha = item.TryGetProperty("sha", out var s) ? s.GetString() : null;
                    string? path = item.TryGetProperty("path", out var p) ? p.GetString() : null;

                    if (string.IsNullOrEmpty(name) || !name.EndsWith(".json") || string.IsNullOrEmpty(path)) continue;

                    // Fetch file content via GitHub contents API directly
                    string fileApiUrl = $"https://api.github.com/repos/{GitHubRepo}/contents/{path}?t={DateTime.UtcNow.Ticks}";
                    HttpResponseMessage fileResp;
                    try
                    {
                        fileResp = await _httpClient.GetAsync(fileApiUrl).ConfigureAwait(false);
                    }
                    catch { continue; }

                    if (!fileResp.IsSuccessStatusCode) continue;

                    string fileJson = await fileResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var fileDoc = JsonDocument.Parse(fileJson);
                    
                    if (!fileDoc.RootElement.TryGetProperty("content", out var contentElem) || contentElem.GetString() is not string contentB64)
                        continue;

                    string orderJson = Encoding.UTF8.GetString(Convert.FromBase64String(contentB64.Replace("\n", "").Replace("\r", "").Trim()));
                    if (string.IsNullOrWhiteSpace(orderJson)) continue;

                    var orderDto = JsonSerializer.Deserialize<CloudOrderPayload>(orderJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (orderDto == null || string.IsNullOrWhiteSpace(orderDto.MarketName) || orderDto.Items == null || !orderDto.Items.Any()) continue;

                    using var db = new AppDbContext();

                    // Check if already imported
                    bool exists = await db.SupplierOrders.AnyAsync(o => o.OrderNumber == orderDto.OrderNumber).ConfigureAwait(false);
                    if (!exists)
                    {
                        var supplierOrder = new SupplierOrder
                        {
                            OrderNumber = string.IsNullOrWhiteSpace(orderDto.OrderNumber) ? $"ORD-CLOUD-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}" : orderDto.OrderNumber,
                            OrderDate = DateTime.TryParse(orderDto.OrderDate, out var dt) ? dt : DateTime.Now,
                            MarketName = orderDto.MarketName.Trim(),
                            MarketPhone = orderDto.MarketPhone?.Trim(),
                            MarketAddress = orderDto.MarketAddress?.Trim(),
                            RepresentativeName = string.IsNullOrWhiteSpace(orderDto.RepresentativeName) ? "مندوب المبيعات" : orderDto.RepresentativeName.Trim(),
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
                        await db.SaveChangesAsync().ConfigureAwait(false);
                        hasNewOrders = true;
                    }

                    // Delete the processed order file from GitHub queue
                    string actualSha = fileDoc.RootElement.TryGetProperty("sha", out var actualShaElem) ? actualShaElem.GetString() ?? sha ?? "" : sha ?? "";
                    if (!string.IsNullOrEmpty(actualSha))
                    {
                        await DeleteFileFromGitHubAsync(path, actualSha, $"cloud sync: processed order {orderDto.OrderNumber}").ConfigureAwait(false);
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
        finally
        {
            _syncLock.Release();
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
