using System;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;

namespace HamoPos.Services;

/// <summary>
/// كلاس مخصص ومستقل للتعامل مع خزنة النقدية (Cash Drawer)
/// يدعم إرسال أوامر ESC/POS المباشرة عبر:
/// 1. منفذ طابعة الإيصالات المثبتة على الويندوز (Raw Spooler API)
/// 2. المنفذ التسلسلي (Serial / COM Port)
/// </summary>
public class CashDrawerService
{
    #region Win32 Raw Print Spooler APIs

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pDataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    #endregion

    #region Standard ESC/POS Cash Drawer Command Bytes

    // أمر النبضة القياسي ESC/POS لفتح الدرج 1 (Pin 2): ESC p 0 25 250
    public static readonly byte[] DrawerPin2Command = new byte[] { 27, 112, 0, 25, 250 };

    // أمر النبضة القياسي ESC/POS لفتح الدرج 2 (Pin 5): ESC p 1 25 250
    public static readonly byte[] DrawerPin5Command = new byte[] { 27, 112, 1, 25, 250 };

    // أمر الجرس القديم BEL
    public static readonly byte[] DrawerBelCommand = new byte[] { 7 };

    #endregion

    /// <summary>
    /// فتح الدرج عبر طابعة الإيصالات (Windows Printer Name)
    /// </summary>
    /// <param name="printerName">اسم الطابعة المعرفة في نظام الويندوز</param>
    /// <param name="commandBytes">أوامر البايت (افتراضياً ESC p 0 25 250)</param>
    /// <returns>True إذا نجح الإرسال للطابعة</returns>
    public static bool OpenViaPrinter(string printerName, byte[]? commandBytes = null)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return false;

        commandBytes ??= DrawerPin2Command;

        IntPtr pUnmanagedBytes = IntPtr.Zero;
        IntPtr hPrinter = IntPtr.Zero;

        try
        {
            // حجز ذاكرة غير مدارة للبايتات
            pUnmanagedBytes = Marshal.AllocCoTaskMem(commandBytes.Length);
            Marshal.Copy(commandBytes, 0, pUnmanagedBytes, commandBytes.Length);

            if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
            {
                return false;
            }

            var di = new DOCINFOA
            {
                pDocName = "POS_Drawer_Pulse",
                pDataType = "RAW"
            };

            if (StartDocPrinter(hPrinter, 1, di))
            {
                if (StartPagePrinter(hPrinter))
                {
                    WritePrinter(hPrinter, pUnmanagedBytes, commandBytes.Length, out _);
                    EndPagePrinter(hPrinter);
                }
                EndDocPrinter(hPrinter);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CashDrawerService] Error opening via printer: {ex.Message}");
            return false;
        }
        finally
        {
            if (pUnmanagedBytes != IntPtr.Zero)
                Marshal.FreeCoTaskMem(pUnmanagedBytes);

            if (hPrinter != IntPtr.Zero)
                ClosePrinter(hPrinter);
        }
    }

    /// <summary>
    /// فتح الدرج المتصل مباشرة بمنفذ تسلسلي (Serial / COM Port)
    /// </summary>
    /// <param name="portName">اسم المنفذ مثل COM1, COM2, COM3</param>
    /// <param name="baudRate">معدل الباود (افتراضياً 9600)</param>
    /// <param name="commandBytes">أوامر البايت</param>
    /// <returns>True إذا تم الإرسال بنجاح</returns>
    public static bool OpenViaSerialPort(string portName, int baudRate = 9600, byte[]? commandBytes = null)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return false;

        commandBytes ??= DrawerPin2Command;

        try
        {
            using var serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                WriteTimeout = 2000,
                ReadTimeout = 2000
            };

            serialPort.Open();
            serialPort.Write(commandBytes, 0, commandBytes.Length);
            serialPort.Close();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CashDrawerService] Error opening via Serial Port {portName}: {ex.Message}");
            return false;
        }
    }
}
