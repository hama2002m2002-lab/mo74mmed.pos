using System;
using System.IO;
using System.Text.Json;

namespace HamoPos.Services;

public class StoreSettings
{
    public string StoreName { get; set; } = "مخزن ومبيعات 7amo POS";
    public string Tagline { get; set; } = "لتجارة المواد الغذائية والسلع العامة بالجملة والمفرد";
    public string Phone1 { get; set; } = "0770 000 0000";
    public string Phone2 { get; set; } = "0780 000 0000";
    public string Address { get; set; } = "العراق - بغداد / شارع التجارة";
    public string LogoPath { get; set; } = "";
    public string ReceiptFooterNotes { get; set; } = "شكراً لتعاملكم معنا - يرجى فحص البضاعة عند الاستلام";
    public string A4TermsAndConditions { get; set; } = "البضاعة المباعة لا تُرد ولا تُستبدل إلا بموجب هذا الوصل وخلال 48 ساعة من تاريخ الاستلام.";
    public bool AutoPrintA4OnDelivery { get; set; } = true;
}

public class StoreSettingsService
{
    private static readonly Lazy<StoreSettingsService> _instance = new(() => new StoreSettingsService());
    public static StoreSettingsService Instance => _instance.Value;

    private readonly string _settingsFilePath;
    public StoreSettings Settings { get; private set; }

    public event Action? SettingsChanged;

    public StoreSettingsService()
    {
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "7amoPos");
        if (!Directory.Exists(appData))
        {
            Directory.CreateDirectory(appData);
        }
        _settingsFilePath = Path.Combine(appData, "store_settings.json");
        Settings = LoadSettings();
    }

    public StoreSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<StoreSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch { }

        return new StoreSettings();
    }

    public void SaveSettings(StoreSettings settings)
    {
        try
        {
            Settings = settings;
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
            SettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving store settings: {ex.Message}");
        }
    }
}
