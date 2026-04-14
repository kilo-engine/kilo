namespace Kilo.Rendering.Driver;

public interface ITexture : IDisposable
{
    int Width { get; }
    int Height { get; }
    DriverPixelFormat Format { get; }
    void UploadData<T>(ReadOnlySpan<T> data) where T : unmanaged;
}
