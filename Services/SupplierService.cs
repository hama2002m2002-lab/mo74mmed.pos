using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;

namespace HamoPos.Services;

public class SupplierService : ISupplierService
{
    private readonly AppDbContext _context;

    public SupplierService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Supplier>> GetSuppliersAsync(string? searchQuery = null)
    {
        IQueryable<Supplier> query = _context.Suppliers
            .AsNoTracking()
            .Include(s => s.Products.Where(p => !p.IsDeleted))
            .Include(s => s.Transactions.Where(t => !t.IsDeleted));

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            searchQuery = searchQuery.Trim();
            query = query.Where(s => s.Name.Contains(searchQuery) || 
                                     (s.Company != null && s.Company.Contains(searchQuery)) ||
                                     (s.Phone != null && s.Phone.Contains(searchQuery)));
        }

        return await query
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Supplier?> GetSupplierByIdAsync(Guid id)
    {
        return await _context.Suppliers
            .AsNoTracking()
            .Include(s => s.Products.Where(p => !p.IsDeleted))
            .Include(s => s.Transactions.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Supplier> GetOrCreateSupplierByNameAsync(string name, string? phone = null, string? company = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Supplier name cannot be empty", nameof(name));

        string cleanName = name.Trim();
        using var db = new AppDbContext();
        var existing = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.Suppliers, s => s.Name.ToLower() == cleanName.ToLower() && !s.IsDeleted);
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(phone)) existing.Phone = phone;
            if (!string.IsNullOrWhiteSpace(company)) existing.Company = company;
            if (!string.IsNullOrWhiteSpace(notes)) existing.Notes = notes;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing;
        }

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = cleanName,
            Phone = phone,
            Company = company,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        await db.Suppliers.AddAsync(supplier);
        await db.SaveChangesAsync();
        return supplier;
    }

    public async Task<bool> SaveSupplierAsync(Supplier supplier)
    {
        using var db = new AppDbContext();
        if (supplier.Id == Guid.Empty)
        {
            supplier.Id = Guid.NewGuid();
            supplier.CreatedAt = DateTime.UtcNow;
            await db.Suppliers.AddAsync(supplier);
        }
        else
        {
            var existing = await db.Suppliers.FindAsync(supplier.Id);
            if (existing == null)
            {
                supplier.CreatedAt = DateTime.UtcNow;
                await db.Suppliers.AddAsync(supplier);
            }
            else
            {
                existing.Name = supplier.Name;
                existing.Phone = supplier.Phone;
                existing.Company = supplier.Company;
                existing.Address = supplier.Address;
                existing.OpeningBalance = supplier.OpeningBalance;
                existing.Balance = supplier.Balance;
                existing.Notes = supplier.Notes;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDeleted = false;
            }
        }

        return await db.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteSupplierAsync(Guid id)
    {
        using var db = new AppDbContext();
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier == null)
            return false;

        supplier.IsDeleted = true;
        supplier.UpdatedAt = DateTime.UtcNow;
        return await db.SaveChangesAsync() > 0;
    }

    public async Task<bool> AddTransactionAsync(Guid supplierId, string type, decimal amount, string? description, string? invoiceNumber = null)
    {
        using var db = new AppDbContext();
        var transaction = new SupplierTransaction
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            TransactionType = type,
            Amount = amount,
            Description = description,
            InvoiceNumber = invoiceNumber,
            TransactionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var supplier = await db.Suppliers.FindAsync(supplierId);
        if (supplier != null)
        {
            if (type == "Payment" || type == "دفع" || type == "Return" || type == "إرجاع")
            {
                supplier.Balance -= amount;
            }
            else if (type == "Purchase" || type == "شراء")
            {
                supplier.Balance += amount;
            }
            supplier.UpdatedAt = DateTime.UtcNow;
        }

        await db.SupplierTransactions.AddAsync(transaction);
        return await db.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateSupplierBalanceAsync(Guid supplierId, decimal newBalance)
    {
        using var db = new AppDbContext();
        var supplier = await db.Suppliers.FindAsync(supplierId);
        if (supplier == null)
            return false;

        supplier.Balance = newBalance;
        supplier.UpdatedAt = DateTime.UtcNow;
        return await db.SaveChangesAsync() > 0;
    }

    public async Task<List<SupplierTransaction>> GetSupplierTransactionsAsync(Guid supplierId)
    {
        return await _context.SupplierTransactions
            .AsNoTracking()
            .Where(t => t.SupplierId == supplierId && !t.IsDeleted)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }
}
