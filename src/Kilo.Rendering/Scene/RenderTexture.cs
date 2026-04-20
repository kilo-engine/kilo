using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;

namespace Kilo.Rendering.Scene;

/// <summary>
/// An offscreen render target that can be used as a camera output
/// and subsequently sampled as a texture in materials or UI.
/// </summary>
public sealed class RenderTexture : IDisposable
{
    public int Width { get; }
    public int Height { get; }
    public DriverPixelFormat Format { get; }

    internal ITexture? ColorTexture;
    public ITexture? Texture => ColorTexture;
    internal ITexture? DepthTexture;

    private int _lastWidth;
    private int _lastHeight;
    private bool _disposed;

    public RenderTexture(int width, int height, DriverPixelFormat format = DriverPixelFormat.RGBA16Float)
    {
        Width = width;
        Height = height;
        Format = format;
    }

    /// <summary>
    /// Ensures GPU textures are created and match the configured dimensions.
    /// Call once per frame before rendering.
    /// </summary>
    public void EnsureResources(IRenderDriver driver)
    {
        if (ColorTexture != null && _lastWidth == Width && _lastHeight == Height)
            return;

        ColorTexture?.Dispose();
        DepthTexture?.Dispose();

        ColorTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = Width,
            Height = Height,
            Format = Format,
            Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
        });

        DepthTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = Width,
            Height = Height,
            Format = DriverPixelFormat.Depth24Plus,
            Usage = TextureUsage.RenderAttachment,
        });

        _lastWidth = Width;
        _lastHeight = Height;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            ColorTexture?.Dispose();
            DepthTexture?.Dispose();
            _disposed = true;
        }
    }
}
