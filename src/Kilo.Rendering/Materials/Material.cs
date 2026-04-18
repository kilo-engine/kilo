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

/// <summary>
/// An instance of a material that can override specific bindings or use a dynamic offset.
/// </summary>
public sealed class MaterialInstance
{
    public Material Parent { get; }
    public int DynamicOffsetIndex { get; set; }
    public Dictionary<int, ITexture>? TextureOverrides { get; set; }

    public MaterialInstance(Material parent)
    {
        Parent = parent;
    }

    public IRenderPipeline Pipeline => Parent.Pipeline;

    public IBindingSet GetBindingSet(int index) => Parent.BindingSets[index];
}
