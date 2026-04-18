using Kilo.Rendering.Driver;

namespace Kilo.Rendering.Scene;

public sealed class SpriteRenderState
{
    public IRenderPipeline? Pipeline { get; set; }
    public IBuffer? QuadVertexBuffer { get; set; }
    public IBuffer? QuadIndexBuffer { get; set; }
    public IBuffer? UniformBuffer { get; set; }
    public IBindingSet? BindingSet { get; set; }
}
