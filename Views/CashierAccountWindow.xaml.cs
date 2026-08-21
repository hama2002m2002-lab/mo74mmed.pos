using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;

namespace HamoPos.Views;

public partial class CashierAccountWindow : Window
{
    private readonly Guid? _userId;
    public event Action? UserSaved;

    public CashierAccountWindow(User? user = null)
    {
        InitializeComponent();
        if (user != null)
        {
            _userId = user.Id;
            TxtTitle.Text = $"تعديل حساب: {user.FullName}";
            TxtFullName.Text = user.FullName;
            TxtUsername.Text = user.Username;
            TxtPassword.Text = user.PasswordHash;
            ChkActive.IsChecked = user.IsActive;

            foreach (ComboBoxItem item in CmbRole.Items)
            {
                if (item.Content.ToString() == user.Role)
                {
                    item.IsSelected = true;
                    break;
                }
            }
        }
        else
        {
            _userId = null;
            TxtTitle.Text = "إنشاء حساب كاشير جديد";
        }
        TxtFullName.Focus();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        string fullName = TxtFullName.Text.Trim();
        string username = TxtUsername.Text.Trim();
        string password = TxtPassword.Text.Trim();
        string role = (CmbRole.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Cashier";
        bool isActive = ChkActive.IsChecked == true;

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("يرجى كتابة الاسم الكامل واسم الدخول.", "بيانات ناقصة", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var db = new AppDbContext();

            if (_userId.HasValue)
            {
                var existing = await db.Users.FindAsync(_userId.Value);
                if (existing != null)
                {
                    existing.FullName = fullName;
                    existing.Username = username;
                    if (!string.IsNullOrWhiteSpace(password)) existing.PasswordHash = password;
                    existing.Role = role;
                    existing.IsActive = isActive;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            else
            {
                bool exists = await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
                if (exists)
                {
                    MessageBox.Show("اسم المستخدم هذا مسجل مسبقاً، يرجى اختيار اسم دخول آخر.", "تكرار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = fullName,
                    Username = username,
                    PasswordHash = string.IsNullOrWhiteSpace(password) ? "1234" : password,
                    Role = role,
                    IsActive = isActive,
                    CreatedAt = DateTime.UtcNow
                };
                await db.Users.AddAsync(newUser);
                await db.SaveChangesAsync();
            }

            UserSaved?.Invoke();
            MessageBox.Show("✔ تم حفظ بيانات الحساب بنجاح!", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء الحفظ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
