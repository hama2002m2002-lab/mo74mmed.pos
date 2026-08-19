using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class SelectableProductItem : BaseViewModel
{
    public Product Product { get; }

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private int _copiesCount = 1;
    public int CopiesCount
    {
        get => _copiesCount;
        set => SetProperty(ref _copiesCount, Math.Max(1, value));
    }

    public Guid Id => Product.Id;
    public string Name => Product.Name;
    public string Barcode => Product.Barcode;
    public decimal Price => Product.Price;
    public decimal WholesalePrice => Product.WholesalePrice;
    public decimal CartonSellingPrice => Product.CartonSellingPrice;
    public decimal Cost => Product.Cost;
    public decimal StockQuantity => Product.StockQuantity;
    public decimal CartonsCount => Product.CartonsCount;
    public string CategoryName => Product.Category?.Name ?? "عام";
    public string SupplierName => Product.SupplierName ?? "--";

    public bool HasBarcode => !string.IsNullOrWhiteSpace(Barcode) && Barcode.Trim() != "0";

    public List<BarcodeBar> VisualBarcodeBars => BarcodeGeneratorService.GenerateVisualBarcodeBars(string.IsNullOrWhiteSpace(Barcode) ? "123456789012" : Barcode);

    public SelectableProductItem(Product product, bool isSelected = true)
    {
        Product = product;
        _isSelected = isSelected;
        _copiesCount = 1;
    }

    public void UpdateBarcode(string newBarcode)
    {
        Product.Barcode = newBarcode;
        OnPropertyChanged(nameof(Barcode));
        OnPropertyChanged(nameof(HasBarcode));
        OnPropertyChanged(nameof(VisualBarcodeBars));
    }
}

