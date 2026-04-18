using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Resources;

namespace Kilo.Rendering;

public sealed class RenderContext
{
    public IRenderDriver Driver { get; set; } = null!;
    public ShaderCache ShaderCache { get; set; } = new();
    public PipelineCache PipelineCache { get; set; } = new();
    public MaterialManager MaterialManager { get; } = new();
    public RenderGraph.RenderGraph RenderGraph { get; } = new();
    public IRenderPipeline? SpritePipeline { get; set; }
    public IBuffer? QuadVertexBuffer { get; set; }
    public IBuffer? QuadIndexBuffer { get; set; }
    public IBuffer? UniformBuffer { get; set; }
    public IBindingSet? BindingSet { get; set; }
    public IBuffer? ShadowDataBuffer { get; set; }
    public ISampler? ShadowSampler { get; set; }
    public bool WindowResized { get; set; }

    public List<Mesh> Meshes { get; set; } = [];
    public List<Material> Materials { get; set; } = [];

    // Screenshot support
    public bool ScreenshotRequested { get; set; }
    public bool HasPendingScreenshot { get; set; }
    public IBuffer? ScreenshotBuffer { get; set; }
    public uint ScreenshotAlignedBytesPerRow { get; set; }
    public int ScreenshotWidth { get; set; }
    public int ScreenshotHeight { get; set; }
}
