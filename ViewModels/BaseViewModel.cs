using System.ComponentModel;
using System.Runtime.CompilerServices;
using HamoPos.Services;

namespace HamoPos.ViewModels;

/// <summary>
/// الكلاس الأساسي للـ ViewModel لتطبيق إشعارات تغير الخصائص INotifyPropertyChanged
/// ودعم الترجمة واللغات التفاعلية
/// </summary>
public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationManager Loc => LocalizationManager.Instance;

    public BaseViewModel()
    {
        LocalizationManager.Instance.LanguageChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(Loc));
            OnLanguageChanged();
        };
    }

    protected virtual void OnLanguageChanged() { }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
