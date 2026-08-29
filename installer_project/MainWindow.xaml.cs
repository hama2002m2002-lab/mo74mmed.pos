using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace HamoPos.Installer;

public partial class MainWindow : Window
{
    private string _targetDirectory = @"C:\HamoPos";

    public MainWindow()
    {
        InitializeComponent();
        _targetDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7amo POS");
        if (string.IsNullOrWhiteSpace(_targetDirectory))
        {
            _targetDirectory = @"C:\HamoPos";
        }
        TxtInstallPath.Text = _targetDirectory;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "اختر مجلد تثبيت البرنامج:",
            SelectedPath = TxtInstallPath.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtInstallPath.Text = dialog.SelectedPath;
        }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        _targetDirectory = TxtInstallPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(_targetDirectory))
        {
            MessageBox.Show("يرجى تحديد مسار تثبيت صالح.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConfigPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        BtnInstall.Visibility = Visibility.Collapsed;
        BtnCancel.IsEnabled = false;

        bool createDesktopShortcut = ChkDesktopShortcut.IsChecked == true;
        bool createStartMenuShortcut = ChkStartMenuShortcut.IsChecked == true;

        bool success = await Task.Run(() => PerformInstallation(_targetDirectory, createDesktopShortcut, createStartMenuShortcut));

        ProgressPanel.Visibility = Visibility.Collapsed;
        BtnCancel.IsEnabled = true;

        if (success)
        {
            SuccessPanel.Visibility = Visibility.Visible;
            BtnFinish.Visibility = Visibility.Visible;
            BtnCancel.Visibility = Visibility.Collapsed;
        }
        else
        {
            ConfigPanel.Visibility = Visibility.Visible;
            BtnInstall.Visibility = Visibility.Visible;
        }
    }

    private bool PerformInstallation(string targetDir, bool desktopShortcut, bool startMenuShortcut)
    {
        try
        {
            // 0. Automatically close running instances of 7amo.pos to release file locks
            try
            {
                foreach (var proc in Process.GetProcessesByName("7amo.pos"))
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
            }
            catch { }

            // 1. Create target directory
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            else
            {
                // Safety Backup: If old database exists, backup it with timestamp before updating!
                string existingDb = Path.Combine(targetDir, "pos_data.db");
                if (File.Exists(existingDb))
                {
                    try
                    {
                        string backupFolder = Path.Combine(targetDir, "Backups");
                        Directory.CreateDirectory(backupFolder);
                        string backupFile = Path.Combine(backupFolder, $"pos_data_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                        File.Copy(existingDb, backupFile, overwrite: true);
                    }
                    catch { }
                }
            }

            // 2. Extract embedded payload.zip safely (NEVER overwrite user database or config)
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("payload.zip");
            if (stream == null)
            {
                // Fallback: look for payload.zip next to installer
                string nextToExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload.zip");
                if (File.Exists(nextToExe))
                {
                    using var archive = ZipFile.OpenRead(nextToExe);
                    SafeExtractArchive(archive, targetDir);
                }
                else
                {
                    Dispatcher.Invoke(() => MessageBox.Show("تعذر العثور على حزمة ملفات التثبيت.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error));
                    return false;
                }
            }
            else
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                SafeExtractArchive(archive, targetDir);
            }

            string exePath = Path.Combine(targetDir, "7amo.pos.exe");

            // 3. Create Desktop Shortcut
            if (desktopShortcut)
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                CreateWindowsShortcut(Path.Combine(desktopPath, "7amo POS - نظام نقاط البيع.lnk"), exePath, targetDir);
                
                // OneDrive Desktop fallback
                string oneDriveDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop");
                if (Directory.Exists(oneDriveDesktop))
                {
                    CreateWindowsShortcut(Path.Combine(oneDriveDesktop, "7amo POS - نظام نقاط البيع.lnk"), exePath, targetDir);
                }
            }

            // 4. Create Start Menu Shortcut
            if (startMenuShortcut)
            {
                string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "7amo POS");
                if (!Directory.Exists(startMenuPath))
                {
                    Directory.CreateDirectory(startMenuPath);
                }
                CreateWindowsShortcut(Path.Combine(startMenuPath, "7amo POS.lnk"), exePath, targetDir);
            }

            return true;
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => MessageBox.Show($"حدث خطأ أثناء التثبيت: {ex.Message}", "خطأ في التثبيت", MessageBoxButton.OK, MessageBoxImage.Error));
            return false;
        }
    }

    private void CreateWindowsShortcut(string shortcutPath, string targetPath, string workingDir)
    {
        try
        {
            string psScript = $"$WshShell = New-Object -ComObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('{shortcutPath}'); $Shortcut.TargetPath = '{targetPath}'; $Shortcut.WorkingDirectory = '{workingDir}'; $Shortcut.Description = '7amo POS System'; $Shortcut.Save()";
            
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi)?.WaitForExit(5000);
        }
        catch { }
    }

    private void SafeExtractArchive(ZipArchive archive, string targetDir)
    {
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                string dirPath = Path.Combine(targetDir, entry.FullName);
                Directory.CreateDirectory(dirPath);
                continue;
            }

            string destinationPath = Path.Combine(targetDir, entry.FullName);
            string parentDir = Path.GetDirectoryName(destinationPath)!;
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            // CRITICAL: NEVER overwrite existing user database or local network configs
            if (File.Exists(destinationPath))
            {
                string fileName = Path.GetFileName(destinationPath);
                if (fileName.Equals("pos_data.db", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("pos_data.db-wal", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("pos_data.db-shm", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("pos.db", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("network_config.json", StringComparison.OrdinalIgnoreCase))
                {
                    // Preserve existing user data completely intact!
                    continue;
                }
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        if (ChkLaunchAfter.IsChecked == true)
        {
            string exePath = Path.Combine(_targetDirectory, "7amo.pos.exe");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = _targetDirectory,
                    UseShellExecute = true
                });
            }
        }

        this.Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
