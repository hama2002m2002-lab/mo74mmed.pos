using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class NetworkSettingsViewModel : BaseViewModel
{
    private readonly NetworkConfigService _networkService;

    private bool _isServerMode;
    public bool IsServerMode
    {
        get => _isServerMode;
        set
        {
            if (SetProperty(ref _isServerMode, value))
            {
                OnPropertyChanged(nameof(IsClientMode));
                OnPropertyChanged(nameof(ModeDescription));
                OnPropertyChanged(nameof(StatusBadgeText));
            }
        }
    }

    public bool IsClientMode
    {
        get => !IsServerMode;
        set => IsServerMode = !value;
    }

    private string _sharedDatabasePath = "";
    public string SharedDatabasePath
    {
        get => _sharedDatabasePath;
        set => SetProperty(ref _sharedDatabasePath, value);
    }

    private string _serverIp = "";
    public string ServerIp
    {
        get => _serverIp;
        set => SetProperty(ref _serverIp, value);
    }

    private string _localIp = "";
    public string LocalIp
    {
        get => _localIp;
        set => SetProperty(ref _localIp, value);
    }

    private string _testStatusMessage = "";
    public string TestStatusMessage
    {
        get => _testStatusMessage;
        set => SetProperty(ref _testStatusMessage, value);
    }

    private bool? _isConnectionSuccessful = null;
    public bool? IsConnectionSuccessful
    {
        get => _isConnectionSuccessful;
        set => SetProperty(ref _isConnectionSuccessful, value);
    }

    public string ModeDescription => IsServerMode
        ? "هذا الجهاز يعمل كـ (سيرفر رئيسي للمدير). يستضيف قاعدة البيانات ويشاركها مع أجهزة الكاشير على نفس شبكة الواي فاي."
        : "هذا الجهاز يعمل كـ (كاشير فرعي). يتصل بقاعدة بيانات السيرفر الرئيسي عبر مسار الشبكة المشترك.";

    public string StatusBadgeText => IsServerMode ? "🖥️ جهاز رئيسي (سيرفر)" : "💻 جهاز فرعي (كاشير)";

    public ICommand TestConnectionCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand BrowseDatabasePathCommand { get; }
    public ICommand CheckUpdatesCommand { get; }
    public ICommand RollbackPreviousVersionCommand { get; }
    public ICommand CopyIpCommand { get; }

    public event Action? RequestClose;

    public NetworkSettingsViewModel()
    {
        _networkService = NetworkConfigService.Instance;
        
        var config = _networkService.Config;
        _isServerMode = config.IsServerMode;
        _sharedDatabasePath = config.SharedDatabasePath;
        _serverIp = config.ServerIpOrPath;
        _localIp = NetworkConfigService.GetLocalIpAddress();

        TestConnectionCommand = new RelayCommand(ExecuteTestConnection);
        SaveSettingsCommand = new RelayCommand(ExecuteSaveSettings);
        BrowseDatabasePathCommand = new RelayCommand(ExecuteBrowseDatabasePath);
        CheckUpdatesCommand = new RelayCommand(ExecuteCheckUpdates);
        RollbackPreviousVersionCommand = new AsyncRelayCommand(async () => await UpdateService.Instance.RollbackToPreviousVersionAsync());
        CopyIpCommand = new RelayCommand(ExecuteCopyIp);
    }

    private void ExecuteBrowseDatabasePath()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "قاعدة بيانات SQLite (*.db;*.sqlite)|*.db;*.sqlite|كافة الملفات (*.*)|*.*",
            Title = "اختر ملف قاعدة بيانات السيرفر الرئيسي المشترك"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            SharedDatabasePath = openFileDialog.FileName;
        }
    }

    private void ExecuteTestConnection()
    {
        string path = IsServerMode 
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pos_data.db") 
            : SharedDatabasePath;

        var result = _networkService.TestDatabaseConnection(path);
        IsConnectionSuccessful = result.Success;
        TestStatusMessage = result.Message;
    }

    private void ExecuteSaveSettings()
    {
        var config = _networkService.Config;
        config.IsServerMode = IsServerMode;
        config.SharedDatabasePath = SharedDatabasePath;
        config.ServerIpOrPath = ServerIp;

        _networkService.SaveConfig(config);

        MessageBox.Show(
            "تم حفظ إعدادات الشبكة بنجاح!\nيرجى إعادة تشغيل البرنامج لتطبيق مسار قاعدة البيانات الجديد.",
            "تم الحفظ بنجاح",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        RequestClose?.Invoke();
    }

    private void ExecuteCheckUpdates()
    {
        UpdateService.Instance.CheckForUpdates(isManual: true);
    }

    private void ExecuteCopyIp()
    {
        try
        {
            Clipboard.SetText(LocalIp);
            MessageBox.Show($"تم نسخ عنوان IP ({LocalIp}) إلى الحافظة!", "تم النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch { }
    }
}
