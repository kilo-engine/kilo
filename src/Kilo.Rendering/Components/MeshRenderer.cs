namespace Kilo.Rendering;

/// <summary>
/// Mesh renderer component referencing mesh and material resources.
/// </summary>
public struct MeshRenderer
{
    /// <summary>Handle/index to the mesh resource.</summary>
    public int MeshHandle;

    /// <summary>Handle/index to the material resource.</summary>
    public int MaterialHandle;
}
