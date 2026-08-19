using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HamoPos.Services;

public class BarcodeBar
{
    public double Width { get; set; } = 2;
    public Brush Brush { get; set; } = Brushes.Black;
}

public static class BarcodeGeneratorService
{
    /// <summary>
    /// توليد خطوط باركود مبسطة ونقية للعرض والطباعة بدقة عالية
    /// </summary>
    public static List<BarcodeBar> GenerateVisualBarcodeBars(string barcode, double totalWidth = 200)
    {
        var bars = new List<BarcodeBar>();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            barcode = "123456789012";
        }

        string clean = barcode.Trim();
        
        // Guard pattern Start
        bars.Add(new BarcodeBar { Width = 2, Brush = Brushes.Black });
        bars.Add(new BarcodeBar { Width = 2, Brush = Brushes.Transparent });
        bars.Add(new BarcodeBar { Width = 2, Brush = Brushes.Black });

        // Encode digits into varying bar widths
        for (int i = 0; i < clean.Length; i++)
        {
            int val = (int)clean[i];
            int w1 = (val % 3) + 1;
            int w2 = ((val / 3) % 2) + 1;
            int w3 = ((val / 2) % 3) + 1;

            bars.Add(new BarcodeBar { Width = w1 * 1.5, Brush = Brushes.Transparent });
            bars.Add(new BarcodeBar { Width = w2 * 1.5, Brush = Brushes.Black });
            bars.Add(new BarcodeBar { Width = w3 * 1.5, Brush = Brushes.Transparent });
            bars.Add(new BarcodeBar { Width = 2, Brush = Brushes.Black });
        }

        // Guard pattern End
        bars.Add(new BarcodeBar { Width = 2, Brush = Brushes.Black });
        bars.Add(new BarcodeBar { Width = 2, Brush = Brushes.Transparent });
        bars.Add(new BarcodeBar { Width = 2, Brush = Brushes.Black });

        return bars;
    }
}
