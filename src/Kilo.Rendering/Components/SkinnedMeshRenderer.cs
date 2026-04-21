using Kilo.Rendering.Driver;

namespace Kilo.Rendering;

/// <summary>
/// Renderer component for skinned (skeletal animated) meshes.
/// Extends MeshRenderer with joint matrix GPU buffer.
/// </summary>
public struct SkinnedMeshRenderer
{
    /// <summary>Handle to the skinned mesh resource.</summary>
    public MeshHandle MeshHandle;

    /// <summary>Handle to the material resource.</summary>
    public MaterialHandle MaterialHandle;

    /// <summary>GPU buffer holding joint matrices (array of mat4x4).</summary>
    public IBuffer? JointMatrixBuffer;

    /// <summary>Binding set for group 4 (joint matrices).</summary>
    public IBindingSet? JointBindingSet;
}
