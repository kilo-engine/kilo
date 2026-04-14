using Kilo.ECS;

namespace Kilo.Rendering;

public sealed class WindowResizeSystem
{
    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        if (!context.WindowResized) return;

        var windowSize = world.GetResource<WindowSize>();
        context.Driver.ResizeSurface(windowSize.Width, windowSize.Height);
    }
}
