using System.Collections.Generic;

namespace HamoPos.Models;

/// <summary>
/// نموذج المستخدم / الكاشير / المسؤول
/// </summary>
public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PinCode { get; set; } = string.Empty; // رمز PIN السريع (مثل 1234)
    public string Role { get; set; } = "Cashier"; // "Admin", "Cashier", "Supervisor", "Accountant"
    public string Permissions { get; set; } = "[]"; // مصفوفة JSON بالصلاحيات
    public string AvatarIcon { get; set; } = "👤"; // الرمز التعبيري أو الأيقونة
    public string ColorHex { get; set; } = "#3B82F6"; // لون التمييز
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    // المبيعات المرتبطة بالمستخدم
    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
