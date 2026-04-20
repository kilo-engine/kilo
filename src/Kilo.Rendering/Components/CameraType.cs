namespace Kilo.Rendering;

/// <summary>
/// Specifies the type of camera, determining which rendering pipeline it uses.
/// </summary>
public enum CameraType
{
    /// <summary>Full 3D scene rendering (meshes, skybox, lights, shadows).</summary>
    Scene,
    /// <summary>2D overlay rendering (sprites, text) — no depth, no post-processing.</summary>
    UIOverlay,
}
