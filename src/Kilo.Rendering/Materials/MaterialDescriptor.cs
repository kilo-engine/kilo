using System.Numerics;

namespace Kilo.Rendering.Materials;

/// <summary>
/// Descriptor for creating a material with configurable properties.
/// </summary>
public sealed class MaterialDescriptor
{
    /// <summary>Base color when no texture is bound (default: white).</summary>
    public Vector4 BaseColor { get; init; } = Vector4.One;

    /// <summary>Optional path to an albedo texture file (PNG/JPG).</summary>
    public string? AlbedoTexturePath { get; init; }

    /// <summary>
    /// Override transparency detection.
    /// <c>null</c> (default) = auto-detect from <see cref="BaseColor"/>.W &lt; 1.0;
    /// <c>true</c> = force transparent; <c>false</c> = force opaque.
    /// </summary>
    public bool? IsTransparent { get; init; }

    /// <summary>Metallic factor (0.0 = dielectric, 1.0 = full metal). Default: 0.0</summary>
    public float Metallic { get; init; } = 0.0f;

    /// <summary>Roughness factor (0.0 = smooth mirror, 1.0 = fully rough). Default: 0.5</summary>
    public float Roughness { get; init; } = 0.5f;

    /// <summary>Optional path to a normal map texture file (PNG/JPG).</summary>
    public string? NormalMapTexturePath { get; init; }

    /// <summary>
    /// Resolves whether the material should be transparent.
    /// If <see cref="IsTransparent"/> is set, uses that value; otherwise auto-detects from alpha channel.
    /// </summary>
    public bool ResolveTransparency() => IsTransparent ?? BaseColor.W < 1.0f;
}
