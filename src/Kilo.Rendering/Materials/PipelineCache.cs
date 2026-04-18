using Kilo.Rendering.Driver;

namespace Kilo.Rendering.Materials;

/// <summary>
/// Key used to identify a cached render pipeline.
/// </summary>
public readonly record struct PipelineCacheKey
{
    public required string VertexShaderSource { get; init; }
    public required string VertexShaderEntryPoint { get; init; }
    public required string FragmentShaderSource { get; init; }
    public required string FragmentShaderEntryPoint { get; init; }
    public required DriverPrimitiveTopology Topology { get; init; }
    public required int SampleCount { get; init; }
    public required VertexBufferLayout[] VertexBuffers { get; init; }
    public required ColorTargetDescriptor[] ColorTargets { get; init; }
    public required DepthStencilStateDescriptor? DepthStencil { get; init; }

    public override int GetHashCode()
    {
        HashCode hc = new();
        hc.Add(VertexShaderSource);
        hc.Add(VertexShaderEntryPoint);
        hc.Add(FragmentShaderSource);
        hc.Add(FragmentShaderEntryPoint);
        hc.Add((int)Topology);
        hc.Add(SampleCount);
        foreach (var vb in VertexBuffers)
        {
            hc.Add(vb.ArrayStride);
            foreach (var attr in vb.Attributes)
            {
                hc.Add(attr.ShaderLocation);
                hc.Add((int)attr.Format);
                hc.Add(attr.Offset);
            }
        }
        foreach (var ct in ColorTargets)
        {
            hc.Add((int)ct.Format);
            if (ct.Blend is not null)
            {
                hc.Add((int)ct.Blend.Color.SrcFactor);
                hc.Add((int)ct.Blend.Color.DstFactor);
                hc.Add((int)ct.Blend.Color.Operation);
                hc.Add((int)ct.Blend.Alpha.SrcFactor);
                hc.Add((int)ct.Blend.Alpha.DstFactor);
                hc.Add((int)ct.Blend.Alpha.Operation);
            }
        }
        if (DepthStencil is not null)
        {
            hc.Add((int)DepthStencil.Format);
            hc.Add(DepthStencil.DepthWriteEnabled);
            hc.Add((int)DepthStencil.DepthCompare);
        }
        return hc.ToHashCode();
    }
}

/// <summary>
/// Caches render pipelines to avoid recreating them every frame.
/// </summary>
public sealed class PipelineCache
{
    private readonly Dictionary<int, IRenderPipeline> _pipelines = new();

    public IRenderPipeline GetOrCreate(IRenderDriver driver, PipelineCacheKey key, Func<IRenderPipeline> factory)
    {
        int hash = key.GetHashCode();
        if (!_pipelines.TryGetValue(hash, out var pipeline))
        {
            pipeline = factory();
            _pipelines[hash] = pipeline;
        }
        return pipeline;
    }

    public void Clear()
    {
        foreach (var pipeline in _pipelines.Values)
            pipeline.Dispose();
        _pipelines.Clear();
    }
}
