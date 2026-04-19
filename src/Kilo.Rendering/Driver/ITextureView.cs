namespace Kilo.Rendering.Driver;

public interface ITextureView : IDisposable { }

public enum TextureViewDimension
{
    View1D,
    View2D,
    View2DArray,
    View3D,
    ViewCube,
}

public sealed class TextureViewDescriptor
{
    public DriverPixelFormat Format { get; init; }
    public int MipLevelCount { get; init; } = 1;
    public int BaseMipLevel { get; init; } = 0;
    public TextureViewDimension Dimension { get; init; } = TextureViewDimension.View2D;
}
