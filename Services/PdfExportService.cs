using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using HamoPos.Models;

namespace HamoPos.Services;

public static class PdfExportService
{
    public static string InvoicesDirectory
    {
        get
        {
            string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string folder = Path.Combine(myDocs, "7amoPos_Invoices");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }
    }

    public static string ExportA4InvoiceToPdf(SupplierOrder order, bool openAfterSave = true)
    {
        try
        {
            var storeSettings = StoreSettingsService.Instance.Settings;
            string cleanMarket = string.Join("_", (order.MarketName ?? "عميل").Split(Path.GetInvalidFileNameChars()));
            string cleanNum = string.Join("_", (order.OrderNumber ?? "ORD").Split(Path.GetInvalidFileNameChars()));
            string fileName = $"وصل_طلبية_{cleanNum}_{cleanMarket}.pdf";
            string pdfPath = Path.Combine(InvoicesDirectory, fileName);
            string htmlPath = Path.Combine(InvoicesDirectory, $"temp_{cleanNum}.html");

            string htmlContent = GenerateInvoiceHtml(order, storeSettings);
            File.WriteAllText(htmlPath, htmlContent, Encoding.UTF8);

            // Generate PDF via Microsoft Edge Headless CLI
            string? edgePath = GetEdgeExecutablePath();
            bool pdfCreated = false;

            if (!string.IsNullOrEmpty(edgePath) && File.Exists(edgePath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = edgePath,
                    Arguments = $"--headless --disable-gpu --run-all-compositor-stages-before-draw --print-to-pdf=\"{pdfPath}\" \"{htmlPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(10000);
                    if (File.Exists(pdfPath))
                    {
                        pdfCreated = true;
                    }
                }
            }

            // Cleanup temp HTML
            try { File.Delete(htmlPath); } catch { }

            if (!pdfCreated)
            {
                // Fallback: If Edge is unavailable, save as HTML invoice and notify
                string fallbackHtmlPath = Path.Combine(InvoicesDirectory, $"فاتورة_{cleanNum}_{cleanMarket}.html");
                File.WriteAllText(fallbackHtmlPath, htmlContent, Encoding.UTF8);
                if (openAfterSave)
                {
                    Process.Start(new ProcessStartInfo { FileName = fallbackHtmlPath, UseShellExecute = true });
                }
                return fallbackHtmlPath;
            }

            if (openAfterSave && File.Exists(pdfPath))
            {
                Process.Start(new ProcessStartInfo { FileName = pdfPath, UseShellExecute = true });
            }

            return pdfPath;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"تعذر تصدير الفاتورة كـ PDF: {ex.Message}", "تنبيه PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            return "";
        }
    }

