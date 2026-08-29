using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HamoPos.Models;

namespace HamoPos.Data;

/// <summary>
/// تهيئة قاعدة البيانات المحلية وتحديث الـ Schema تلقائياً لكافة الجداول
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        // 1. تفعيل WAL Mode في SQLite للسرعة الفائقة
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;");
        await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;");

        // 2. فحص وتحديث Schema لكافة الجداول لضمان عدم حدوث أي خطأ no such column
        await EnsureAllTablesAndColumnsUpToDateAsync(context);

        // 3. إضافة المستخدمين الافتراضيين أو تحديث بياناتهم
        if (!await context.Users.AnyAsync())
        {
            var defaultAdmin = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                FullName = "مدير النظام",
                PasswordHash = "admin",
                PinCode = "1111",
                Role = "Admin",
                Permissions = "[\"*\"]",
                AvatarIcon = "👑",
                ColorHex = "#8B5CF6",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var defaultCashier = new User
            {
                Id = Guid.NewGuid(),
                Username = "cashier",
                FullName = "محمد الكاشير",
                PasswordHash = "123",
                PinCode = "1234",
                Role = "Cashier",
                Permissions = "[\"pos_sales\",\"pos_discount\",\"invoices_view\",\"invoices_return\",\"customers_view\",\"customers_manage\",\"cash_drawer\"]",
                AvatarIcon = "🧑‍💼",
                ColorHex = "#10B981",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await context.Users.AddRangeAsync(defaultAdmin, defaultCashier);
        }
        else
        {
            // تحديث المستخدمين الحاليين الذين ليس لديهم رمز PIN
            var existingUsers = await context.Users.ToListAsync();
            foreach (var u in existingUsers)
            {
                if (string.IsNullOrEmpty(u.PinCode))
                {
                    u.PinCode = (u.Role == "Admin" || u.Username == "admin") ? "1111" : "1234";
                }
                if (string.IsNullOrEmpty(u.Permissions) || u.Permissions == "[]")
                {
                    u.Permissions = (u.Role == "Admin" || u.Username == "admin") ? "[\"*\"]" : "[\"pos_sales\",\"pos_discount\",\"invoices_view\",\"invoices_return\",\"customers_view\",\"customers_manage\",\"cash_drawer\"]";
                }
                if (string.IsNullOrEmpty(u.AvatarIcon))
                {
                    u.AvatarIcon = (u.Role == "Admin") ? "👑" : "🧑‍💼";
                }
                if (string.IsNullOrEmpty(u.ColorHex))
                {
                    u.ColorHex = (u.Role == "Admin") ? "#8B5CF6" : "#10B981";
                }
            }
            await context.SaveChangesAsync();
        }

        // 4. إضافة التصنيفات الأساسية النظيفة
        if (!await context.Categories.AnyAsync())
        {
            var catBeverages = new Category { Id = Guid.NewGuid(), Name = "مشروبات", Icon = "☕", ColorHex = "#3B82F6", DisplayOrder = 1 };
            var catSnacks = new Category { Id = Guid.NewGuid(), Name = "مواد غذائية", Icon = "🍔", ColorHex = "#10B981", DisplayOrder = 2 };
            var catDesserts = new Category { Id = Guid.NewGuid(), Name = "منظفات", Icon = "🍰", ColorHex = "#F59E0B", DisplayOrder = 3 };
            var catGroceries = new Category { Id = Guid.NewGuid(), Name = "أخرى", Icon = "🛒", ColorHex = "#8B5CF6", DisplayOrder = 4 };

            await context.Categories.AddRangeAsync(catBeverages, catSnacks, catDesserts, catGroceries);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// فحص وإضافة الأعمدة والجداول المحدثة لكافة جداول النظام في SQLite
    /// </summary>
    private static async Task EnsureAllTablesAndColumnsUpToDateAsync(AppDbContext context)
    {
        try
        {
            var conn = context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            // 1. إنشاء جدول Suppliers إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""Suppliers"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Suppliers"" PRIMARY KEY,
                    ""Name"" TEXT NOT NULL,
                    ""Phone"" TEXT NULL,
                    ""Company"" TEXT NULL,
                    ""Address"" TEXT NULL,
                    ""OpeningBalance"" TEXT NOT NULL DEFAULT '0',
                    ""Balance"" TEXT NOT NULL DEFAULT '0',
                    ""Notes"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""IsSynced"" INTEGER NOT NULL DEFAULT 0,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0
                );
            ");

            // 2. إنشاء جدول SupplierTransactions
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""SupplierTransactions"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_SupplierTransactions"" PRIMARY KEY,
                    ""SupplierId"" TEXT NOT NULL,
                    ""TransactionType"" TEXT NOT NULL,
                    ""Amount"" TEXT NOT NULL DEFAULT '0',
                    ""InvoiceNumber"" TEXT NOT NULL,
                    ""Description"" TEXT NULL,
                    ""TransactionDate"" TEXT NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""IsSynced"" INTEGER NOT NULL DEFAULT 0,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0
                );
            ");

            // 3. إنشاء جدول DamagedItems (المواد التالفة والهالك)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""DamagedItems"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_DamagedItems"" PRIMARY KEY,
                    ""ProductId"" TEXT NOT NULL,
                    ""ProductName"" TEXT NOT NULL,
                    ""Barcode"" TEXT NOT NULL,
                    ""Quantity"" TEXT NOT NULL DEFAULT '0',
                    ""UnitCost"" TEXT NOT NULL DEFAULT '0',
                    ""TotalLossAmount"" TEXT NOT NULL DEFAULT '0',
                    ""Reason"" TEXT NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""IsSynced"" INTEGER NOT NULL DEFAULT 0,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0
                );
            ");

            // 4. إنشاء جدول PurchaseInvoices (فواتير الشراء والتوريد)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""PurchaseInvoices"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_PurchaseInvoices"" PRIMARY KEY,
                    ""InvoiceNumber"" TEXT NOT NULL,
                    ""SupplierId"" TEXT NOT NULL,
                    ""SupplierName"" TEXT NOT NULL,
                    ""TotalAmount"" TEXT NOT NULL DEFAULT '0',
                    ""PaidAmount"" TEXT NOT NULL DEFAULT '0',
                    ""PaymentMethod"" TEXT NOT NULL DEFAULT 'Cash',
                    ""Notes"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""IsSynced"" INTEGER NOT NULL DEFAULT 0,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0
                );
            ");

            // 5. إنشاء جدول PurchaseInvoiceItems (بنود فواتير الشراء)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""PurchaseInvoiceItems"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_PurchaseInvoiceItems"" PRIMARY KEY,
                    ""PurchaseInvoiceId"" TEXT NOT NULL,
                    ""ProductId"" TEXT NOT NULL,
                    ""ProductName"" TEXT NOT NULL,
                    ""Barcode"" TEXT NOT NULL,
                    ""Quantity"" TEXT NOT NULL DEFAULT '0',
                    ""UnitCost"" TEXT NOT NULL DEFAULT '0',
                    ""SellingPrice"" TEXT NOT NULL DEFAULT '0',
                    ""IsCarton"" INTEGER NOT NULL DEFAULT 0,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""IsSynced"" INTEGER NOT NULL DEFAULT 0,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0
                );
            ");

            // 6. إنشاء جدول CustomerDebts (حسابات وديون العملاء والآجل)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""CustomerDebts"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_CustomerDebts"" PRIMARY KEY,
                    ""CustomerName"" TEXT NOT NULL,
                    ""PhoneNumber"" TEXT NOT NULL,
                    ""TotalDebt"" TEXT NOT NULL DEFAULT '0',
                    ""TotalPaid"" TEXT NOT NULL DEFAULT '0',
                    ""LastTransactionType"" TEXT NOT NULL DEFAULT 'دين جديد',
                    ""Notes"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""IsSynced"" INTEGER NOT NULL DEFAULT 0,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0
                );
            ");

            // 7. إنشاء جدول ShiftAudits (التدقيق الأمني وإغلاق الوردية)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""ShiftAudits"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_ShiftAudits"" PRIMARY KEY,
                    ""CashierName"" TEXT NOT NULL,
                    ""ShiftStartTime"" TEXT NOT NULL,
                    ""ShiftEndTime"" TEXT NOT NULL,
                    ""OpeningBalance"" TEXT NOT NULL DEFAULT '0',
                    ""TotalSalesCash"" TEXT NOT NULL DEFAULT '0',
                    ""TotalSalesCard"" TEXT NOT NULL DEFAULT '0',
                    ""TotalReturnsCash"" TEXT NOT NULL DEFAULT '0',
                    ""ActualCountedCash"" TEXT NOT NULL DEFAULT '0',
                    ""HandoverNotes"" TEXT NOT NULL DEFAULT '',
                    ""SupervisorName"" TEXT NOT NULL DEFAULT '',
                    ""CreatedAt"" TEXT NOT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""IsSynced"" INTEGER NOT NULL DEFAULT 0,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0
                );
            ");

            // 7.1 إنشاء جدول Expenses (المصروفات العامة)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""Expenses"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Expenses"" PRIMARY KEY,
                    ""Title"" TEXT NOT NULL,
                    ""Amount"" TEXT NOT NULL DEFAULT '0',
                    ""Category"" TEXT NOT NULL DEFAULT 'عام',
                    ""Notes"" TEXT NULL,
                    ""RecordedBy"" TEXT NOT NULL DEFAULT 'مدير النظام',
                    ""CreatedAt"" TEXT NOT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""IsSynced"" INTEGER NOT NULL DEFAULT 0,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0
                );
            ");

            // 7.2 إنشاء جدول CashDrawerMovements (حركات الدرج والصندوق)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""CashDrawerMovements"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_CashDrawerMovements"" PRIMARY KEY,
                    ""CashierName"" TEXT NOT NULL,
                    ""MovementType"" TEXT NOT NULL DEFAULT 'Deposit',
                    ""Amount"" TEXT NOT NULL DEFAULT '0',
                    ""Reason"" TEXT NOT NULL DEFAULT '',
                    ""CreatedAt"" TEXT NOT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""IsSynced"" INTEGER NOT NULL DEFAULT 0,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0
                );
            ");

            // 8. تحديث أعمدة جدول Products
            await AddColumnIfNotExistsAsync(conn, "Products", "CartonPurchasePrice", "TEXT NOT NULL DEFAULT '0'");
            await AddColumnIfNotExistsAsync(conn, "Products", "Cost", "TEXT NOT NULL DEFAULT '0'");
            await AddColumnIfNotExistsAsync(conn, "Products", "CartonSellingPrice", "TEXT NOT NULL DEFAULT '0'");
            await AddColumnIfNotExistsAsync(conn, "Products", "WholesalePrice", "TEXT NOT NULL DEFAULT '0'");
            await AddColumnIfNotExistsAsync(conn, "Products", "CartonsCount", "TEXT NOT NULL DEFAULT '0'");
            await AddColumnIfNotExistsAsync(conn, "Products", "ItemsPerCarton", "TEXT NOT NULL DEFAULT '1'");
            await AddColumnIfNotExistsAsync(conn, "Products", "SupplierId", "TEXT NULL");
            await AddColumnIfNotExistsAsync(conn, "Products", "SupplierName", "TEXT NULL");
            await AddColumnIfNotExistsAsync(conn, "Products", "ExpiryDate", "TEXT NULL");
            await AddColumnIfNotExistsAsync(conn, "Products", "ExpiryAlertDays", "INTEGER NOT NULL DEFAULT 30");
            await AddColumnIfNotExistsAsync(conn, "Products", "IsSynced", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfNotExistsAsync(conn, "Products", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");

            // 9. تحديث أعمدة جدول Sales
            await AddColumnIfNotExistsAsync(conn, "Sales", "IsSynced", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfNotExistsAsync(conn, "Sales", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfNotExistsAsync(conn, "Sales", "Status", "TEXT NOT NULL DEFAULT 'Completed'");
            await AddColumnIfNotExistsAsync(conn, "Sales", "DiscountAmount", "TEXT NOT NULL DEFAULT '0'");

            // 10. تحديث أعمدة جدول SaleItems
            await AddColumnIfNotExistsAsync(conn, "SaleItems", "DiscountAmount", "TEXT NOT NULL DEFAULT '0'");
            await AddColumnIfNotExistsAsync(conn, "SaleItems", "TaxAmount", "TEXT NOT NULL DEFAULT '0'");

            // 11. تحديث أعمدة جدول PurchaseInvoices
            await AddColumnIfNotExistsAsync(conn, "PurchaseInvoices", "ReceiptImagePath", "TEXT NULL");

            // 12. تحديث أعمدة جدول Users (رمز PIN، الصلاحيات، الأيقونة واللون)
            await AddColumnIfNotExistsAsync(conn, "Users", "PinCode", "TEXT NOT NULL DEFAULT '1234'");
            await AddColumnIfNotExistsAsync(conn, "Users", "Permissions", "TEXT NOT NULL DEFAULT '[]'");
            await AddColumnIfNotExistsAsync(conn, "Users", "AvatarIcon", "TEXT NOT NULL DEFAULT '👤'");
            await AddColumnIfNotExistsAsync(conn, "Users", "ColorHex", "TEXT NOT NULL DEFAULT '#3B82F6'");
            await AddColumnIfNotExistsAsync(conn, "Users", "LastLoginAt", "TEXT NULL");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DbInitializer] Migration Exception: {ex.Message}");
        }
    }

    private static async Task AddColumnIfNotExistsAsync(DbConnection conn, string tableName, string columnName, string columnDef)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
            using var reader = await cmd.ExecuteReaderAsync();
            bool exists = false;
            while (await reader.ReadAsync())
            {
                var name = reader["name"]?.ToString();
                if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
            reader.Close();

            if (!exists)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDef};";
                await alterCmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DbInitializer] Failed to add column {columnName} to {tableName}: {ex.Message}");
        }
    }
}
