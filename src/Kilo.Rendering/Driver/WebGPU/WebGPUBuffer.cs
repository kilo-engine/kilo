using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;

public sealed unsafe class WebGPUBuffer : IBuffer
{
    private readonly WgpuApi _wgpu;
    private readonly Device* _device;
    private readonly WgpuBuffer* _buffer;
    private bool _disposed;

    internal WgpuBuffer* NativePtr => _buffer;

    public nuint Size { get; }

    internal WebGPUBuffer(WgpuApi wgpu, Device* device, WgpuBuffer* buffer, nuint size)
    {
        _wgpu = wgpu;
        _device = device;
        _buffer = buffer;
        Size = size;
    }

    public unsafe void UploadData<T>(ReadOnlySpan<T> data, nuint offset = 0) where T : unmanaged
    {
        var queue = _wgpu.DeviceGetQueue(_device);
        fixed (T* ptr = data)
        {
            _wgpu.QueueWriteBuffer(queue, _buffer, offset, ptr, (nuint)(data.Length * sizeof(T)));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wgpu.BufferRelease(_buffer);
    }
}
