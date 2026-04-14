using Kilo.Rendering.Driver;

namespace Kilo.Rendering.RenderGraph;

public sealed class RenderGraphBuilder : IDisposable
{
    private readonly RenderGraph _graph = new();

    public RenderGraphBuilder AddRenderPass(string name, Action<PassBuilder> setup, Action<RenderPassExecutionContext> execute)
    {
        _graph.AddPass(name, setup, execute);
        return this;
    }

    public RenderGraphBuilder AddComputePass(string name, Action<ComputePassBuilder> setup, Action<RenderPassExecutionContext> execute)
    {
        _graph.AddComputePass(name, setup, execute);
        return this;
    }

    public RenderGraph Build(IRenderDriver driver)
    {
        _graph.Compile(driver);
        return _graph;
    }

    public RenderGraph GetGraph() => _graph;

    public void Dispose() => _graph.Dispose();
}