public class PrintingViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;

    #region Tab Navigation

    private string _activeTab = "PriceLabels"; // "PriceLabels" or "NoBarcode"
    public string ActiveTab
    {
        get => _activeTab;
        set
        {
            if (SetProperty(ref _activeTab, value))
            {
                OnPropertyChanged(nameof(IsPriceLabelsTabActive));
                OnPropertyChanged(nameof(IsNoBarcodeTabActive));
                UpdateDisplayedProducts();
            }
        }
    }

    public bool IsPriceLabelsTabActive => ActiveTab == "PriceLabels";
    public bool IsNoBarcodeTabActive => ActiveTab == "NoBarcode";

    #endregion

    #region Search & Category Filter

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _ = RefreshDataAsync();
            }
        }
    }

    public ObservableCollection<Category> Categories { get; } = new();

    private Category? _selectedCategory;
    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                _ = RefreshDataAsync();
            }
        }
    }

    #endregion

    #region Lists & Collections

    public ObservableCollection<SelectableProductItem> DisplayedProducts { get; } = new();
    public List<SelectableProductItem> AllLoadedProducts { get; } = new();

    private SelectableProductItem? _selectedPreviewItem;
    public SelectableProductItem? SelectedPreviewItem
    {
        get => _selectedPreviewItem;
        set => SetProperty(ref _selectedPreviewItem, value);
    }

    #endregion

    #region Label Customization Options

    private string _storeName = "7amo.pos";
    public string StoreName
    {
        get => _storeName;
        set => SetProperty(ref _storeName, value);
    }

    private string _selectedLabelSize = "50x30mm";
    public string SelectedLabelSize
    {
        get => _selectedLabelSize;
        set => SetProperty(ref _selectedLabelSize, value);
    }

    private bool _showStoreName = true;
    public bool ShowStoreName
    {
        get => _showStoreName;
        set => SetProperty(ref _showStoreName, value);
    }

    private bool _showPrice = true;
    public bool ShowPrice
    {
        get => _showPrice;
        set => SetProperty(ref _showPrice, value);
    }

    private bool _showBarcode = true;
    public bool ShowBarcode
    {
        get => _showBarcode;
        set => SetProperty(ref _showBarcode, value);
    }

    private bool _showWholesalePrice = false;
    public bool ShowWholesalePrice
    {
        get => _showWholesalePrice;
        set => SetProperty(ref _showWholesalePrice, value);
    }

    private int _globalCopiesCount = 1;
    public int GlobalCopiesCount
    {
        get => _globalCopiesCount;
        set
        {
            if (SetProperty(ref _globalCopiesCount, Math.Max(1, value)))
            {
                foreach (var item in DisplayedProducts)
                {
                    item.CopiesCount = _globalCopiesCount;
                }
            }
        }
    }

    #endregion

    #region Stats Counters

    private int _totalProductsCount;
    public int TotalProductsCount
    {
        get => _totalProductsCount;
        set => SetProperty(ref _totalProductsCount, value);
    }

    private int _totalUnlabeledCount;
    public int TotalUnlabeledCount
    {
        get => _totalUnlabeledCount;
        set => SetProperty(ref _totalUnlabeledCount, value);
    }

    private int _selectedCount;
    public int SelectedCount
    {
        get => _selectedCount;
        set => SetProperty(ref _selectedCount, value);
    }

    #endregion

    #region Commands

    public ICommand SwitchToPriceLabelsCommand { get; }
    public ICommand SwitchToNoBarcodeCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand UnselectAllCommand { get; }
    public ICommand GenerateSingleBarcodeCommand { get; }
    public ICommand GenerateAllMissingBarcodesCommand { get; }
    public ICommand PrintLabelsCommand { get; }
    public ICommand RefreshCommand { get; }

    #endregion

    public PrintingViewModel()
    {
        _context = new AppDbContext();
        _productService = new ProductService(_context);

        SwitchToPriceLabelsCommand = new RelayCommand(() => ActiveTab = "PriceLabels");
        SwitchToNoBarcodeCommand = new RelayCommand(() => ActiveTab = "NoBarcode");

        SelectAllCommand = new RelayCommand(() =>
        {
            foreach (var item in DisplayedProducts) item.IsSelected = true;
            UpdateSelectedCount();
        });

        UnselectAllCommand = new RelayCommand(() =>
        {
            foreach (var item in DisplayedProducts) item.IsSelected = false;
            UpdateSelectedCount();
        });

        GenerateSingleBarcodeCommand = new AsyncRelayCommand(async (param) =>
        {
            if (param is SelectableProductItem item)
            {
                string newBarcode = await _productService.GenerateUniqueBarcodeAsync("200245");
                var prod = await _context.Products.FindAsync(item.Product.Id);
                if (prod != null)
                {
                    prod.Barcode = newBarcode;
                    prod.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    item.UpdateBarcode(newBarcode);
                    await RefreshDataAsync();
                }
            }
        });

        GenerateAllMissingBarcodesCommand = new AsyncRelayCommand(async () =>
        {
            var missing = await _context.Products.Where(p => string.IsNullOrWhiteSpace(p.Barcode) || p.Barcode == "0").ToListAsync();
            if (missing.Count == 0)
            {
                MessageBox.Show("لا توجد مواد بدون باركود في المخزن، كافة المواد مسجل لها باركود.", "اكتمل الباركود", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var res = MessageBox.Show($"هل ترغب في توليد وحفظ باركود فريد وتلقائي لجميع المواد ({missing.Count} مادة)؟", "تأكيد توليد الباركود", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                foreach (var p in missing)
                {
                    p.Barcode = await _productService.GenerateUniqueBarcodeAsync("200245");
                    p.UpdatedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
                await RefreshDataAsync();
                MessageBox.Show($"تم توليد وتحديث الباركود لـ {missing.Count} مادة بنجاح!", "تم التوليد والحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });

        PrintLabelsCommand = new RelayCommand(PrintLabels);
        RefreshCommand = new AsyncRelayCommand(async () => await RefreshDataAsync());
    }

    public async Task InitializeAsync()
    {
        var cats = await _productService.GetCategoriesAsync();
        Categories.Clear();
        string allCats = Loc.IsKurdish ? "هەموو پۆلەکان" : "جميع التصنيفات";
        Categories.Add(new Category { Id = Guid.Empty, Name = allCats });
        foreach (var c in cats) Categories.Add(c);
        SelectedCategory = Categories.FirstOrDefault();

        await RefreshDataAsync();
    }

    public async Task RefreshDataAsync()
    {
        Guid? catId = (SelectedCategory == null || SelectedCategory.Id == Guid.Empty) ? null : SelectedCategory.Id;
        var allProducts = await _productService.GetAllProductsListAsync(SearchQuery, catId);

        AllLoadedProducts.Clear();
        int unlabeledCount = 0;

        foreach (var p in allProducts)
        {
            var item = new SelectableProductItem(p);
            AllLoadedProducts.Add(item);
            if (!item.HasBarcode)
            {
                unlabeledCount++;
            }
        }

        TotalProductsCount = allProducts.Count;
        TotalUnlabeledCount = unlabeledCount;

        UpdateDisplayedProducts();
    }

    private void UpdateDisplayedProducts()
    {
        DisplayedProducts.Clear();
        var source = (ActiveTab == "NoBarcode")
            ? AllLoadedProducts.Where(p => !p.HasBarcode)
            : AllLoadedProducts;

        foreach (var item in source)
        {
            DisplayedProducts.Add(item);
        }

        if (SelectedPreviewItem == null || !DisplayedProducts.Any(p => p.Id == SelectedPreviewItem.Id))
        {
            SelectedPreviewItem = DisplayedProducts.FirstOrDefault();
        }

        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = DisplayedProducts.Count(i => i.IsSelected);
    }

    private void PrintLabels()
    {
        var targetList = DisplayedProducts.Where(i => i.IsSelected).ToList();
        if (targetList.Count == 0)
        {
            MessageBox.Show("يرجى تحديد مادة واحدة على الأقل للطباعة.", "لم يتم التحديد", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = CreateLabelsFlowDocument(targetList, printDialog.PrintableAreaWidth);
                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, "طباعة ملصقات الأسعار والباركود");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء إرسال أمر الطباعة: {ex.Message}", "خطأ بالطباعة", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private FlowDocument CreateLabelsFlowDocument(List<SelectableProductItem> items, double printableWidth)
    {
        FlowDocument doc = new FlowDocument
        {
            PageWidth = printableWidth > 0 ? printableWidth : 790,
            PagePadding = new Thickness(15),
            FontFamily = new FontFamily("Times New Roman, Arial"),
            FlowDirection = FlowDirection.RightToLeft
        };

        Section section = new Section();
        Table table = new Table();
        table.CellSpacing = 8;

        int columnsCount = SelectedLabelSize switch
        {
            "40x25mm" => 4,
            "50x30mm" => 3,
            "80x50mm" => 2,
            "Roll_80mm" => 1,
            _ => 3
        };

        for (int c = 0; c < columnsCount; c++)
        {
            table.Columns.Add(new TableColumn());
        }

        TableRowGroup rowGroup = new TableRowGroup();
        TableRow? currentRow = null;
        int colIndex = 0;

        foreach (var item in items)
        {
            for (int copy = 0; copy < item.CopiesCount; copy++)
            {
                if (colIndex == 0 || currentRow == null)
                {
                    currentRow = new TableRow();
                    rowGroup.Rows.Add(currentRow);
                }

                BlockUIContainer cardContainer = new BlockUIContainer(CreatePrintableCardVisual(item));
                TableCell cell = new TableCell(cardContainer)
                {
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(2)
                };
                currentRow.Cells.Add(cell);

                colIndex++;
                if (colIndex >= columnsCount)
                {
                    colIndex = 0;
                    currentRow = null;
                }
            }
        }

        table.RowGroups.Add(rowGroup);
        section.Blocks.Add(table);
        doc.Blocks.Add(section);

        return doc;
    }

    private FrameworkElement CreatePrintableCardVisual(SelectableProductItem item)
    {
        Border border = new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8),
            Background = Brushes.White,
            Width = SelectedLabelSize switch
            {
                "40x25mm" => 150,
                "50x30mm" => 210,
                "80x50mm" => 300,
                "Roll_80mm" => 240,
                _ => 210
            }
        };

        StackPanel stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        // 1. Store Name Header
        if (ShowStoreName && !string.IsNullOrWhiteSpace(StoreName))
        {
            TextBlock txtStore = new TextBlock
            {
                Text = StoreName,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DimGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            stack.Children.Add(txtStore);
        }

        // 2. Product Name
        TextBlock txtName = new TextBlock
        {
            Text = item.Name,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        stack.Children.Add(txtName);

        // 3. Price Tag
        if (ShowPrice)
        {
            StackPanel pricePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 4) };
            TextBlock txtPrice = new TextBlock
            {
                Text = item.Price.ToString("N0"),
                FontSize = 18,
                FontWeight = FontWeights.Black,
                Foreground = Brushes.Black
            };
            TextBlock txtCurr = new TextBlock
            {
                Text = " د.ع",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DimGray,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(2, 0, 0, 2)
            };
            pricePanel.Children.Add(txtPrice);
            pricePanel.Children.Add(txtCurr);
            stack.Children.Add(pricePanel);
        }

        // 4. Barcode Visual Lines & Text
        if (ShowBarcode)
        {
            StackPanel barPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Height = 26, Margin = new Thickness(0, 2, 0, 2) };
            foreach (var b in item.VisualBarcodeBars)
            {
                Border bar = new Border
                {
                    Width = b.Width,
                    Background = b.Brush,
                    Margin = new Thickness(0)
                };
                barPanel.Children.Add(bar);
            }
            stack.Children.Add(barPanel);

            TextBlock txtCode = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.Barcode) ? "123456789012" : item.Barcode,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            stack.Children.Add(txtCode);
        }

        border.Child = stack;
        return border;
    }
}
