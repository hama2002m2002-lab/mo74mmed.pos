using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace HamoPos.Services;

/// <summary>
/// إدارة الثيم والوضع الليلي / النهاري في النظام وتحديث ألوان الواجهات لحظياً
/// </summary>
public class ThemeManager : INotifyPropertyChanged
{
    private static ThemeManager? _instance;
    public static ThemeManager Instance => _instance ??= new ThemeManager();

    private bool _isDarkMode = true;
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                ApplyTheme(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThemeToggleDisplayName));
                OnPropertyChanged(nameof(ThemeIcon));
            }
        }
    }

    public string ThemeIcon => IsDarkMode ? "🌙" : "☀️";
    public string ThemeToggleDisplayName => IsDarkMode ? "الوضع الليلي" : "الوضع النهاري";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ThemeManager()
    {
    }

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }

    public void ApplyTheme(bool isDark)
    {
        if (Application.Current == null) return;

        try
        {
            // 1. Swap Active Theme ResourceDictionary
            string source = isDark ? "Styles/DarkTheme.xaml" : "Styles/LightTheme.xaml";
            var newThemeDict = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };

            if (Application.Current.Resources.MergedDictionaries.Count > 0)
            {
                Application.Current.Resources.MergedDictionaries[0] = newThemeDict;
            }
            else
            {
                Application.Current.Resources.MergedDictionaries.Add(newThemeDict);
            }

            // 2. Also mutate colors across all resources for any existing instances
            var palette = isDark ? GetDarkPalette() : GetLightPalette();
            foreach (var (key, hex) in palette)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                if (Application.Current.Resources[key] is SolidColorBrush rootB && !rootB.IsFrozen)
                {
                    rootB.Color = color;
                }
                Application.Current.Resources[key] = newThemeDict[key];
            }

            // 3. Force Invalidation of Window
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.InvalidateVisual();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Theme apply error: {ex.Message}");
        }
    }

    private static Dictionary<string, string> GetDarkPalette() => new()
    {
        ["BgDarkPrimary"] = "#0F172A",
        ["BgDarkSecondary"] = "#1E293B",
        ["BgDarkCard"] = "#1E293B",
        ["BgDarkHover"] = "#334155",
        ["BgDarkInput"] = "#0F172A",
        ["BorderDark"] = "#334155",
        ["TextPrimary"] = "#F8FAFC",
        ["TextSecondary"] = "#94A3B8",
        ["TextMuted"] = "#64748B",
        ["PrimaryAccent"] = "#3B82F6",
        ["PrimaryAccentHover"] = "#2563EB",
        ["SuccessBrush"] = "#10B981",
        ["SuccessBrushHover"] = "#059669",
        ["WarningBrush"] = "#F59E0B",
        ["WarningBrushHover"] = "#D97706",
        ["DangerBrush"] = "#EF4444",
        ["DangerBrushHover"] = "#DC2626"
    };

    private static Dictionary<string, string> GetLightPalette() => new()
    {
        ["BgDarkPrimary"] = "#F1F5F9",
        ["BgDarkSecondary"] = "#FFFFFF",
        ["BgDarkCard"] = "#FFFFFF",
        ["BgDarkHover"] = "#E2E8F0",
        ["BgDarkInput"] = "#FFFFFF",
        ["BorderDark"] = "#CBD5E1",
        ["TextPrimary"] = "#0F172A",
        ["TextSecondary"] = "#475569",
        ["TextMuted"] = "#64748B",
        ["PrimaryAccent"] = "#2563EB",
        ["PrimaryAccentHover"] = "#1D4ED8",
        ["SuccessBrush"] = "#059669",
        ["SuccessBrushHover"] = "#047857",
        ["WarningBrush"] = "#D97706",
        ["WarningBrushHover"] = "#B45309",
        ["DangerBrush"] = "#DC2626",
        ["DangerBrushHover"] = "#B91C1C"
    };

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
