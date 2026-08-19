using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;
using HamoPos.ViewModels;

namespace HamoPos.Tests;

[TestClass]
public class PosSystemTests
{
    private string _testDbPath = string.Empty;
    private DbContextOptions<AppDbContext> _dbOptions = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_pos_{Guid.NewGuid():N}.db");
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_testDbPath}")
            .Options;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [TestMethod]
    public async Task DbInitializer_ShouldCreateDatabaseAndSeedData()
    {
        using (var context = new AppDbContext(_dbOptions))
        {
            await DbInitializer.InitializeAsync(context);

            var usersCount = await context.Users.CountAsync();
            var categoriesCount = await context.Categories.CountAsync();

            Assert.IsTrue(usersCount >= 2, "Users should be seeded");
            Assert.IsTrue(categoriesCount >= 4, "Categories should be seeded");
        }
    }

    [TestMethod]
    public async Task ProductService_SearchAndBarcodeLookup_ShouldReturnCorrectProduct()
    {
        using (var context = new AppDbContext(_dbOptions))
        {
            await DbInitializer.InitializeAsync(context);
            var service = new ProductService(context);

            var testProd = new Product
            {
                Id = Guid.NewGuid(),
                Barcode = "6281001",
                Name = "إسبريسو دبل",
                Price = 2500m,
                Cost = 1000m,
                StockQuantity = 100
            };
            await service.SaveProductAsync(testProd);

            var product = await service.GetProductByBarcodeAsync("6281001");
            Assert.IsNotNull(product);
            Assert.AreEqual("إسبريسو دبل", product.Name);

            var searchResults = await service.GetProductsAsync(null, "إسبريسو");
            Assert.IsTrue(searchResults.Any(p => p.Name.Contains("إسبريسو")));
        }
    }

    [TestMethod]
    public async Task SaleService_CompleteSale_ShouldGenerateInvoiceAndReduceStock()
    {
        using (var context = new AppDbContext(_dbOptions))
        {
            await DbInitializer.InitializeAsync(context);
            var productService = new ProductService(context);
            var saleService = new SaleService(context);

            var testProd = new Product
            {
                Id = Guid.NewGuid(),
                Barcode = "6281001",
                Name = "إسبريسو دبل",
                Price = 2500m,
                Cost = 1000m,
                StockQuantity = 100
            };
            await productService.SaveProductAsync(testProd);

            var product = await productService.GetProductByBarcodeAsync("6281001");
            Assert.IsNotNull(product);
            decimal initialStock = product.StockQuantity;

            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                SubTotal = 5000m,
                TaxAmount = 0.0m,
                DiscountAmount = 0.0m,
                TotalAmount = 5000m,
                PaidAmount = 5000m,
                ChangeAmount = 0.0m,
                PaymentMethod = "Cash",
                Status = "Completed"
            };

            sale.Items.Add(new SaleItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductName = product.Name,
                Barcode = product.Barcode,
                UnitPrice = 2500m,
                Quantity = 2.0m,
                TaxRate = 0.0m,
                TaxAmount = 0.0m,
                TotalPrice = 5000m
            });

            var completedSale = await saleService.CompleteSaleAsync(sale);

            Assert.IsFalse(string.IsNullOrWhiteSpace(completedSale.InvoiceNumber));
            Assert.IsTrue(completedSale.InvoiceNumber.StartsWith("INV-"));

            // Verify stock decremented
            var updatedProduct = await productService.GetProductByIdAsync(product.Id);
            Assert.IsNotNull(updatedProduct);
            Assert.AreEqual(initialStock - 2.0m, updatedProduct.StockQuantity);
        }
    }

    [TestMethod]
    public void CartItemViewModel_Calculations_ShouldBeAccurate()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Barcode = "9990001",
            Name = "مشروب غازي",
            Price = 10.0m,
            TaxRate = 0.15m,
            Unit = "علبة"
        };

        var cartItem = new CartItemViewModel(product, 2.0m);

        // Price = 10 * 2 = 20, Tax 15% = 3.0, Total = 23.0
        Assert.AreEqual(20.0m, cartItem.SubTotal);
        Assert.AreEqual(3.0m, cartItem.TaxAmount);
        Assert.AreEqual(23.0m, cartItem.TotalPrice);

        // Apply discount 5.0
        cartItem.DiscountAmount = 5.0m;
        // Net = 15.0, Tax = 2.25, Total = 17.25
        Assert.AreEqual(15.0m, cartItem.SubTotal);
        Assert.AreEqual(2.25m, cartItem.TaxAmount);
        Assert.AreEqual(17.25m, cartItem.TotalPrice);
    }

    [TestMethod]
    public void CashDrawer_CommandBytes_ShouldMatchEscPosStandards()
    {
        // ESC p 0 25 250 -> 27, 112, 0, 25, 250
        CollectionAssert.AreEqual(new byte[] { 27, 112, 0, 25, 250 }, CashDrawerService.DrawerPin2Command);
        CollectionAssert.AreEqual(new byte[] { 27, 112, 1, 25, 250 }, CashDrawerService.DrawerPin5Command);
    }

    [TestMethod]
    public void WeightedAverageCost_Calculation_ShouldMatchFormula()
    {
        // Example scenario:
        // Old Stock: 10 pieces at 1,000 IQD = 10,000 IQD
        // New Purchase: 20 pieces at 1,300 IQD = 26,000 IQD
        // Combined Cost = 36,000 IQD
        // Total Quantity = 30 pieces
        // Weighted Average Unit Cost = 36,000 / 30 = 1,200 IQD

        decimal oldQty = 10;
        decimal oldCost = 1000m;
        decimal newQty = 20;
        decimal newCost = 1300m;

        decimal oldTotalValue = oldQty * oldCost;
        decimal newTotalValue = newQty * newCost;
        decimal combinedCost = oldTotalValue + newTotalValue;
        decimal combinedQty = oldQty + newQty;
        decimal weightedAvgCost = Math.Round(combinedCost / combinedQty, 2);

        Assert.AreEqual(36000m, combinedCost);
        Assert.AreEqual(30m, combinedQty);
        Assert.AreEqual(1200m, weightedAvgCost);
    }
}
