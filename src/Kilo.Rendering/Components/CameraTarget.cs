namespace Kilo.Rendering;

/// <summary>
/// Specifies what a camera renders to.
/// </summary>
public enum CameraTarget
{
    /// <summary>Renders to the swapchain backbuffer (screen).</summary>
    Screen,
    /// <summary>Renders to an offscreen <see cref="Scene.RenderTexture"/>.</summary>
    RenderTexture,
}
