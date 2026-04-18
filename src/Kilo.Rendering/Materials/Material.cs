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
}
