using System.Numerics;
using Kilo.Rendering.Driver;

namespace Kilo.Rendering.Materials;

/// <summary>
/// Material resource containing pipeline, binding sets, and texture bindings.
/// </summary>
public sealed class Material
{
    public IRenderPipeline Pipeline { get; set; } = null!;
    public IBindingSet[] BindingSets { get; set; } = [];

    /// <summary>Base color when no texture is bound.</summary>
    public Vector4 BaseColor { get; set; } = Vector4.One;

    /// <summary>Whether this material uses a texture.</summary>
    public bool UseTexture => AlbedoTexture != null;

    /// <summary>Optional albedo texture.</summary>
    public ITexture? AlbedoTexture { get; set; }

    /// <summary>Sampler for the albedo texture.</summary>
    public ISampler? AlbedoSampler { get; set; }

    /// <summary>Whether this material uses alpha blending.</summary>
    public bool IsTransparent { get; set; }

    /// <summary>Metallic factor (0.0 = dielectric, 1.0 = full metal).</summary>
    public float Metallic { get; set; } = 0.0f;

    /// <summary>Roughness factor (0.0 = smooth mirror, 1.0 = fully rough).</summary>
    public float Roughness { get; set; } = 0.5f;

    /// <summary>Optional normal map texture.</summary>
    public ITexture? NormalMapTexture { get; set; }

    /// <summary>Whether this material uses a normal map.</summary>
    public bool UseNormalMap => NormalMapTexture != null;
}
