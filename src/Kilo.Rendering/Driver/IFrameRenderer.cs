namespace Kilo.Rendering.Driver;

/// <summary>
/// Controls frame rendering lifecycle: begin, encode, present.
/// </summary>
public interface IFrameRenderer
{
    ITexture GetCurrentSwapchainTexture();
    DriverPixelFormat SwapchainFormat { get; }
    void BeginFrame();
    IRenderCommandEncoder BeginCommandEncoding();
    void EndFrame();
    void Present();
}
