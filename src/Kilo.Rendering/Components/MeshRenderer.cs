namespace Kilo.Rendering;

/// <summary>
/// Mesh renderer component referencing mesh and material resources.
/// </summary>
public struct MeshRenderer
{
    /// <summary>Handle to the mesh resource.</summary>
    public MeshHandle MeshHandle;

    /// <summary>Handle to the material resource.</summary>
    public MaterialHandle MaterialHandle;
}
