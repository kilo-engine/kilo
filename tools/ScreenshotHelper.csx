#r "System.Drawing"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

public class WinAPI
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}

IntPtr hwnd = (IntPtr)6358366;
WinAPI.ShowWindow(hwnd, 9); // SW_RESTORE
Thread.Sleep(500);
WinAPI.SetForegroundWindow(hwnd);
Thread.Sleep(500);

WinAPI.RECT rect;
WinAPI.GetWindowRect(hwnd, out rect);
int w = rect.Right - rect.Left;
int h = rect.Bottom - rect.Top;
Console.WriteLine($"Window: {rect.Left},{rect.Top} - {rect.Right},{rect.Bottom} ({w}x{h})");

var bmp = new Bitmap(w, h);
var gfx = Graphics.FromImage(bmp);
var hdc = gfx.GetHdc();
WinAPI.PrintWindow(hwnd, hdc, 2); // PW_RENDERFULLCONTENT
gfx.ReleaseHdc(hdc);
gfx.Dispose();
bmp.Save(@"C:\code\kilo\screenshot_renderdemo.png", ImageFormat.Png);
bmp.Dispose();
Console.WriteLine("Screenshot saved to C:\\code\\kilo\\screenshot_renderdemo.png");
