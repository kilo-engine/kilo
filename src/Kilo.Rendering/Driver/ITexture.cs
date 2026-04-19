namespace Kilo.Rendering.Driver;

public interface ITexture : IDisposable
{
    int Width { get; }
    int Height { get; }
    DriverPixelFormat Format { get; }
    void UploadData<T>(ReadOnlySpan<T> data) where T : unmanaged;
    void UploadLayer<T>(ReadOnlySpan<T> data, int layer) where T : unmanaged;
}
