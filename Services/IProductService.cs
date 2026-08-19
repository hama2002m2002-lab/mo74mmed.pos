using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HamoPos.Models;

namespace HamoPos.Services;

public interface IProductService
{
    Task<List<Category>> GetCategoriesAsync();
    Task<List<Product>> GetProductsAsync(Guid? categoryId = null, string? searchQuery = null, int take = 100);
    Task<List<Product>> GetAllProductsListAsync(string? searchQuery = null, Guid? categoryId = null);
    Task<List<Product>> GetLowStockProductsAsync(int take = 10);
    Task<int> GetTotalProductsCountAsync();
    Task<Product?> GetProductByBarcodeAsync(string barcode);
    Task<Product?> GetProductByIdAsync(Guid id);
    Task<string> GenerateUniqueBarcodeAsync(string prefix = "200245");
    Task<bool> SaveProductAsync(Product product);
    Task<bool> DeleteProductAsync(Guid id);
    Task<Category> AddCategoryAsync(string name);
}
