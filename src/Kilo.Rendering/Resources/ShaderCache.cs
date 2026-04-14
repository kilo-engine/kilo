using Kilo.Rendering.Driver;

namespace Kilo.Rendering.Resources;

/// <summary>
/// Caches shader modules to avoid recreating them every frame.
/// </summary>
public sealed class ShaderCache
{
    private readonly Dictionary<(string Source, string EntryPoint), IShaderModule> _shaders = new();
    private readonly Dictionary<(string Source, string EntryPoint), IComputeShaderModule> _computeShaders = new();

    public IShaderModule GetOrCreateShader(IRenderDriver driver, string source, string entryPoint)
    {
        var key = (source, entryPoint);
        if (!_shaders.TryGetValue(key, out var shader))
        {
            shader = driver.CreateShaderModule(source, entryPoint);
            _shaders[key] = shader;
        }
        return shader;
    }

    public IComputeShaderModule GetOrCreateComputeShader(IRenderDriver driver, string source, string entryPoint)
    {
        var key = (source, entryPoint);
        if (!_computeShaders.TryGetValue(key, out var shader))
        {
            shader = driver.CreateComputeShaderModule(source, entryPoint);
            _computeShaders[key] = shader;
        }
        return shader;
    }

    public void Clear()
    {
        foreach (var shader in _shaders.Values)
            shader.Dispose();
        foreach (var shader in _computeShaders.Values)
            shader.Dispose();
        _shaders.Clear();
        _computeShaders.Clear();
    }
}
