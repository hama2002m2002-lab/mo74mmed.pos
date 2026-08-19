using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HamoPos.Models;

namespace HamoPos.Services;

public interface ISupplierService
{
    Task<List<Supplier>> GetSuppliersAsync(string? searchQuery = null);
    Task<Supplier?> GetSupplierByIdAsync(Guid id);
    Task<Supplier> GetOrCreateSupplierByNameAsync(string name, string? phone = null, string? company = null, string? notes = null);
    Task<bool> SaveSupplierAsync(Supplier supplier);
    Task<bool> DeleteSupplierAsync(Guid id);
    Task<bool> AddTransactionAsync(Guid supplierId, string type, decimal amount, string? description, string? invoiceNumber = null);
    Task<List<SupplierTransaction>> GetSupplierTransactionsAsync(Guid supplierId);
}
