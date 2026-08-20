using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class InventoryViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();

    #region Search & Category Filter

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _ = LoadProductsAsync();
            }
        }
    }

    private Category? _selectedCategory;
    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                _ = LoadProductsAsync();
            }
        }
    }

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    #endregion

    #region Inventory Financial & Quantity KPIs (حسابات قيمة الشراء، الكراتين، والمفرد)

    private int _totalItemsCount;
    public int TotalItemsCount
    {
        get => _totalItemsCount;
        set => SetProperty(ref _totalItemsCount, value);
    }

    private decimal _totalStockPurchaseCost;
    public decimal TotalStockPurchaseCost
    {
        get => _totalStockPurchaseCost;
        set => SetProperty(ref _totalStockPurchaseCost, value);
    }

    private decimal _totalStockPieces;
    public decimal TotalStockPieces
    {
        get => _totalStockPieces;
        set => SetProperty(ref _totalStockPieces, value);
    }

    private decimal _totalStockCartons;
    public decimal TotalStockCartons
    {
        get => _totalStockCartons;
        set => SetProperty(ref _totalStockCartons, value);
    }

    private decimal _totalStockSellingValue;
    public decimal TotalStockSellingValue
    {
        get => _totalStockSellingValue;
        set => SetProperty(ref _totalStockSellingValue, value);
    }

    private decimal _totalExpectedProfit;
    public decimal TotalExpectedProfit
    {
        get => _totalExpectedProfit;
        set => SetProperty(ref _totalExpectedProfit, value);
    }

    #endregion

    #region Commands & Events

    public ICommand AddProductCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand EditProductCommand { get; }
    public ICommand DeleteProductCommand { get; }
    public ICommand QuickAddStockCommand { get; }
    public ICommand BackToMainCommand { get; }

    public event Action? RequestAddProduct;
    public event Action<Product>? RequestEditProduct;
    public event Action? RequestBackToNavigation;

    #endregion

    public InventoryViewModel()
    {
        _context = new AppDbContext();
        _productService = new ProductService(_context);

        AddProductCommand = new RelayCommand(() => RequestAddProduct?.Invoke());
        RefreshCommand = new AsyncRelayCommand(async () => await LoadProductsAsync());

        EditProductCommand = new RelayCommand((param) =>
        {
            if (param is Product p)
            {
                RequestEditProduct?.Invoke(p);
            }
            else if (SelectedProduct != null)
            {
                RequestEditProduct?.Invoke(SelectedProduct);
            }
        });

        DeleteProductCommand = new AsyncRelayCommand(async (param) =>
        {
            if (param is Product product)
            {
                string msgConfirm = Loc.IsKurdish 
                    ? $"ئایا دڵنیایت لە سڕینەوەی کاڵای '{product.Name}'؟" 
                    : $"هل أنت متأكد من حذف المادة '{product.Name}'؟";
                string msgTitle = Loc.IsKurdish ? "دڵنیابوونەوە لە سڕینەوە" : "تأكيد الحذف";

                var result = MessageBox.Show(msgConfirm, msgTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    await _productService.DeleteProductAsync(product.Id);
                    await LoadProductsAsync();
                }
            }
        });

        QuickAddStockCommand = new AsyncRelayCommand(async (param) =>
        {
            if (param is Product product)
            {
                product.StockQuantity += 10;
                await _productService.SaveProductAsync(product);
                await LoadProductsAsync();
            }
        });

        BackToMainCommand = new RelayCommand(() => RequestBackToNavigation?.Invoke());
    }

    public async Task InitializeAsync()
    {
        var cats = await _productService.GetCategoriesAsync();
        Categories.Clear();
        string allCatsName = Loc.IsKurdish ? "هەموو پۆلەکان" : "جميع التصنيفات";
        Categories.Add(new Category { Id = Guid.Empty, Name = allCatsName });
        foreach (var c in cats)
        {
            Categories.Add(c);
        }
        SelectedCategory = Categories.FirstOrDefault();

        await LoadProductsAsync();
    }

    public async Task LoadProductsAsync()
    {
        Guid? catId = (SelectedCategory == null || SelectedCategory.Id == Guid.Empty) ? null : SelectedCategory.Id;
        var list = await _productService.GetAllProductsListAsync(SearchQuery, catId);

        Products.Clear();
        decimal totalPurchaseCost = 0;
        decimal totalPieces = 0;
        decimal totalCartons = 0;
        decimal totalSellingVal = 0;

        foreach (var p in list)
        {
            Products.Add(p);
            
            // حساب قيمة الشراء للمادة = الكمية بالمخزن × تكلفة الشراء
            totalPurchaseCost += (p.StockQuantity * p.Cost);
            totalSellingVal += (p.StockQuantity * p.Price);
            totalPieces += p.StockQuantity;
            totalCartons += p.CartonsCount;
        }

        TotalItemsCount = Products.Count;
        TotalStockPurchaseCost = totalPurchaseCost;
        TotalStockPieces = totalPieces;
        TotalStockCartons = totalCartons;
        TotalStockSellingValue = totalSellingVal;
        TotalExpectedProfit = Math.Max(0, totalSellingVal - totalPurchaseCost);
    }

    public void OnAddProductClicked() => RequestAddProduct?.Invoke();
    public void OnEditProductClicked(Product p) => RequestEditProduct?.Invoke(p);
}
