using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;

namespace HamoPos.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _context;

    public ProductService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        return await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<Product>> GetProductsAsync(Guid? categoryId = null, string? searchQuery = null, int take = 100)
    {
        IQueryable<Product> query = _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && !p.IsDeleted);

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            searchQuery = searchQuery.Trim();
            query = query.Where(p => p.Barcode.StartsWith(searchQuery) || 
                                     p.Name.Contains(searchQuery));
        }

        return await query
            .OrderBy(p => p.Name)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<Product>> GetAllProductsListAsync(string? searchQuery = null, Guid? categoryId = null)
    {
        IQueryable<Product> query = _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted);

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            searchQuery = searchQuery.Trim();
            query = query.Where(p => p.Barcode.Contains(searchQuery) || 
                                     p.Name.Contains(searchQuery));
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Product>> GetLowStockProductsAsync(int take = 10)
    {
        return await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && !p.IsDeleted && p.StockQuantity <= p.MinStockAlert)
            .OrderBy(p => p.StockQuantity)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetTotalProductsCountAsync()
    {
        return await _context.Products.CountAsync(p => p.IsActive && !p.IsDeleted);
    }

    public async Task<Product?> GetProductByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        string cleanBarcode = barcode.Trim();
        return await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Barcode == cleanBarcode && p.IsActive && !p.IsDeleted);
    }

    public async Task<Product?> GetProductByIdAsync(Guid id)
    {
        return await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<string> GenerateUniqueBarcodeAsync(string prefix = "200245")
    {
        var random = new Random();
        string barcode;
        bool exists;

        do
        {
            // إنشاء رقم تسلسلي عشوائي فريد مكون من 7 أرقام بعد البادئة
            int suffix = random.Next(1000000, 9999999);
            barcode = $"{prefix}{suffix}";

            exists = await _context.Products
                .IgnoreQueryFilters()
                .AnyAsync(p => p.Barcode == barcode);
        } while (exists);

        return barcode;
    }

    public async Task<bool> SaveProductAsync(Product product)
    {
        try
        {
            using var db = new AppDbContext();
            string cleanBarcode = product.Barcode?.Trim() ?? string.Empty;

            if (product.Id == Guid.Empty)
            {
                var existing = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Barcode == cleanBarcode);
                if (existing != null)
                {
                    existing.Name = product.Name?.Trim() ?? string.Empty;
                    existing.CategoryId = product.CategoryId;
                    existing.SupplierId = product.SupplierId;
                    existing.SupplierName = product.SupplierName;
                    existing.CartonsCount = product.CartonsCount;
                    existing.ItemsPerCarton = product.ItemsPerCarton;
                    existing.StockQuantity = product.StockQuantity;
                    existing.MinStockAlert = product.MinStockAlert;
                    existing.Unit = string.IsNullOrWhiteSpace(product.Unit) ? "قطعة" : product.Unit.Trim();
                    existing.CartonPurchasePrice = product.CartonPurchasePrice;
                    existing.Cost = product.Cost;
                    existing.Price = product.Price;
                    existing.WholesalePrice = product.WholesalePrice;
                    existing.CartonSellingPrice = product.CartonSellingPrice;
                    existing.ExpiryDate = product.ExpiryDate;
                    existing.ExpiryAlertDays = product.ExpiryAlertDays;
                    existing.IsActive = true;
                    existing.IsDeleted = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                    db.Products.Update(existing);
                }
                else
                {
                    product.Id = Guid.NewGuid();
                    product.Barcode = cleanBarcode;
                    product.CreatedAt = DateTime.UtcNow;
                    product.IsActive = true;
                    product.IsDeleted = false;
                    await db.Products.AddAsync(product);
                }
            }
            else
            {
                var existing = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == product.Id);
                if (existing != null)
                {
                    existing.Barcode = cleanBarcode;
                    existing.Name = product.Name?.Trim() ?? string.Empty;
                    existing.CategoryId = product.CategoryId;
                    existing.SupplierId = product.SupplierId;
                    existing.SupplierName = product.SupplierName;
                    existing.CartonsCount = product.CartonsCount;
                    existing.ItemsPerCarton = product.ItemsPerCarton;
                    existing.StockQuantity = product.StockQuantity;
                    existing.MinStockAlert = product.MinStockAlert;
                    existing.Unit = string.IsNullOrWhiteSpace(product.Unit) ? "قطعة" : product.Unit.Trim();
                    existing.CartonPurchasePrice = product.CartonPurchasePrice;
                    existing.Cost = product.Cost;
                    existing.Price = product.Price;
                    existing.WholesalePrice = product.WholesalePrice;
                    existing.CartonSellingPrice = product.CartonSellingPrice;
                    existing.ExpiryDate = product.ExpiryDate;
                    existing.ExpiryAlertDays = product.ExpiryAlertDays;
                    existing.IsActive = true;
                    existing.IsDeleted = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                    db.Products.Update(existing);
                }
                else
                {
                    product.CreatedAt = DateTime.UtcNow;
                    product.IsActive = true;
                    product.IsDeleted = false;
                    await db.Products.AddAsync(product);
                }
            }

            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveProductAsync error: {ex}");
            return false;
        }
    }

    public async Task<bool> DeleteProductAsync(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            return false;

        product.IsDeleted = true;
        product.UpdatedAt = DateTime.UtcNow;
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<Category> AddCategoryAsync(string name)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Icon = "📦",
            ColorHex = "#3B82F6",
            CreatedAt = DateTime.UtcNow
        };

        await _context.Categories.AddAsync(category);
        await _context.SaveChangesAsync();
        return category;
    }
}
