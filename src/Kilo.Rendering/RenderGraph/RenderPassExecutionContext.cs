using Kilo.Rendering.Driver;

namespace Kilo.Rendering.RenderGraph;

public sealed class RenderPassExecutionContext
{
    private readonly RenderGraph _graph;
    private readonly IRenderDriver _driver;

    public IRenderCommandEncoder Encoder { get; }

    internal RenderPassExecutionContext(RenderGraph graph, IRenderDriver driver, IRenderCommandEncoder encoder)
    {
        _graph = graph;
        _driver = driver;
        Encoder = encoder;
    }

    public ITexture GetTexture(RenderResourceHandle handle) => _graph.GetResolvedTexture(handle);
    public IBuffer GetBuffer(RenderResourceHandle handle) => _graph.GetResolvedBuffer(handle);
    public ITextureView GetTextureView(RenderResourceHandle handle)
    {
        var texture = _graph.GetResolvedTexture(handle);
        return _graph.GetOrCreateTextureView(_driver, handle, texture);
    }
}
