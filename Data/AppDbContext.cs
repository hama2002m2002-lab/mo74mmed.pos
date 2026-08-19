using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using HamoPos.Models;

namespace HamoPos.Data;

/// <summary>
/// سياق قاعدة البيانات المحليّة SQLite باستخدام Entity Framework Core
/// مصمم للعمل Offline-First بكفاءة وسرعة فائقة
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierTransaction> SupplierTransactions => Set<SupplierTransaction>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<DamagedItem> DamagedItems => Set<DamagedItem>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
    public DbSet<CustomerDebt> CustomerDebts => Set<CustomerDebt>();
    public DbSet<ShiftAudit> ShiftAudits => Set<ShiftAudit>();
    public DbSet<CashDrawerMovement> CashDrawerMovements => Set<CashDrawerMovement>();
    public DbSet<SupplierOrder> SupplierOrders => Set<SupplierOrder>();
    public DbSet<SupplierOrderItem> SupplierOrderItems => Set<SupplierOrderItem>();

    public static string DatabasePath => HamoPos.Services.NetworkConfigService.Instance.GetEffectiveDatabasePath();

    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            string dbPath = DatabasePath;
            // SQLite connection string with WAL and busy timeout for concurrent multi-device network access
            string connStr = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared;Busy Timeout=5000;";
            optionsBuilder.UseSqlite(connStr);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. إعدادات المستخدمين (Users)
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Username).IsRequired().HasMaxLength(100);
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(200);
            entity.HasQueryFilter(u => !u.IsDeleted);
        });

        // 2. إعدادات التصنيفات (Categories)
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Name);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(150);
            entity.HasQueryFilter(c => !c.IsDeleted);
        });

        // 3. إعدادات المناديب والموردين (Suppliers)
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.Name).HasDatabaseName("IX_Suppliers_Name");
            entity.Property(s => s.Name).IsRequired().HasMaxLength(200);
            entity.HasQueryFilter(s => !s.IsDeleted);
        });

        // 4. إعدادات معاملات المناديب (SupplierTransactions)
        modelBuilder.Entity<SupplierTransaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.SupplierId).HasDatabaseName("IX_SupplierTransactions_SupplierId");
            entity.HasIndex(t => t.TransactionDate).HasDatabaseName("IX_SupplierTransactions_Date");

            entity.HasOne(t => t.Supplier)
                  .WithMany(s => s.Transactions)
                  .HasForeignKey(t => t.SupplierId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(t => !t.IsDeleted);
        });

        // 5. إعدادات المنتجات (Products) - فهارس محسنة لمئات الآلاف من المواد
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            
            entity.HasIndex(p => p.Barcode).HasDatabaseName("IX_Products_Barcode");
            entity.HasIndex(p => p.Name).HasDatabaseName("IX_Products_Name");
            entity.HasIndex(p => p.CategoryId).HasDatabaseName("IX_Products_CategoryId");
            entity.HasIndex(p => p.SupplierId).HasDatabaseName("IX_Products_SupplierId");
            entity.HasIndex(p => new { p.IsActive, p.IsDeleted }).HasDatabaseName("IX_Products_ActiveDeleted");

            entity.Property(p => p.Barcode).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(300);

            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(p => p.Supplier)
                  .WithMany(s => s.Products)
                  .HasForeignKey(p => p.SupplierId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(p => !p.IsDeleted);
        });

        // 6. إعدادات المبيعات (Sales)
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.InvoiceNumber).IsUnique().HasDatabaseName("IX_Sales_InvoiceNumber");
            entity.HasIndex(s => s.CreatedAt).HasDatabaseName("IX_Sales_CreatedAt");
            entity.HasIndex(s => s.Status).HasDatabaseName("IX_Sales_Status");

            entity.Property(s => s.InvoiceNumber).IsRequired().HasMaxLength(100);

            entity.HasOne(s => s.User)
                  .WithMany(u => u.Sales)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(s => s.Items)
                  .WithOne(i => i.Sale)
                  .HasForeignKey(i => i.SaleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(s => !s.IsDeleted);
        });

        // 7. إعدادات بنود الفاتورة (SaleItems)
        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.SaleId).HasDatabaseName("IX_SaleItems_SaleId");
            entity.HasIndex(i => i.ProductId).HasDatabaseName("IX_SaleItems_ProductId");

            entity.HasOne(i => i.Product)
                  .WithMany()
                  .HasForeignKey(i => i.ProductId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(i => !i.IsDeleted);
        });

        // 8. إعدادات طلبيات المناديب والمحلات (SupplierOrders)
        modelBuilder.Entity<SupplierOrder>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.HasIndex(o => o.OrderNumber).HasDatabaseName("IX_SupplierOrders_OrderNumber");
            entity.HasIndex(o => o.MarketName).HasDatabaseName("IX_SupplierOrders_MarketName");
            entity.HasIndex(o => o.SupplierId).HasDatabaseName("IX_SupplierOrders_SupplierId");
            entity.HasIndex(o => o.OrderDate).HasDatabaseName("IX_SupplierOrders_Date");

            entity.HasOne(o => o.Supplier)
                  .WithMany()
                  .HasForeignKey(o => o.SupplierId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(o => o.Items)
                  .WithOne(i => i.SupplierOrder)
                  .HasForeignKey(i => i.SupplierOrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(o => !o.IsDeleted);
        });

        modelBuilder.Entity<SupplierOrderItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.SupplierOrderId).HasDatabaseName("IX_SupplierOrderItems_OrderId");
            entity.HasIndex(i => i.ProductId).HasDatabaseName("IX_SupplierOrderItems_ProductId");

            entity.HasOne(i => i.Product)
                  .WithMany()
                  .HasForeignKey(i => i.ProductId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(i => !i.IsDeleted);
        });
    }
}
