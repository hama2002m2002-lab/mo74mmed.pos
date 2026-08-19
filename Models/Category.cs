using System.Collections.Generic;

namespace HamoPos.Models;

/// <summary>
/// تصنيف المنتجات (مثل: مشروبات، مأكولات، إلكترونيات، منظفات، إلخ)
/// </summary>
public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; } // أيقونة أو رمز تعبيري
    public string? ColorHex { get; set; } = "#3B82F6"; // لون البطاقة في الواجهة
    public int DisplayOrder { get; set; } = 0;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
