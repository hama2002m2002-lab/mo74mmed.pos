using System;
using System.Windows.Input;

namespace HamoPos.Models;

/// <summary>
/// نموذج تبويب المتصفح العلوي للتنقل المتعدد بين شاشات النظام
/// </summary>
public class ShellTabItem : HamoPos.ViewModels.BaseViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public object ViewModel { get; set; } = null!;
    public bool CanClose { get; set; } = true;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
