using Kilo.Rendering.Driver;

namespace Kilo.Rendering;

/// <summary>
/// Shared skybox rendering state, initialized by SkyboxRenderSystem, used by RenderSystem.
/// </summary>
public sealed class SkyboxState
{
    public IRenderPipeline Pipeline { get; set; } = null!;
    public IBuffer VertexBuffer { get; set; } = null!;
    public IBuffer IndexBuffer { get; set; } = null!;
    public IBuffer CameraBuffer { get; set; } = null!;
    public IBindingSet CameraBinding { get; set; } = null!;
    public IBindingSet TextureBinding { get; set; } = null!;
}
