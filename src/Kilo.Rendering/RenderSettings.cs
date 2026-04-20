namespace Kilo.Rendering;

public enum GraphicsBackend
{
    WebGPU,
    Vulkan,
    Direct3D12,
    Metal,
}

public sealed class RenderSettings
{
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public string Title { get; set; } = "Kilo Engine";
    public bool VSync { get; set; } = true;
    public GraphicsBackend Backend { get; set; } = GraphicsBackend.WebGPU;

    /// <summary>Optional cubemap face paths for skybox (order: +X, -X, +Y, -Y, +Z, -Z). Null = procedural gradient.</summary>
    public string[]? SkyboxFacePaths { get; set; }

    // Post-processing settings
    public bool BloomEnabled { get; set; } = true;
    public float BloomThreshold { get; set; } = 1.0f;
    public float BloomIntensity { get; set; } = 0.5f;
    public bool ToneMappingEnabled { get; set; } = true;
    public bool FxaaEnabled { get; set; } = true;
}
