using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using AutoUpdaterDotNET;

namespace HamoPos.Services;

public class UpdateService
{
    private static readonly Lazy<UpdateService> _instance = new(() => new UpdateService());
    public static UpdateService Instance => _instance.Value;

    private bool _isCurrentCheckManual = false;

    public void CheckForUpdates(bool isManual = true)
    {
        _isCurrentCheckManual = isManual;
        _ = CheckForUpdatesAsync(isManual);
    }

    private async Task CheckForUpdatesAsync(bool isManual)
    {
        try
        {
            string updateUrl = NetworkConfigService.Instance.Config.UpdateServerUrl;
            if (string.IsNullOrWhiteSpace(updateUrl) || updateUrl.Contains("HamoPos/Releases"))
            {
                updateUrl = "https://raw.githubusercontent.com/hama2002m2002-lab/mo74mmed.pos/main/update.xml";
            }

            // 2. Check URL availability
            if (updateUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                updateUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
                try
                {
                    var response = await httpClient.GetAsync(updateUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        if (isManual)
                        {
                            var currentAsmVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                            string installedVer = currentAsmVer != null ? $"{currentAsmVer.Major}.{currentAsmVer.Minor}.{currentAsmVer.Build}" : "1.7.5";
                            MessageBox.Show(
                                $"✔ أنت تستخدم أحدث إصدار من البرنامج (v{installedVer}).\nلا توجد تحديثات جديدة منشورة حالياً على السحابة.",
                                "فحص التحديثات",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        return;
                    }
                }
                catch
                {
                    if (isManual)
                    {
                        var currentAsmVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                        string installedVer = currentAsmVer != null ? $"{currentAsmVer.Major}.{currentAsmVer.Minor}.{currentAsmVer.Build}" : "1.7.5";
                        MessageBox.Show(
                            $"✔ أنت تستخدم أحدث إصدار من البرنامج (v{installedVer}).",
                            "فحص التحديثات",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    return;
                }
            }

            // 3. Configure AutoUpdater
            AutoUpdater.ReportErrors = false;
            AutoUpdater.ShowSkipButton = false;
            AutoUpdater.ShowRemindLaterButton = true;
            AutoUpdater.Mandatory = false;
            AutoUpdater.UpdateMode = Mode.Normal;
            AutoUpdater.RunUpdateAsAdmin = false;
            AutoUpdater.OpenDownloadPage = false;
            AutoUpdater.Synchronous = false;

            AutoUpdater.CheckForUpdateEvent -= AutoUpdaterOnCheckForUpdateEvent;
            AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;

            AutoUpdater.Start(updateUrl);
        }
        catch
        {
            if (isManual)
            {
                var currentAsmVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string installedVer = currentAsmVer != null ? $"{currentAsmVer.Major}.{currentAsmVer.Minor}.{currentAsmVer.Build}" : "1.7.5";
                MessageBox.Show(
                    $"✔ أنت تستخدم أحدث إصدار من البرنامج (v{installedVer}).",
                    "فحص التحديثات",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
    {
        AutoUpdater.CheckForUpdateEvent -= AutoUpdaterOnCheckForUpdateEvent;

        if (Application.Current?.Dispatcher == null) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                var currentAsmVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string installedVer = currentAsmVer != null ? $"{currentAsmVer.Major}.{currentAsmVer.Minor}.{currentAsmVer.Build}" : "1.7.5";
                bool isKu = LocalizationManager.Instance.IsKurdish;

                if (args.Error == null)
                {
                    if (args.IsUpdateAvailable)
                    {
                        string msg = isKu
                            ? $"وەشانی نوێی بەرنامە بەردەستە ({args.CurrentVersion})!\nوەشانی ئێستای تۆ: ({installedVer})\n\nئایا دەتەوێت نوێکردنەوەی ڕاستەوخۆ دابەزێنیت و دابمەزرێنیت؟"
                            : $"يوجد إصدار جديد متوفر من البرنامج ({args.CurrentVersion})!\nالإصدار المثبت لديك حالياً: ({installedVer})\n\nهل ترغب في تنزيل أحدث إصدار وتثبيته مباشرة الآن بنقرة واحدة؟";

                        string title = isKu ? "نوێکردنەوەی نوێ بەردەستە 🚀" : "تحديث جديد متوفر 🚀";

                        var dialogResult = MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Information);

                        if (dialogResult == MessageBoxResult.Yes)
                        {
                            try
                            {
                                if (AutoUpdater.DownloadUpdate(args))
                                {
                                    Application.Current.Shutdown();
                                }
                            }
                            catch (Exception exception)
                            {
                                MessageBox.Show(exception.Message, isKu ? "هەڵە لە نوێکردنەوە" : "خطأ في التحديث", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                    else if (_isCurrentCheckManual)
                    {
                        string msg = isKu
                            ? $"تۆ نوێترین وەشانی فەرمیی بەرنامەت بەکارهێناوە (v{installedVer}) ✔"
                            : $"أنت تستخدم أحدث إصدار من البرنامج بالفعل (v{installedVer}) ✔";

                        MessageBox.Show(msg, isKu ? "پشکنینی نوێکردنەوە" : "فحص التحديثات", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else if (_isCurrentCheckManual)
                {
                    string msg = isKu
                        ? $"تۆ نوێترین وەشانی بەرنامەت بەکارهێناوە (v{installedVer}) ✔"
                        : $"أنت تستخدم أحدث إصدار من البرنامج بالفعل (v{installedVer}) ✔";

                    MessageBox.Show(msg, isKu ? "پشکنینی نوێکردنەوە" : "فحص التحديثات", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch { }
        });
    }

    public async Task RollbackToPreviousVersionAsync()
    {
        bool isKu = LocalizationManager.Instance.IsKurdish;
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "7amoPOS-Updater");

            string relUrl = "https://api.github.com/repos/hama2002m2002-lab/mo74mmed.pos/releases";
            var response = await httpClient.GetAsync(relUrl);
            if (!response.IsSuccessStatusCode)
            {
                string msgErr = isKu 
                    ? "نەتوانرا پەیوەندی بە سێرڤەری گەڕانەوە بکرێت. تکایە دڵنیابەرەوە لە هەبوونی ئینتەرنێت."
                    : "تعذر الاتصال بخادم الاسترجاع. يرجى التأكد من اتصال الإنترنت والمحاولة مجدداً.";
                MessageBox.Show(msgErr, isKu ? "هەڵە" : "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Array || root.GetArrayLength() < 2)
            {
                string msgNoPrev = isKu 
                    ? "هیچ وەشانێکی پێشوو نەدۆزرایەوە بۆ گەڕانەوە."
                    : "لا يوجد إصدار سابق متاح للرجوع إليه حالياً.";
                MessageBox.Show(msgNoPrev, isKu ? "ئاگاداری" : "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Current assembly version
            var currentAsmVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string currentTag = currentAsmVer != null ? $"v{currentAsmVer.Major}.{currentAsmVer.Minor}.{currentAsmVer.Build}" : "";

            // Find the release immediately preceding current version
            System.Text.Json.JsonElement targetRelease = default;
            bool foundCurrent = false;
            foreach (var release in root.EnumerateArray())
            {
                string tagName = release.GetProperty("tag_name").GetString() ?? "";
                if (!foundCurrent)
                {
                    if (string.Equals(tagName, currentTag, StringComparison.OrdinalIgnoreCase))
                    {
                        foundCurrent = true;
                    }
                    continue;
                }
                
                targetRelease = release;
                break;
            }

            if (targetRelease.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            {
                // If current tag wasn't matched exactly, take the second release in list
                targetRelease = root[1];
            }

            string prevTag = targetRelease.GetProperty("tag_name").GetString() ?? "الإصدار السابق";
            string downloadUrl = "";

            if (targetRelease.TryGetProperty("assets", out var assets) && assets.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (name.Equals("payload.zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                downloadUrl = $"https://github.com/hama2002m2002-lab/mo74mmed.pos/releases/download/{prevTag}/payload.zip";
            }

            string confirmMsg = isKu
                ? $"ئایا دڵنیایت لە گەڕانەوە بۆ وەشانی پێشوو ({prevTag}) بۆ دۆخی کتوپڕ؟\n\nسیستەم فایلەکانی وەشانی پێشوو دادەبەزێنێت و بەرنامە دادەخات بۆ دانانی فایلەکان، لەگەڵ پاراستنی تەواوی داتابەیسی فرۆشتن و کاڵاکان."
                : $"هل ترغب في الرجوع إلى الإصدار السابق ({prevTag}) لحالة الطوارئ؟\n\nسيتم تنزيل الإصدار السابق وتثبيته تلقائياً، مع الحفاظ الكامل على قاعدة بيانات المبيعات والمخزن.";

            var confirmResult = MessageBox.Show(confirmMsg, isKu ? "گەڕانەوە بۆ وەشانی پێشوو" : "تأكيد الرجوع للإصدار السابق", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes)
                return;

            string tempZipPath = Path.Combine(Path.GetTempPath(), "7amo_rollback_payload.zip");
            if (File.Exists(tempZipPath)) File.Delete(tempZipPath);

            var zipBytes = await httpClient.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(tempZipPath, zipBytes);

            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string batPath = Path.Combine(Path.GetTempPath(), "7amo_rollback_runner.bat");

            string batContent = $@"@echo off
chcp 65001 > nul
timeout /t 2 /nobreak > nul
taskkill /F /IM ""7amo.pos.exe"" > nul 2>&1
powershell -NoProfile -Command ""Expand-Archive -Path '{tempZipPath.Replace("'", "''")}' -DestinationPath '{appDir.Replace("'", "''")}' -Force""
start """" ""{Path.Combine(appDir, "7amo.pos.exe")}""
del ""{tempZipPath}"" > nul 2>&1
(goto) 2>nul & del ""%~f0""
";

            await File.WriteAllTextAsync(batPath, batContent);

            var procInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };
            System.Diagnostics.Process.Start(procInfo);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            string errMsg = isKu ? $"هەڵە لە کاتی گەڕانەوە: {ex.Message}" : $"حدث خطأ أثناء محاولة الرجوع للإصدار السابق: {ex.Message}";
            MessageBox.Show(errMsg, isKu ? "هەڵە" : "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
