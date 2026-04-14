using System.Numerics;

namespace Kilo.Rendering.Driver;

public sealed class ColorAttachmentDescriptor
{
    public ITextureView RenderTarget { get; init; } = null!;
    public DriverLoadAction LoadAction { get; init; } = DriverLoadAction.Clear;
    public DriverStoreAction StoreAction { get; init; } = DriverStoreAction.Store;
    public Vector4? ClearColor { get; init; }
}

public sealed class DepthStencilAttachmentDescriptor
{
    public ITextureView View { get; init; } = null!;
    public DriverLoadAction DepthLoadAction { get; init; } = DriverLoadAction.Clear;
    public DriverStoreAction DepthStoreAction { get; init; } = DriverStoreAction.Store;
    public float? ClearDepth { get; init; } = 1.0f;
}

public sealed class RenderPassDescriptor
{
    public ColorAttachmentDescriptor[] ColorAttachments { get; init; } = [];
    public DepthStencilAttachmentDescriptor? DepthStencilAttachment { get; init; }
}

public sealed class TextureCopyRegion
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int DepthOrArrayLayers { get; init; } = 1;
}
