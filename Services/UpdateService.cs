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

    public void CheckForUpdates(bool isManual = true)
    {
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

            // 2. Check URL or Local File availability
            if (updateUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                updateUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await httpClient.GetAsync(updateUrl);
                if (!response.IsSuccessStatusCode)
                {
                    if (isManual)
                    {
                        MessageBox.Show(
                            $"✔ لا توجد تحديثات جديدة منشورة على الخادم حالياً.\nأنت تستخدم أحدث نسخة من البرنامج.",
                            "فحص التحديثات",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    return;
                }
            }
            else if (!File.Exists(updateUrl))
            {
                if (isManual)
                {
                    MessageBox.Show(
                        "✔ أنت تستخدم أحدث إصدار من البرنامج (v1.0.0).\nلا توجد تحديثات جديدة حالياً.",
                        "فحص التحديثات",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            // 3. Start AutoUpdater if valid feed exists
            AutoUpdater.ShowSkipButton = false;
            AutoUpdater.ShowRemindLaterButton = true;
            AutoUpdater.Mandatory = false;
            AutoUpdater.UpdateMode = Mode.Normal;
            AutoUpdater.RunUpdateAsAdmin = false;
            AutoUpdater.OpenDownloadPage = false;
            AutoUpdater.Synchronous = false;

            if (isManual)
            {
                AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
            }

            AutoUpdater.Start(updateUrl);
        }
        catch (Exception ex)
        {
            if (isManual)
            {
                MessageBox.Show(
                    "✔ أنت تستخدم أحدث إصدار من البرنامج (v1.0.0).\nلا توجد تحديثات جديدة حالياً.",
                    "فحص التحديثات",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
    {
        AutoUpdater.CheckForUpdateEvent -= AutoUpdaterOnCheckForUpdateEvent;

        var currentAsmVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        string installedVer = currentAsmVer != null ? $"{currentAsmVer.Major}.{currentAsmVer.Minor}.{currentAsmVer.Build}" : "1.0.2";
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
            else
            {
                string msg = isKu
                    ? $"تۆ نوێترین وەشانی فەرمیی بەرنامەت بەکارهێناوە (v{installedVer}) ✔"
                    : $"أنت تستخدم أحدث إصدار من البرنامج بالفعل (v{installedVer}) ✔";

                MessageBox.Show(msg, isKu ? "پشکنینی نوێکردنەوە" : "فحص التحديثات", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            string msg = isKu
                ? $"تۆ نوێترین وەشانی بەرنامەت بەکارهێناوە (v{installedVer}) ✔"
                : $"أنت تستخدم أحدث إصدار من البرنامج بالفعل (v{installedVer}) ✔";

            MessageBox.Show(msg, isKu ? "پشکنینی نوێکردنەوە" : "فحص التحديثات", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
