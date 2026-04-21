using Kilo.Rendering.Materials;
using Kilo.Rendering.Meshes;

namespace Kilo.Rendering.Scene;

/// <summary>
/// Stores mesh and material resources with type-safe handles.
/// Separated from RenderContext to follow Single Responsibility Principle.
/// </summary>
public sealed class RenderResourceStore
{
    private readonly List<Mesh> _meshes = [];
    private readonly List<Material> _materials = [];

    public IReadOnlyList<Mesh> Meshes => _meshes;
    public IReadOnlyList<Material> Materials => _materials;

    public MeshHandle AddMesh(Mesh mesh)
    {
        _meshes.Add(mesh);
        return new MeshHandle(_meshes.Count - 1);
    }

    public MaterialHandle AddMaterial(Material material)
    {
        _materials.Add(material);
        return new MaterialHandle(_materials.Count - 1);
    }

    public Mesh GetMesh(MeshHandle handle)
    {
        if (!handle.IsValid || handle.Value >= _meshes.Count)
            throw new ArgumentOutOfRangeException(nameof(handle), $"Invalid mesh handle: {handle}");
        return _meshes[handle.Value];
    }

    public Material GetMaterial(MaterialHandle handle)
    {
        if (!handle.IsValid || handle.Value >= _materials.Count)
            throw new ArgumentOutOfRangeException(nameof(handle), $"Invalid material handle: {handle}");
        return _materials[handle.Value];
    }
}
