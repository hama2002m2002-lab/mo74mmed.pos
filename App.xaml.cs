using System;
using System.IO;
using System.Windows;

namespace HamoPos;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (s, args) =>
        {
            try {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt"), $"[{DateTime.Now}] {args.Exception}\n");
            } catch { }
            args.Handled = true;
        };
    }
}
