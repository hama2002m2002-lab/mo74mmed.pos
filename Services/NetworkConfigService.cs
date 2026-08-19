using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace HamoPos.Services;

public class NetworkConfigModel
{
    public bool IsServerMode { get; set; } = true; // True = Master/Server, False = Client/Cashier
    public string ServerIpOrPath { get; set; } = ""; // e.g. "192.168.1.50" or "\\192.168.1.50\HamoPosData"
    public string SharedDatabasePath { get; set; } = "";
    public string DeviceName { get; set; } = Environment.MachineName;
    public string UpdateServerUrl { get; set; } = "https://raw.githubusercontent.com/hama2002m2002-lab/mo74mmed.pos/main/update.xml";
}

public class NetworkConfigService
{
    private static readonly Lazy<NetworkConfigService> _instance = new(() => new NetworkConfigService());
    public static NetworkConfigService Instance => _instance.Value;

    private readonly string _configFilePath;
    public NetworkConfigModel Config { get; private set; }

    public NetworkConfigService()
    {
        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "network_config.json");
        Config = LoadConfig();
    }

    public NetworkConfigModel LoadConfig()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                string json = File.ReadAllText(_configFilePath);
                var loaded = JsonSerializer.Deserialize<NetworkConfigModel>(json);
                if (loaded != null) return loaded;
            }
        }
        catch { }

        return new NetworkConfigModel();
    }

    public void SaveConfig(NetworkConfigModel newConfig)
    {
        Config = newConfig;
        try
        {
            string json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save network config: {ex.Message}");
        }
    }

    public string GetEffectiveDatabasePath()
    {
        if (Config.IsServerMode || string.IsNullOrWhiteSpace(Config.SharedDatabasePath))
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pos_data.db");
        }

        return Config.SharedDatabasePath;
    }

    public static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                return endPoint.Address.ToString();
            }
        }
        catch
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
        }

        return "127.0.0.1";
    }

    public (bool Success, string Message) TestDatabaseConnection(string dbPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                return (false, "يرجى تحديد مسار قاعدة البيانات المشتركة");

            if (!File.Exists(dbPath))
                return (false, $"الملف غير موجود في المسار المحدد: {dbPath}");

            string connStr = $"Data Source={dbPath};Mode=ReadWrite;Cache=Shared;Busy Timeout=5000;";
            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Products;";
                var result = cmd.ExecuteScalar();
                return (true, $"تم الاتصال بنجاح بقاعدة البيانات! إجمالي المواد المسجلة: {result}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"فشل الاتصال: {ex.Message}");
        }
    }
}
