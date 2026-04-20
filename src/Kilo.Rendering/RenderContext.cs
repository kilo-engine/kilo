using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Particles;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

public sealed class RenderContext
{
    public IRenderDriver Driver { get; set; } = null!;
    public ShaderCache ShaderCache { get; set; } = new();
    public PipelineCache PipelineCache { get; set; } = new();
    public MaterialManager MaterialManager { get; } = new();
    public RenderGraph.RenderGraph RenderGraph { get; } = new();
    public bool WindowResized { get; set; }

    private readonly List<Mesh> _meshes = [];
    private readonly List<Material> _materials = [];

    public IReadOnlyList<Mesh> Meshes => _meshes;
    public IReadOnlyList<Material> Materials => _materials;

    public int AddMesh(Mesh mesh) { _meshes.Add(mesh); return _meshes.Count - 1; }
    public int AddMaterial(Material material) { _materials.Add(material); return _materials.Count - 1; }

    public SkyboxState Skybox { get; } = new();
    public ScreenshotState Screenshot { get; } = new();
    public SpriteRenderState Sprite { get; } = new();
    public PostProcessState PostProcess { get; } = new();
    public ParticleSystemState Particles { get; } = new();
}
