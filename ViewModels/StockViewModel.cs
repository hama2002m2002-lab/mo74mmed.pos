using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class StockViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _ = LoadStockAsync();
            }
        }
    }

    private int _totalItemsCount;
    public int TotalItemsCount
    {
        get => _totalItemsCount;
        set => SetProperty(ref _totalItemsCount, value);
    }

    private decimal _totalPiecesStock;
    public decimal TotalPiecesStock
    {
        get => _totalPiecesStock;
        set => SetProperty(ref _totalPiecesStock, value);
    }

    private decimal _totalCartonsStock;
    public decimal TotalCartonsStock
    {
        get => _totalCartonsStock;
        set => SetProperty(ref _totalCartonsStock, value);
    }

    public ObservableCollection<Product> StockList { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand EditProductCommand { get; }

    public event Action<Product>? RequestEditProduct;

    public StockViewModel()
    {
        _context = new AppDbContext();
        _productService = new ProductService(_context);
        RefreshCommand = new AsyncRelayCommand(async () => await LoadStockAsync());
        EditProductCommand = new RelayCommand(p =>
        {
            if (p is Product prod)
            {
                RequestEditProduct?.Invoke(prod);
            }
        });
    }

    public async Task InitializeAsync()
    {
        await LoadStockAsync();
    }

    public async Task LoadStockAsync()
    {
        var list = await _productService.GetAllProductsListAsync(SearchQuery);
        StockList.Clear();
        decimal totalPieces = 0;
        decimal totalCartons = 0;

        foreach (var p in list)
        {
            StockList.Add(p);
            totalPieces += p.StockQuantity;
            totalCartons += p.CartonsCount;
        }

        TotalItemsCount = list.Count;
        TotalPiecesStock = totalPieces;
        TotalCartonsStock = totalCartons;
    }
}
