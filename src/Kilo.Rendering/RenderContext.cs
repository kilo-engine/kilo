using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Materials;

namespace Kilo.Rendering;

/// <summary>
/// Core rendering infrastructure. Subsystem states are registered as independent ECS resources.
/// Mesh and material storage is in <see cref="Scene.RenderResourceStore"/>.
/// </summary>
public sealed class RenderContext
{
    public IRenderDriver Driver { get; set; } = null!;
    public ShaderCache ShaderCache { get; set; } = new();
    public PipelineCache PipelineCache { get; set; } = new();
    public MaterialManager MaterialManager { get; } = new();
    public RenderGraph.RenderGraph RenderGraph { get; } = new();
    public bool WindowResized { get; set; }
}
