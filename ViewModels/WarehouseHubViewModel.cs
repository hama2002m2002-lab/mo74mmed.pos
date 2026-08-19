using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class WarehouseHubViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;

    #region KPI Stats

    private int _totalProductsCount;
    public int TotalProductsCount
    {
        get => _totalProductsCount;
        set => SetProperty(ref _totalProductsCount, value);
    }

    private decimal _totalStockPurchaseCost;
    public decimal TotalStockPurchaseCost
    {
        get => _totalStockPurchaseCost;
        set => SetProperty(ref _totalStockPurchaseCost, value);
    }

    private decimal _totalRemainingPieces;
    public decimal TotalRemainingPieces
    {
        get => _totalRemainingPieces;
        set => SetProperty(ref _totalRemainingPieces, value);
    }

    private decimal _totalRemainingCartons;
    public decimal TotalRemainingCartons
    {
        get => _totalRemainingCartons;
        set => SetProperty(ref _totalRemainingCartons, value);
    }

    private int _totalDamagedCount;
    public int TotalDamagedCount
    {
        get => _totalDamagedCount;
        set => SetProperty(ref _totalDamagedCount, value);
    }

    #endregion

    #region Navigation Events

    public event Action? RequestOpenInventory;
    public event Action? RequestOpenAddProduct;
    public event Action? RequestOpenDamagedItems;
    public event Action? RequestOpenStock;
    public event Action? RequestOpenStockAudit;
    public event Action<Product>? RequestEditProduct;

    #endregion

    #region Commands

    public ICommand OpenInventoryCommand { get; }
    public ICommand OpenAddProductCommand { get; }
    public ICommand OpenDamagedItemsCommand { get; }
    public ICommand OpenStockCommand { get; }
    public ICommand OpenStockAuditCommand { get; }
    public ICommand EditProductCommand { get; }
    public ICommand RefreshCommand { get; }

    #endregion

    public WarehouseHubViewModel()
    {
        _context = new AppDbContext();
        _productService = new ProductService(_context);

        OpenInventoryCommand = new RelayCommand(() => RequestOpenInventory?.Invoke());
        OpenAddProductCommand = new RelayCommand(() => RequestOpenAddProduct?.Invoke());
        OpenDamagedItemsCommand = new RelayCommand(() => RequestOpenDamagedItems?.Invoke());
        OpenStockCommand = new RelayCommand(() => RequestOpenStock?.Invoke());
        OpenStockAuditCommand = new RelayCommand(() => RequestOpenStockAudit?.Invoke());

        EditProductCommand = new RelayCommand(param =>
        {
            if (param is Product p)
            {
                RequestEditProduct?.Invoke(p);
            }
        });

        RefreshCommand = new AsyncRelayCommand(async () => await LoadHubDataAsync());
    }

    public async Task InitializeAsync()
    {
        await LoadHubDataAsync();
    }

    public async Task LoadHubDataAsync()
    {
        var products = await _productService.GetAllProductsListAsync(null);
        var damagedCount = await _context.DamagedItems.CountAsync();

        decimal totalPieces = 0;
        decimal totalCartons = 0;
        decimal totalPurchaseCost = 0;

        foreach (var p in products)
        {
            totalPieces += p.StockQuantity;
            totalCartons += p.CartonsCount;
            totalPurchaseCost += (p.StockQuantity * p.Cost);
        }

        TotalProductsCount = products.Count;
        TotalRemainingPieces = totalPieces;
        TotalRemainingCartons = totalCartons;
        TotalStockPurchaseCost = totalPurchaseCost;
        TotalDamagedCount = damagedCount;
    }
}
