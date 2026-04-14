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
}
