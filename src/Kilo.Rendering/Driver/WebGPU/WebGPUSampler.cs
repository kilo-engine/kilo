using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;

public sealed unsafe class WebGPUSampler : ISampler
{
    private readonly WgpuApi _wgpu;
    private readonly Sampler* _sampler;
    private bool _disposed;

    internal Sampler* NativePtr => _sampler;

    internal WebGPUSampler(WgpuApi wgpu, Sampler* sampler)
    {
        _wgpu = wgpu;
        _sampler = sampler;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wgpu.SamplerRelease(_sampler);
    }
}
