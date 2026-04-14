using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

class Program
{
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: ScreenshotTool <hwnd> <outputPath> [--screen]");
            return;
        }

        IntPtr hwnd = (IntPtr)long.Parse(args[0]);
        string outputPath = args[1];
        bool useScreen = args.Length > 2 && args[2] == "--screen";

        if (!IsWindow(hwnd))
        {
            Console.WriteLine($"Invalid window handle: {hwnd}");
            return;
        }

        ShowWindow(hwnd, 9); // SW_RESTORE
        Thread.Sleep(500);
        SetForegroundWindow(hwnd);
        Thread.Sleep(500);

        RECT rect;
        GetWindowRect(hwnd, out rect);
        int w = rect.Right - rect.Left;
        int h = rect.Bottom - rect.Top;
        Console.WriteLine($"Window: {rect.Left},{rect.Top} - {rect.Right},{rect.Bottom} ({w}x{h})");

        var bmp = new Bitmap(w, h);
        var gfx = Graphics.FromImage(bmp);

        if (useScreen)
        {
            // Copy from screen DC (captures actual rendered content including WebGPU/Vulkan)
            gfx.CopyFromScreen(new Point(rect.Left, rect.Top), Point.Empty, new Size(w, h));
        }
        else
        {
            // PrintWindow may not capture GPU-rendered content
            var hdc = gfx.GetHdc();
            PrintWindow(hwnd, hdc, 2); // PW_RENDERFULLCONTENT
            gfx.ReleaseHdc(hdc);
        }

        gfx.Dispose();
        bmp.Save(outputPath, ImageFormat.Png);
        bmp.Dispose();
        Console.WriteLine($"Screenshot saved to {outputPath}");
    }

    [DllImport("user32.dll")]
    static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
}
