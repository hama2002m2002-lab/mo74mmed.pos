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
    public string Role { get; set; } = "Cashier"; // "Admin", "Cashier", "Manager"
    public bool IsActive { get; set; } = true;

    // المبيعات المرتبطة بالمستخدم
    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
