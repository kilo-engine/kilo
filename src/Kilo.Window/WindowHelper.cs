using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Kilo.Window;

public static class WindowHelper
{
    public static IWindow CreateWindow(int width, int height, string title, bool vsync)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(width, height);
        options.Title = title;
        options.VSync = vsync;
        options.API = GraphicsAPI.None;
        options.IsContextControlDisabled = true;
        options.ShouldSwapAutomatically = false;
        return Silk.NET.Windowing.Window.Create(options);
    }
}
