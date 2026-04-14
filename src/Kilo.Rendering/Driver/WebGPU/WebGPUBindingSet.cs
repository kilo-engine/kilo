using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;

public sealed unsafe class WebGPUBindingSet : IBindingSet
{
    private readonly WgpuApi _wgpu;
    private readonly BindGroup* _bindGroup;
    private bool _disposed;

    internal BindGroup* NativePtr => _bindGroup;
    internal bool HasDynamicOffsets { get; init; }

    internal WebGPUBindingSet(WgpuApi wgpu, BindGroup* bindGroup)
    {
        _wgpu = wgpu;
        _bindGroup = bindGroup;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wgpu.BindGroupRelease(_bindGroup);
    }
}
