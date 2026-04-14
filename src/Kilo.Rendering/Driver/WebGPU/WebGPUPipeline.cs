using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;

public sealed unsafe class WebGPUPipeline : IRenderPipeline
{
    private readonly WgpuApi _wgpu;
    private readonly RenderPipeline* _pipeline;
    private bool _disposed;

    internal RenderPipeline* NativePtr => _pipeline;

    internal WebGPUPipeline(WgpuApi wgpu, RenderPipeline* pipeline)
    {
        _wgpu = wgpu;
        _pipeline = pipeline;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wgpu.RenderPipelineRelease(_pipeline);
    }
}
