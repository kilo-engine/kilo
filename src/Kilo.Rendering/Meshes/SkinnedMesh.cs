using System.Numerics;
using Kilo.Rendering.Driver;

namespace Kilo.Rendering.Meshes;

/// <summary>
/// Skinned vertex: pos(3) + normal(3) + uv(2) + tangent(4) + joints(4 uint) + weights(4 float) = 80 bytes per vertex
/// </summary>
public struct SkinnedVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 UV;
    public Vector4 Tangent;
    public uint Joint0;
    public uint Joint1;
    public uint Joint2;
    public uint Joint3;
    public float Weight0;
    public float Weight1;
    public float Weight2;
    public float Weight3;
}

/// <summary>
/// Helper for building skinned mesh vertex buffers with correct layout.
/// </summary>
public static class SkinnedMesh
{
    public const int BytesPerVertex = 80; // pos(12) + normal(12) + uv(8) + tangent(16) + joints(16) + weights(16)

    public static VertexBufferLayout Layout => new()
    {
        ArrayStride = 80,
        Attributes =
        [
            new VertexAttributeDescriptor { ShaderLocation = 0, Format = VertexFormat.Float32x3, Offset = 0 },
            new VertexAttributeDescriptor { ShaderLocation = 1, Format = VertexFormat.Float32x3, Offset = 12 },
            new VertexAttributeDescriptor { ShaderLocation = 2, Format = VertexFormat.Float32x2, Offset = 24 },
            new VertexAttributeDescriptor { ShaderLocation = 3, Format = VertexFormat.Float32x4, Offset = 32 },
            new VertexAttributeDescriptor { ShaderLocation = 4, Format = VertexFormat.UInt32x4, Offset = 48 },
            new VertexAttributeDescriptor { ShaderLocation = 5, Format = VertexFormat.Float32x4, Offset = 64 },
        ]
    };
}
