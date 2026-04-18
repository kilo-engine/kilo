namespace Kilo.Rendering.Driver;

/// <summary>
/// Combined render driver interface for GPU rendering.
/// Inherits specialized sub-interfaces for ISP compliance.
/// </summary>
public interface IRenderDriver : IGraphicsResourceFactory, IFrameRenderer, ISurfaceManager, IBufferReadback, IDisposable
{
}
