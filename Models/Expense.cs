using System;

namespace HamoPos.Models;

/// <summary>
/// نموذج تسجيل المصروفات العامة (إيجار، كهرباء، رواتب، نثريات...)
/// لحساب صافي الأرباح الحقيقي بدقة
/// </summary>
public class Expense : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; } = 0.0m;
    public string Category { get; set; } = "عام"; // إيجار، كهرباء، رواتب، صيانة، نثريات
    public string? Notes { get; set; }
    public string RecordedBy { get; set; } = "مدير النظام";
}
