using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;

public sealed unsafe class WebGPUTextureView : ITextureView
{
    private readonly WgpuApi _wgpu;
    private readonly TextureView* _view;
    private bool _disposed;

    internal TextureView* NativePtr => _view;

    internal WebGPUTextureView(WgpuApi wgpu, TextureView* view)
    {
        _wgpu = wgpu;
        _view = view;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wgpu.TextureViewRelease(_view);
    }
}
