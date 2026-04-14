using System.Numerics;

namespace Kilo.Rendering;

/// <summary>
/// Point light component that emits light in all directions from a position.
/// </summary>
public struct PointLight
{
    /// <summary>Light position in world space.</summary>
    public Vector3 Position;

    /// <summary>Light color (RGB).</summary>
    public Vector3 Color;

    /// <summary>Light intensity multiplier.</summary>
    public float Intensity;

    /// <summary>Maximum range of the light effect.</summary>
    public float Range;
}
