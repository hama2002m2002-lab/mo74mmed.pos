using System;

namespace HamoPos.Models;

/// <summary>
/// الكيان الأساسي الذي ترث منه كافة الكيانات في قاعدة البيانات
/// يوفّر معرف فريد Guid وحقول التدقيق والمزامنة السحابية Offline-First
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsSynced { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
}
