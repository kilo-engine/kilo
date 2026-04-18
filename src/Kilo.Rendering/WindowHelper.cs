using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Kilo.Rendering;

public static class WindowHelper
{
    public static IWindow CreateWindow(RenderSettings settings)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(settings.Width, settings.Height);
        options.Title = settings.Title;
        options.VSync = settings.VSync;
        options.API = GraphicsAPI.None;
        options.IsContextControlDisabled = true;
        options.ShouldSwapAutomatically = false;
        return Window.Create(options);
    }
}
