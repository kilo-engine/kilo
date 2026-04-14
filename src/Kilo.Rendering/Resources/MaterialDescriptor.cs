using System.Numerics;

namespace Kilo.Rendering.Resources;

/// <summary>
/// Descriptor for creating a material with configurable properties.
/// </summary>
public sealed class MaterialDescriptor
{
    /// <summary>Base color when no texture is bound (default: white).</summary>
    public Vector4 BaseColor { get; init; } = Vector4.One;

    /// <summary>Optional path to an albedo texture file (PNG/JPG).</summary>
    public string? AlbedoTexturePath { get; init; }
}
