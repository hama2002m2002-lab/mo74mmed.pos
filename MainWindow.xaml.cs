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

        Loaded += async (s, e) =>
        {
            try
            {
                using var db = new AppDbContext();
                await DbInitializer.InitializeAsync(db);

                // Start local Rep portal server in background
                RepWebPortalService.Instance.Start();

                // Auto update check
                _ = Task.Run(() =>
                {
                    try
                    {
                        var updateService = new UpdateService();
                        updateService.CheckForUpdates();
                    }
                    catch { }
                });

                await posWebView.EnsureCoreWebView2Async();

                // Configure WebView settings
                posWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                posWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;

                // Load Web UI
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "index.html");
                if (File.Exists(htmlPath))
                {
                    posWebView.Source = new Uri(htmlPath);
                }
                else
                {
                    string devPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
                    if (File.Exists(devPath))
                    {
                        posWebView.Source = new Uri(devPath);
                    }
                }

                // Setup Bi-directional C# Bridge
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