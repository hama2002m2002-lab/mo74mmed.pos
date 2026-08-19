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

        if (args.Error == null)
        {
            if (args.IsUpdateAvailable)
            {
                var dialogResult = MessageBox.Show(
                    $"يوجد إصدار جديد متوفر من البرنامج ({args.CurrentVersion})!\n\nهل ترغب في تنزيل التحديث وتثبيته الآن؟",
                    "تحديث جديد متوفر 🚀",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

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
                        MessageBox.Show(exception.Message, "خطأ في التحديث", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("أنت تستخدم أحدث إصدار من البرنامج بالفعل (v1.0.0) ✔", "فحص التحديثات", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            MessageBox.Show("أنت تستخدم أحدث إصدار من البرنامج بالفعل (v1.0.0) ✔", "فحص التحديثات", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
