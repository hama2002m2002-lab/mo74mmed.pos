using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class AddEditProductViewModel : BaseViewModel
{
    private readonly IProductService _productService;

    public Guid ProductId { get; set; } = Guid.Empty;
    public string DialogTitle => ProductId == Guid.Empty ? "إضافة مادة جديدة للمخزن" : "تعديل بيانات المادة";

    private string _barcode = string.Empty;
    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private decimal _cost;
    public decimal Cost
    {
        get => _cost;
        set => SetProperty(ref _cost, value);
    }

    private decimal _price;
    public decimal Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }

    private decimal _stockQuantity = 1.0m;
    public decimal StockQuantity
    {
        get => _stockQuantity;
        set => SetProperty(ref _stockQuantity, value);
    }

    private decimal _minStockAlert = 5.0m;
    public decimal MinStockAlert
    {
        get => _minStockAlert;
        set => SetProperty(ref _minStockAlert, value);
    }

    private string _unit = "قطعة";
    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    private Category? _selectedCategory;
    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ObservableCollection<Category> Categories { get; } = new();

    public ICommand GenerateBarcodeCommand { get; }
    public ICommand SaveCommand { get; }

    public event Action<bool>? RequestClose;

    public AddEditProductViewModel(IProductService productService, Product? existingProduct = null)
    {
        _productService = productService;

        GenerateBarcodeCommand = new RelayCommand(GenerateRandomBarcode);

        SaveCommand = new AsyncRelayCommand(async () => await SaveAsync());

        if (existingProduct != null)
        {
            ProductId = existingProduct.Id;
            Barcode = existingProduct.Barcode;
            Name = existingProduct.Name;
            Cost = existingProduct.Cost;
            Price = existingProduct.Price;
            StockQuantity = existingProduct.StockQuantity;
            MinStockAlert = existingProduct.MinStockAlert;
            Unit = existingProduct.Unit;
        }
        else
        {
            GenerateRandomBarcode();
        }
    }

    public async Task InitializeAsync()
    {
        var list = await _productService.GetCategoriesAsync();
        Categories.Clear();
        foreach (var c in list)
        {
            Categories.Add(c);
        }

        if (SelectedCategory == null && Categories.Count > 0)
        {
            SelectedCategory = Categories[0];
        }
    }

    private void GenerateRandomBarcode()
    {
        var random = new Random();
        Barcode = "628" + random.Next(1000000, 9999999).ToString();
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Barcode))
        {
            ErrorMessage = "يرجى إدخال أو توليد باركود للمادة.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "يرجى كتابة اسم المادة.";
            return;
        }

        if (Price <= 0)
        {
            ErrorMessage = "يجب أن يكون سعر البيع أكبر من صفر.";
            return;
        }

        ErrorMessage = string.Empty;

        var product = new Product
        {
            Id = ProductId,
            Barcode = Barcode.Trim(),
            Name = Name.Trim(),
            CategoryId = SelectedCategory?.Id,
            Cost = Cost,
            Price = Price,
            StockQuantity = StockQuantity,
            MinStockAlert = MinStockAlert,
            Unit = string.IsNullOrWhiteSpace(Unit) ? "قطعة" : Unit.Trim(),
            TaxRate = 0.0m,
            IsActive = true
        };

        bool saved = await _productService.SaveProductAsync(product);
        if (saved)
        {
            RequestClose?.Invoke(true);
        }
        else
        {
            ErrorMessage = "فشل حفظ المنتج، تأكد من عدم تكرار الباركود.";
        }
    }
}
