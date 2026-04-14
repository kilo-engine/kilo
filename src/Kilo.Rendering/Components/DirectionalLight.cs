using System.Numerics;

namespace Kilo.Rendering;

/// <summary>
/// Directional light component representing a distant light source (like the sun).
/// </summary>
public struct DirectionalLight
{
    /// <summary>Direction the light is pointing.</summary>
    public Vector3 Direction;

    /// <summary>Light color (RGB).</summary>
    public Vector3 Color;

    /// <summary>Light intensity multiplier.</summary>
    public float Intensity;
}
