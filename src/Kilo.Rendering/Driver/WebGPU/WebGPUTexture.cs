using System.Runtime.CompilerServices;
using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;

public sealed unsafe class WebGPUTexture : ITexture
{
    private readonly WgpuApi _wgpu;
    private readonly Device* _device;
    private readonly Texture* _texture;
    private bool _disposed;

    internal Texture* NativePtr => _texture;

    public int Width { get; }
    public int Height { get; }
    public DriverPixelFormat Format { get; }

    internal WebGPUTexture(WgpuApi wgpu, Texture* texture, int width, int height, DriverPixelFormat format)
    {
        _wgpu = wgpu;
        _device = null;
        _texture = texture;
        Width = width;
        Height = height;
        Format = format;
    }

    internal WebGPUTexture(WgpuApi wgpu, Device* device, Texture* texture, int width, int height, DriverPixelFormat format)
    {
        _wgpu = wgpu;
        _device = device;
        _texture = texture;
        Width = width;
        Height = height;
        Format = format;
    }

    public void UploadData<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        if (_device == null)
            throw new InvalidOperationException("Texture does not support upload (no device reference).");

        var queue = _wgpu.DeviceGetQueue(_device);
        uint srcRowBytes = (uint)(4 * Width);
        uint bytesPerRow = (srcRowBytes + 255u) & ~255u; // WebGPU requires 256-byte alignment

        var extent = new Extent3D
        {
            Width = (uint)Width,
            Height = (uint)Height,
            DepthOrArrayLayers = 1,
        };

        var imageCopyTexture = new ImageCopyTexture
        {
            Texture = _texture,
            MipLevel = 0,
            Origin = new Origin3D { X = 0, Y = 0, Z = 0 },
            Aspect = TextureAspect.All,
        };

        if (bytesPerRow == srcRowBytes)
        {
            // Already aligned — upload directly
            var dataLength = (nuint)(data.Length * sizeof(T));
            fixed (T* ptr = data)
            {
                var imageData = new TextureDataLayout
                {
                    Offset = 0,
                    BytesPerRow = bytesPerRow,
                    RowsPerImage = (uint)Height,
                };
                _wgpu.QueueWriteTexture(queue, in imageCopyTexture, ptr, dataLength, in imageData, in extent);
            }
        }
        else
        {
            // Need to pad rows to 256-byte alignment
            var padded = new byte[bytesPerRow * Height];
            int srcIdx = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < srcRowBytes; x++)
                    padded[y * bytesPerRow + x] = Unsafe.As<T, byte>(ref Unsafe.AsRef(in data[srcIdx++]));
            }
            fixed (byte* ptr = padded)
            {
                var imageData = new TextureDataLayout
                {
                    Offset = 0,
                    BytesPerRow = bytesPerRow,
                    RowsPerImage = (uint)Height,
                };
                _wgpu.QueueWriteTexture(queue, in imageCopyTexture, ptr, (nuint)padded.Length, in imageData, in extent);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wgpu.TextureRelease(_texture);
    }
}
