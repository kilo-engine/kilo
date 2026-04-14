using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;

public sealed unsafe class WebGPUComputePipeline : IComputePipeline
{
    private readonly WgpuApi _wgpu;
    private readonly ComputePipeline* _pipeline;
    private bool _disposed;

    internal ComputePipeline* NativePtr => _pipeline;

    internal WebGPUComputePipeline(WgpuApi wgpu, ComputePipeline* pipeline)
    {
        _wgpu = wgpu;
        _pipeline = pipeline;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wgpu.ComputePipelineRelease(_pipeline);
    }
}
