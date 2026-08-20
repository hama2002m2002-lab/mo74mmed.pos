using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HamoPos.Models;

namespace HamoPos.Services;

public static class A4InvoicePrintService
{
    public static FlowDocument CreateA4SupplierOrderInvoice(SupplierOrder order)
    {
        var storeSettings = StoreSettingsService.Instance.Settings;

        var doc = new FlowDocument
        {
            PageWidth = 820, // standard A4 display width in WPF (approx 96 DPI * 8.27 in)
            PageHeight = 1160,
            PagePadding = new Thickness(40, 35, 40, 35),
            FontFamily = new FontFamily("Segoe UI, Tahoma, Arial"),
            FlowDirection = FlowDirection.RightToLeft,
            Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) // #0F172A
        };

        // 1. TOP HEADER (Store Info & Logo)
        var headerTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 15) };
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(140) }); // Logo
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Store Details
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(200) }); // Invoice Title & Badge

        var headerRowGroup = new TableRowGroup();
        var headerRow = new TableRow();

        // Col 1: Logo
        var logoCell = new TableCell();
        if (!string.IsNullOrWhiteSpace(storeSettings.LogoPath) && File.Exists(storeSettings.LogoPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(storeSettings.LogoPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                var img = new Image
                {
                    Source = bmp,
                    Width = 100,
                    Height = 70,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                logoCell.Blocks.Add(new BlockUIContainer(img));
            }
            catch
            {
                logoCell.Blocks.Add(new Paragraph(new Run("🏢")) { FontSize = 36, Margin = new Thickness(0) });
            }
        }
        else
        {
            var logoBorder = new Border
            {
                Width = 75,
                Height = 75,
                Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
                CornerRadius = new CornerRadius(12),
                Child = new TextBlock
                {
                    Text = "📦",
                    FontSize = 34,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            logoCell.Blocks.Add(new BlockUIContainer(logoBorder));
        }
        headerRow.Cells.Add(logoCell);

        // Col 2: Store Info
        var storeInfoCell = new TableCell();
        var storeNamePara = new Paragraph(new Run(storeSettings.StoreName))
        {
            FontSize = 18,
            FontWeight = FontWeights.Black,
            Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
            Margin = new Thickness(0, 0, 0, 3)
        };
        var taglinePara = new Paragraph(new Run(storeSettings.Tagline))
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var addressPhonePara = new Paragraph
        {
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            Margin = new Thickness(0)
        };
        addressPhonePara.Inlines.Add(new Run($"📍 {storeSettings.Address}\n"));
        addressPhonePara.Inlines.Add(new Run($"📞 {storeSettings.Phone1}   |   {storeSettings.Phone2}"));

        storeInfoCell.Blocks.Add(storeNamePara);
        storeInfoCell.Blocks.Add(taglinePara);
        storeInfoCell.Blocks.Add(addressPhonePara);
        headerRow.Cells.Add(storeInfoCell);

        // Col 3: Invoice Title Box
        var invoiceTitleCell = new TableCell { TextAlignment = TextAlignment.Left };
        var invoiceTypePara = new Paragraph(new Run("فاتورة تسليم وتجهيز"))
        {
            FontSize = 16,
            FontWeight = FontWeights.Black,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            Margin = new Thickness(0, 0, 0, 3)
        };
        var invoiceSubPara = new Paragraph(new Run("Commercial Order Invoice"))
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var statusBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = order.Status == OrderStatus.Delivered ? "✔ تم التسليم والاعتماد" : (order.Status == OrderStatus.InPreparation ? "📦 قيد التجهيز" : "⏳ طلبية معتمدة"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52))
            }
        };

        invoiceTitleCell.Blocks.Add(invoiceTypePara);
        invoiceTitleCell.Blocks.Add(invoiceSubPara);
        invoiceTitleCell.Blocks.Add(new BlockUIContainer(statusBadge));
        headerRow.Cells.Add(invoiceTitleCell);

        headerRowGroup.Rows.Add(headerRow);
        headerTable.RowGroups.Add(headerRowGroup);
        doc.Blocks.Add(headerTable);

        // Divider
        doc.Blocks.Add(new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(0, 0, 0, 1.5)
        });

        // 2. ORDER & CLIENT INFO CARD
        var infoTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 15) };
        infoTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        infoTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var infoRowGroup = new TableRowGroup();
        var infoRow = new TableRow();

        // Right Box: Market / Client
        var clientBox = new TableCell();
        var clientBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 6, 0),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "👤 بيانات العميل / الماركت:", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)), Margin = new Thickness(0, 0, 0, 6) },
                    new TextBlock { Text = $"اسم المحل/الماركت:  {order.MarketName}", FontSize = 12, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 0, 0, 3) },
                    new TextBlock { Text = $"رقم هاتف المحل:  {(string.IsNullOrWhiteSpace(order.MarketPhone) ? "--" : order.MarketPhone)}", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), Margin = new Thickness(0, 0, 0, 3) },
                    new TextBlock { Text = $"العنوان / المنطقة:  {(string.IsNullOrWhiteSpace(order.MarketAddress) ? "--" : order.MarketAddress)}", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) }
                }
            }
        };
        clientBox.Blocks.Add(new BlockUIContainer(clientBorder));
        infoRow.Cells.Add(clientBox);

        // Left Box: Invoice Meta & Sales Rep
        var metaBox = new TableCell();
        var metaBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(6, 0, 0, 0),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "📋 تفاصيل الفاتورة والطلب:", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)), Margin = new Thickness(0, 0, 0, 6) },
                    new TextBlock { Text = $"رقم الوصل / الطلبية:  {order.OrderNumber}", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 0, 0, 3) },
                    new TextBlock { Text = $"تاريخ ووقت الإصدار:  {order.OrderDate:yyyy/MM/dd - hh:mm tt}", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), Margin = new Thickness(0, 0, 0, 3) },
                    new TextBlock { Text = $"المندوب المسؤول:  {order.RepresentativeName}", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) }
                }
            }
        };
        metaBox.Blocks.Add(new BlockUIContainer(metaBorder));
        infoRow.Cells.Add(metaBox);

        infoRowGroup.Rows.Add(infoRow);
        infoTable.RowGroups.Add(infoRowGroup);
        doc.Blocks.Add(infoTable);

        // 3. ITEMS TABLE
        var itemsTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 15) };
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(35) });  // #
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Product Name
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(120) }); // Barcode
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(75) });  // Unit
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(65) });  // Qty
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Unit Price
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(120) }); // Total

        var tableRowGroup = new TableRowGroup();

        // Table Header
        var thRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)) };
        thRow.Cells.Add(CreateHeaderCell("#", 35));
        thRow.Cells.Add(CreateHeaderCell("اسم المادة والوصف", 0, TextAlignment.Right));
        thRow.Cells.Add(CreateHeaderCell("الباركود", 120));
        thRow.Cells.Add(CreateHeaderCell("الوحدة", 75));
        thRow.Cells.Add(CreateHeaderCell("الكمية", 65));
        thRow.Cells.Add(CreateHeaderCell("سعر الوحدة", 110));
        thRow.Cells.Add(CreateHeaderCell("الإجمالي د.ع", 120));
        tableRowGroup.Rows.Add(thRow);

        // Data Rows
        int index = 1;
        decimal grandTotal = 0;

        foreach (var item in order.Items)
        {
            decimal rowTotal = item.Quantity * item.UnitPrice;
            grandTotal += rowTotal;

            bool isEven = index % 2 == 0;
            var row = new TableRow
            {
                Background = isEven ? new SolidColorBrush(Color.FromRgb(248, 250, 252)) : Brushes.White
            };

            string unitDisplay = item.UnitType == "Carton" ? "كرتون" : "قطعة";

            row.Cells.Add(CreateDataCell(index.ToString(), TextAlignment.Center));
            row.Cells.Add(CreateDataCell(item.ProductName, TextAlignment.Right, isBold: true));
            row.Cells.Add(CreateDataCell(string.IsNullOrWhiteSpace(item.Barcode) ? "--" : item.Barcode, TextAlignment.Center, fontSize: 10.5));
            row.Cells.Add(CreateDataCell(unitDisplay, TextAlignment.Center));
            row.Cells.Add(CreateDataCell(item.Quantity.ToString("G29"), TextAlignment.Center, isBold: true));
            row.Cells.Add(CreateDataCell($"{item.UnitPrice:N0} د.ع", TextAlignment.Center));
            row.Cells.Add(CreateDataCell($"{rowTotal:N0} د.ع", TextAlignment.Left, isBold: true));

            tableRowGroup.Rows.Add(row);
            index++;
        }

        itemsTable.RowGroups.Add(tableRowGroup);
        doc.Blocks.Add(itemsTable);

        // 4. TOTALS & SUMMARY SECTION
        var summaryTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 20) };
        summaryTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Notes
        summaryTable.Columns.Add(new TableColumn { Width = new GridLength(280) }); // Financials

        var sumRowGroup = new TableRowGroup();
        var sumRow = new TableRow();

        // Notes Cell
        var notesCell = new TableCell();
        var notesBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 10, 0),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "📝 ملاحظات الطلبية والتوصيل:", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9)), Margin = new Thickness(0, 0, 0, 4) },
                    new TextBlock { Text = string.IsNullOrWhiteSpace(order.Notes) ? "لا توجد ملاحظات إضافية." : order.Notes, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(120, 53, 15)), TextWrapping = TextWrapping.Wrap }
                }
            }
        };
        notesCell.Blocks.Add(new BlockUIContainer(notesBorder));
        sumRow.Cells.Add(notesCell);

        // Totals Box Cell
        var totalsCell = new TableCell();
        var totalsBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = new StackPanel
            {
                Children =
                {
                    new Grid
                    {
                        Children =
                        {
                            new TextBlock { Text = "عدد المواد الإجمالي:", FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), HorizontalAlignment = HorizontalAlignment.Right },
                            new TextBlock { Text = $"{order.Items.Count} مادة", FontSize = 12, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Left }
                        }
                    },
                    new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(0, 6, 0, 6) },
                    new Grid
                    {
                        Children =
                        {
                            new TextBlock { Text = "المجموع النهائي الصافي:", FontSize = 13, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)), HorizontalAlignment = HorizontalAlignment.Right },
                            new TextBlock { Text = $"{grandTotal:N0} د.ع", FontSize = 15, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)), HorizontalAlignment = HorizontalAlignment.Left }
                        }
                    }
                }
            }
        };
        totalsCell.Blocks.Add(new BlockUIContainer(totalsBorder));
        sumRow.Cells.Add(totalsCell);

        sumRowGroup.Rows.Add(sumRow);
        summaryTable.RowGroups.Add(sumRowGroup);
        doc.Blocks.Add(summaryTable);

        // 5. TERMS & CONDITIONS
        var termsPara = new Paragraph
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(0, 0, 0, 25),
            TextAlignment = TextAlignment.Center
        };
        termsPara.Inlines.Add(new Run($"* {storeSettings.ReceiptFooterNotes}\n"));
        termsPara.Inlines.Add(new Run($"* {storeSettings.A4TermsAndConditions}"));
        doc.Blocks.Add(termsPara);

        // 6. SIGNATURE BOXES
        var signTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 10, 0, 0) };
        signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var signRowGroup = new TableRowGroup();
        var signRow = new TableRow();

        signRow.Cells.Add(CreateSignatureCell("توقيع واستلام العميل / الماركت"));
        signRow.Cells.Add(CreateSignatureCell("توقيع المندوب / السائق"));
        signRow.Cells.Add(CreateSignatureCell("ختم وتوقيع إدارة المخزن"));

        signRowGroup.Rows.Add(signRow);
        signTable.RowGroups.Add(signRowGroup);
        doc.Blocks.Add(signTable);

        return doc;
    }

    private static TableCell CreateHeaderCell(string text, double width = 0, TextAlignment align = TextAlignment.Center)
    {
        var para = new Paragraph(new Run(text))
        {
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            TextAlignment = align,
            Margin = new Thickness(6, 7, 6, 7)
        };
        return new TableCell(para)
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(0, 0, 0.5, 0)
        };
    }

    private static TableCell CreateDataCell(string text, TextAlignment align, bool isBold = false, double fontSize = 11.5)
    {
        var para = new Paragraph(new Run(text))
        {
            FontSize = fontSize,
            FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            TextAlignment = align,
            Margin = new Thickness(6, 6, 6, 6)
        };
        return new TableCell(para)
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
    }

    private static TableCell CreateSignatureCell(string title)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 8, 8, 30),
            Margin = new Thickness(4, 0, 4, 0),
            Child = new TextBlock
            {
                Text = title,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        var cell = new TableCell();
        cell.Blocks.Add(new BlockUIContainer(border));
        return cell;
    }

    public static void PrintA4Invoice(SupplierOrder order)
    {
        try
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var doc = CreateA4SupplierOrderInvoice(order);
                doc.PageWidth = printDialog.PrintableAreaWidth;
                doc.PageHeight = printDialog.PrintableAreaHeight;

                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                printDialog.PrintDocument(paginator, $"فاتورة A4 - طلبية {order.OrderNumber}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"تعذر إتمام الطباعة: {ex.Message}", "خطأ في الطباعة", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
