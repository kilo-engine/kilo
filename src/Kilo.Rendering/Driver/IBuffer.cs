namespace Kilo.Rendering.Driver;

public interface IBuffer : IDisposable
{
    nuint Size { get; }
    void UploadData<T>(ReadOnlySpan<T> data, nuint offset = 0) where T : unmanaged;
}
