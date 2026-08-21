using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using HamoPos.Data;
using HamoPos.Models;

namespace HamoPos.Views;

public partial class ReceiptImageViewerWindow : Window
{
    private readonly PurchaseInvoice _invoice;
    public event Action? ImageChanged;

    public ReceiptImageViewerWindow(PurchaseInvoice invoice)
    {
        InitializeComponent();
        _invoice = invoice;
        TxtInvoiceTitle.Text = $"صورة وصل المندوب - فاتورة: {invoice.InvoiceNumber}";
        LoadImage();
    }

    private void LoadImage()
    {
        if (!string.IsNullOrWhiteSpace(_invoice.ReceiptImagePath) && File.Exists(_invoice.ReceiptImagePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(_invoice.ReceiptImagePath, UriKind.Absolute);
                bitmap.EndInit();
                ImgReceipt.Source = bitmap;
                ImageScrollViewer.Visibility = Visibility.Visible;
                EmptyPlaceholder.Visibility = Visibility.Collapsed;
                return;
            }
            catch { }
        }

        ImgReceipt.Source = null;
        ImageScrollViewer.Visibility = Visibility.Collapsed;
        EmptyPlaceholder.Visibility = Visibility.Visible;
    }

    private void AttachImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = $"اختيار صورة وصل المندوب - فاتورة {_invoice.InvoiceNumber}",
            Filter = "ملفات الصور (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|كل الملفات (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string receiptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Receipts");
                if (!Directory.Exists(receiptsDir))
                {
                    Directory.CreateDirectory(receiptsDir);
                }

                string ext = Path.GetExtension(dialog.FileName);
                string newFileName = $"receipt_{_invoice.Id}_{DateTime.Now.Ticks}{ext}";
                string destPath = Path.Combine(receiptsDir, newFileName);

                File.Copy(dialog.FileName, destPath, true);

                _invoice.ReceiptImagePath = destPath;

                using (var db = new AppDbContext())
                {
                    var dbInvoice = db.PurchaseInvoices.Find(_invoice.Id);
                    if (dbInvoice != null)
                    {
                        dbInvoice.ReceiptImagePath = destPath;
                        dbInvoice.UpdatedAt = DateTime.UtcNow;
                        db.SaveChanges();
                    }
                }

                LoadImage();
                ImageChanged?.Invoke();
                MessageBox.Show("✔ تم إرفاق وحفظ صورة وصل المندوب بنجاح!", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ الصورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void DeleteImage_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_invoice.ReceiptImagePath))
        {
            MessageBox.Show("لا توجد صورة لحذفها.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var res = MessageBox.Show("هل أنت متأكد من حذف صورة هذا الوصل؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            _invoice.ReceiptImagePath = null;
            using (var db = new AppDbContext())
            {
                var dbInvoice = db.PurchaseInvoices.Find(_invoice.Id);
                if (dbInvoice != null)
                {
                    dbInvoice.ReceiptImagePath = null;
                    db.SaveChanges();
                }
            }
            LoadImage();
            ImageChanged?.Invoke();
            MessageBox.Show("تم حذف الصورة بنجاح.", "تم الحذف", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
