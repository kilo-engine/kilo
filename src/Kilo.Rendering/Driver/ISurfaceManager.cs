namespace Kilo.Rendering.Driver;

/// <summary>
/// Manages the render surface (swapchain) configuration and resizing.
/// </summary>
public interface ISurfaceManager
{
    void ConfigureSurface(int width, int height);
    void ResizeSurface(int width, int height);
}
