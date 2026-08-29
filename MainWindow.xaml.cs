using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using HamoPos.Data;
using HamoPos.Services;

namespace HamoPos;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var currentAsmVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        string installedVer = currentAsmVer != null ? $"{currentAsmVer.Major}.{currentAsmVer.Minor}.{currentAsmVer.Build}" : "1.7.5";
        Title = $"7amo.pos PRO v{installedVer} - نظام إدارة المبيعات والمخازن فائق السرعة";

        Loaded += async (s, e) =>
        {
            try
            {
                using var db = new AppDbContext();
                await DbInitializer.InitializeAsync(db);

                // Start local Rep portal server in background
                RepWebPortalService.Instance.Start();

                // Start Cloud background sync (every 5 seconds)
                CloudSyncService.Instance.StartBackgroundSync(5);

                // Auto update check (silent background check)
                _ = Task.Run(() =>
                {
                    try
                    {
                        UpdateService.Instance.CheckForUpdates(false);
                    }
                    catch { }
                });

                await posWebView.EnsureCoreWebView2Async();

                // Event listener to notify UI when a rep submits an order
                void NotifyNewOrderToUi()
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var evt = new { _event = "new_order_received", timestamp = DateTime.Now.ToString("o") };
                            posWebView?.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(evt));
                        }
                        catch { }
                    });
                }

                CloudSyncService.Instance.CloudOrdersImported += NotifyNewOrderToUi;
                RepWebPortalService.Instance.OrderReceived += NotifyNewOrderToUi;

                posWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                posWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;

                // Load Web UI from wwwroot (Ensure the latest version is loaded)
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "index.html");
                string devPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");

                if (File.Exists(htmlPath))
                {
                    posWebView.Source = new Uri(htmlPath);
                }
                else if (File.Exists(devPath))
                {
                    posWebView.Source = new Uri(devPath);
                }

                // Setup Bi-directional C# Native Bridge
                posWebView.WebMessageReceived += async (sender, args) =>
                {
                    try
                    {
                        string json = args.WebMessageAsJson;
                        using var doc = JsonDocument.Parse(json);
                        string action = doc.RootElement.GetProperty("action").GetString() ?? "";
                        string payload = doc.RootElement.TryGetProperty("payload", out var pProp) ? pProp.GetString() ?? "{}" : "{}";
                        string cbId = doc.RootElement.TryGetProperty("_callbackId", out var cbProp) ? cbProp.GetString() ?? "" : "";

                        string resultJson = await PosBridgeService.Instance.HandleMessageAsync(action, payload);

                        var responsePayload = new
                        {
                            _callbackId = cbId,
                            result = JsonDocument.Parse(resultJson).RootElement
                        };

                        posWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(responsePayload));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebMessage] Error: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تشغيل واجهة المستخدم: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
    }
}