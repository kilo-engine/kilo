using Kilo.Rendering.Driver;

namespace Kilo.Rendering.Resources;

/// <summary>
/// GPU mesh resource containing vertex and index buffers.
/// </summary>
public sealed class Mesh
{
    public IBuffer VertexBuffer { get; set; } = null!;
    public IBuffer IndexBuffer { get; set; } = null!;
    public uint IndexCount { get; set; }
    public VertexBufferLayout[] Layouts { get; set; } = [];
}
