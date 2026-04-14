using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;

public sealed unsafe class WebGPUShaderModule : IShaderModule
{
    private readonly WgpuApi _wgpu;
    private readonly ShaderModule* _shaderModule;
    private bool _disposed;

    internal ShaderModule* NativePtr => _shaderModule;

    public string EntryPoint { get; }

    internal WebGPUShaderModule(WgpuApi wgpu, ShaderModule* shaderModule, string entryPoint)
    {
        _wgpu = wgpu;
        _shaderModule = shaderModule;
        EntryPoint = entryPoint;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wgpu.ShaderModuleRelease(_shaderModule);
    }
}
