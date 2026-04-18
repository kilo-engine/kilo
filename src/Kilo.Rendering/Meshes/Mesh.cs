using System.Numerics;
using Kilo.Rendering.Driver;

namespace Kilo.Rendering.Meshes;

/// <summary>
/// GPU mesh resource containing vertex and index buffers.
/// </summary>
public sealed class Mesh
{
    public IBuffer VertexBuffer { get; set; } = null!;
    public IBuffer IndexBuffer { get; set; } = null!;
    public uint IndexCount { get; set; }
    public VertexBufferLayout[] Layouts { get; set; } = [];

    /// <summary>
    /// Local space bounding box for culling. Defaults to unit cube if not set.
    /// </summary>
    public (Vector3 Min, Vector3 Max) Bounds { get; set; } = (new Vector3(-0.5f), new Vector3(0.5f));
}