    public static void OpenInvoicesFolder()
    {
        try
        {
            string folder = InvoicesDirectory;
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch { }
    }

    private static string? GetEdgeExecutablePath()
    {
        string[] candidates = {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\Application\msedge.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public static string GenerateInvoiceHtml(SupplierOrder order, StoreSettings store)
    {
        string logoHtml = "";
        if (!string.IsNullOrWhiteSpace(store.LogoPath) && File.Exists(store.LogoPath))
        {
            try
            {
                byte[] imgBytes = File.ReadAllBytes(store.LogoPath);
                string b64 = Convert.ToBase64String(imgBytes);
                string ext = Path.GetExtension(store.LogoPath).ToLower().Replace(".", "");
                if (ext == "jpg") ext = "jpeg";
                logoHtml = $"<img src='data:image/{ext};base64,{b64}' style='max-height: 75px; max-width: 130px; object-fit: contain;'>";
            }
            catch { }
        }

        if (string.IsNullOrEmpty(logoHtml))
        {
            logoHtml = "<div style='font-size: 38px;'>📦</div>";
        }

        var sbItems = new StringBuilder();
        int seq = 1;
        foreach (var item in order.Items)
        {
            string unitLabel = item.UnitType == "Carton" ? "📦 كرتون" : (item.UnitType == "Wholesale" ? "جملة" : "قطعة");
            sbItems.Append($@"
                <tr>
                    <td style='text-align: center;'>{seq++}</td>
                    <td style='text-align: right; font-weight: bold;'>{item.ProductName}</td>
                    <td style='text-align: center; color: #64748b; font-size: 11px;'>{item.Barcode}</td>
                    <td style='text-align: center;'>{unitLabel}</td>
                    <td style='text-align: center; font-weight: bold;'>{item.Quantity:N0}</td>
                    <td style='text-align: left;'>{item.UnitPrice:N0} د.ع</td>
                    <td style='text-align: left; font-weight: bold; color: #0f766e;'>{item.TotalPrice:N0} د.ع</td>
                </tr>
            ");
        }

        string statusAr = order.Status switch
        {
            OrderStatus.Pending => "قيد الانتظار",
            OrderStatus.InPreparation => "جاري التجهيز",
            OrderStatus.Delivered => "تم التوصيل والتسليم",
            _ => order.Status.ToString()
        };

        string statusBg = order.Status == OrderStatus.Delivered ? "#065f46" : (order.Status == OrderStatus.InPreparation ? "#1e40af" : "#854d0e");

        return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
    <meta charset='UTF-8'>
    <title>فاتورة وصل طلبية - {order.OrderNumber}</title>
    <style>
        @page {{
            size: A4 portrait;
            margin: 15mm 12mm 15mm 12mm;
        }}
        * {{
            box-sizing: border-box;
            font-family: 'Segoe UI', Tahoma, Arial, sans-serif;
            margin: 0;
            padding: 0;
        }}
        body {{
            background: #ffffff;
            color: #0f172a;
            padding: 10px;
            font-size: 13px;
            line-height: 1.4;
        }}
        .invoice-box {{
            width: 100%;
            max-width: 800px;
            margin: auto;
            border: 1.5px solid #cbd5e1;
            border-radius: 12px;
            padding: 24px;
            background: #ffffff;
        }}
        .header-table {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 20px;
            border-bottom: 2px solid #0284c7;
            padding-bottom: 15px;
        }}
        .header-table td {{
            vertical-align: middle;
        }}
        .store-name {{
            font-size: 20px;
            font-weight: 900;
            color: #0284c7;
            margin-bottom: 4px;
        }}
        .store-tagline {{
            font-size: 12px;
            color: #64748b;
            margin-bottom: 6px;
        }}
        .store-contact {{
            font-size: 11.5px;
            color: #334155;
        }}
        .invoice-badge {{
            background: #0284c7;
            color: white;
            padding: 8px 14px;
            border-radius: 8px;
            text-align: center;
            display: inline-block;
        }}
        .info-grid {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 20px;
            background: #f8fafc;
            border-radius: 8px;
            border: 1px solid #e2e8f0;
        }}
        .info-grid td {{
            padding: 9px 12px;
            border: 1px solid #e2e8f0;
            font-size: 12px;
        }}
        .info-label {{
            color: #64748b;
            font-weight: bold;
            width: 18%;
        }}
        .info-val {{
            font-weight: bold;
            color: #0f172a;
        }}
        .items-table {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 20px;
        }}
        .items-table th {{
            background: #0f172a;
            color: white;
            padding: 10px 8px;
            font-size: 12px;
            font-weight: bold;
            border: 1px solid #0f172a;
        }}
        .items-table td {{
            padding: 8px 8px;
            border: 1px solid #e2e8f0;
            font-size: 12px;
        }}
        .items-table tr:nth-child(even) {{
            background: #f8fafc;
        }}
        .totals-table {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 20px;
        }}
        .totals-table td {{
            padding: 8px 12px;
            font-size: 13px;
        }}
        .grand-total-row {{
            background: #0284c7;
            color: white;
            font-size: 15px;
            font-weight: 900;
        }}
        .terms-box {{
            background: #f1f5f9;
            border: 1px dashed #94a3b8;
            border-radius: 8px;
            padding: 10px 14px;
            font-size: 11px;
            color: #475569;
            margin-bottom: 25px;
        }}
        .signatures-table {{
            width: 100%;
            border-collapse: collapse;
            margin-top: 10px;
        }}
        .sig-box {{
            border: 1.5px solid #cbd5e1;
            border-radius: 8px;
            height: 75px;
            padding: 8px;
            text-align: center;
            font-size: 11px;
            color: #475569;
            font-weight: bold;
        }}
    </style>
</head>
<body>
    <div class='invoice-box'>
        <!-- 1. HEADER -->
        <table class='header-table'>
            <tr>
                <td style='width: 130px; text-align: right;'>
                    {logoHtml}
                </td>
                <td style='padding-right: 15px;'>
                    <div class='store-name'>{store.StoreName}</div>
                    <div class='store-tagline'>{store.Tagline}</div>
                    <div class='store-contact'>
                        📞 {store.Phone1} {(!string.IsNullOrWhiteSpace(store.Phone2) ? " | " + store.Phone2 : "")}
                        <br>
                        📍 {store.Address}
                    </div>
                </td>
                <td style='text-align: left; width: 220px;'>
                    <div class='invoice-badge'>
                        <div style='font-size: 14px; font-weight: 900;'>فاتورة ووصل توريد A4</div>
                        <div style='font-size: 11px; margin-top: 2px;'>{order.OrderNumber}</div>
                    </div>
                    <div style='margin-top: 6px; font-size: 11px; color: #64748b;'>
                        حالة الطلبية: <span style='background: {statusBg}; color: white; padding: 2px 8px; border-radius: 4px; font-weight: bold;'>{statusAr}</span>
                    </div>
                </td>
            </tr>
        </table>

        <!-- 2. ORDER & CLIENT INFO -->
        <table class='info-grid'>
            <tr>
                <td class='info-label'>ماركت / العميل:</td>
                <td class='info-val' style='color: #0284c7; font-size: 13.5px;'>{order.MarketName}</td>
                <td class='info-label'>رقم الطلبية:</td>
                <td class='info-val'>{order.OrderNumber}</td>
            </tr>
            <tr>
                <td class='info-label'>هاتف العميل:</td>
                <td class='info-val'>{(!string.IsNullOrWhiteSpace(order.MarketPhone) ? order.MarketPhone : "--")}</td>
                <td class='info-label'>تاريخ الطلب:</td>
                <td class='info-val'>{order.OrderDate:yyyy/MM/dd - hh:mm tt}</td>
            </tr>
            <tr>
                <td class='info-label'>عنوان التوصيل:</td>
                <td class='info-val'>{(!string.IsNullOrWhiteSpace(order.MarketAddress) ? order.MarketAddress : "--")}</td>
                <td class='info-label'>المندوب المسؤول:</td>
                <td class='info-val'>{order.RepresentativeName}</td>
            </tr>
        </table>

        <!-- 3. ITEMS TABLE -->
        <table class='items-table'>
            <thead>
                <tr>
                    <th style='width: 35px;'>ت</th>
                    <th>اسم المادة / الصنف</th>
                    <th style='width: 120px;'>الباركود</th>
                    <th style='width: 75px;'>الوحدة</th>
                    <th style='width: 65px;'>الكمية</th>
                    <th style='width: 95px;'>السعر المفرد</th>
                    <th style='width: 110px;'>الإجمالي</th>
                </tr>
            </thead>
            <tbody>
                {sbItems}
            </tbody>
        </table>

        <!-- 4. TOTALS & SUMMARY -->
        <table class='totals-table'>
            <tr>
                <td style='width: 55%; vertical-align: top;'>
                    {(!string.IsNullOrWhiteSpace(order.Notes) ? $@"
                        <div style='background: #fffbeb; border: 1px solid #fef3c7; border-radius: 6px; padding: 8px 12px; font-size: 11.5px;'>
                            <b style='color: #b45309;'>📝 ملاحظات الطلبية والتوصيل:</b> {order.Notes}
                        </div>
                    " : "")}
                </td>
                <td style='width: 45%;'>
                    <table style='width: 100%; border-collapse: collapse; border: 1px solid #cbd5e1; border-radius: 8px;'>
                        <tr>
                            <td style='padding: 7px 10px; border-bottom: 1px solid #e2e8f0;'>المجموع الإجمالي:</td>
                            <td style='padding: 7px 10px; text-align: left; font-weight: bold; border-bottom: 1px solid #e2e8f0;'>{order.TotalAmount:N0} د.ع</td>
                        </tr>
                        <tr>
                            <td style='padding: 7px 10px; border-bottom: 1px solid #e2e8f0; color: #16a34a;'>الخصم الواصل:</td>
                            <td style='padding: 7px 10px; text-align: left; font-weight: bold; border-bottom: 1px solid #e2e8f0; color: #16a34a;'>0 د.ع</td>
                        </tr>
                        <tr class='grand-total-row'>
                            <td style='padding: 10px 10px;'>الصافي المستحق:</td>
                            <td style='padding: 10px 10px; text-align: left;'>{order.TotalAmount:N0} د.ع</td>
                        </tr>
                    </table>
                </td>
            </tr>
        </table>

        <!-- 5. TERMS & CONDITIONS -->
        <div class='terms-box'>
            <b>📌 الشروط وسياسة الاستلام:</b> {store.A4TermsAndConditions}
            <br>
            <i>{store.ReceiptFooterNotes}</i>
        </div>

        <!-- 6. SIGNATURES -->
        <table class='signatures-table'>
            <tr>
                <td style='width: 32%; padding-left: 10px;'>
                    <div class='sig-box'>
                        استلام وتوقيع العميل (الماركت)
                    </div>
                </td>
                <td style='width: 32%; padding: 0 5px;'>
                    <div class='sig-box'>
                        توقيع المندوب المجهز
                    </div>
                </td>
                <td style='width: 32%; padding-right: 10px;'>
                    <div class='sig-box'>
                        ختم وتصديق إدارة المخزن
                    </div>
                </td>
            </tr>
        </table>
    </div>
</body>
</html>";
    }
}
