using System.Numerics;

namespace Kilo.Rendering;

/// <summary>
/// Controls how a camera clears its render target at the start of each frame.
/// </summary>
public enum CameraClearMode
{
    /// <summary>Render skybox first (default for 3D scene cameras).</summary>
    Skybox,
    /// <summary>Clear to a solid color.</summary>
    Color,
    /// <summary>Don't clear — load previous contents (for overlay cameras).</summary>
    DontClear,
}

/// <summary>
/// Clear settings for a camera's render target.
/// </summary>
public struct CameraClearSettings
{
    public CameraClearMode Mode;
    public Vector4 Color;

    public static CameraClearSettings Skybox => new() { Mode = CameraClearMode.Skybox };
    public static CameraClearSettings SolidColor(Vector4 color) => new() { Mode = CameraClearMode.Color, Color = color };
    public static CameraClearSettings DontClear => new() { Mode = CameraClearMode.DontClear };
}
