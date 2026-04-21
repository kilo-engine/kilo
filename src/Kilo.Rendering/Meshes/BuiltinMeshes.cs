using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering.Meshes;

internal static class BuiltinMeshes
{
    /// <summary>
    /// Creates the default unit cube mesh (pos3 + normal3 + uv2 + tangent4 = 12 floats per vertex = 48 bytes).
    /// Tangent w-component (handedness) is always 1.0 for builtin geometry.
    /// </summary>
    public static MeshHandle CreateDefaultCube(RenderContext context, RenderResourceStore store, IRenderDriver driver)
    {
        // pos(3) + normal(3) + uv(2) + tangent(4) = 12 floats per vertex
        float[] cubeVertices =
        [
            // Front face (+Z) — tangent = +X, w=1
            -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 0.0f,  1.0f, 0.0f, 0.0f, 1.0f,
             0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 0.0f,  1.0f, 0.0f, 0.0f, 1.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 1.0f,  1.0f, 0.0f, 0.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 1.0f,  1.0f, 0.0f, 0.0f, 1.0f,
            // Back face (-Z) — tangent = -X, w=1
            -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 0.0f, -1.0f, 0.0f, 0.0f, 1.0f,
            -0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f, -1.0f, 0.0f, 0.0f, 1.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 1.0f, -1.0f, 0.0f, 0.0f, 1.0f,
             0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f, -1.0f, 0.0f, 0.0f, 1.0f,
            // Top face (+Y) — tangent = +X, w=1
            -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f,  1.0f, 0.0f, 0.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f,  1.0f, 0.0f, 0.0f, 1.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,  1.0f, 0.0f, 0.0f, 1.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 1.0f,  1.0f, 0.0f, 0.0f, 1.0f,
            // Bottom face (-Y) — tangent = +X, w=1
            -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 0.0f,  1.0f, 0.0f, 0.0f, 1.0f,
             0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,  1.0f, 0.0f, 0.0f, 1.0f,
             0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 1.0f,  1.0f, 0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,  1.0f, 0.0f, 0.0f, 1.0f,
            // Right face (+X) — tangent = -Z, w=1
             0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,  0.0f, 0.0f,-1.0f, 1.0f,
             0.5f,  0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f,  0.0f, 0.0f,-1.0f, 1.0f,
             0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,  0.0f, 0.0f,-1.0f, 1.0f,
             0.5f, -0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 0.0f,  0.0f, 0.0f,-1.0f, 1.0f,
            // Left face (-X) — tangent = +Z, w=1
            -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 0.0f,  0.0f, 0.0f, 1.0f, 1.0f,
            -0.5f, -0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,  0.0f, 0.0f, 1.0f, 1.0f,
            -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 1.0f,  0.0f, 0.0f, 1.0f, 1.0f,
            -0.5f,  0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,  0.0f, 0.0f, 1.0f, 1.0f,
        ];

        uint[] cubeIndices =
        [
            0, 1, 2,  0, 2, 3,
            4, 5, 6,  4, 6, 7,
            8, 9, 10,  8, 10, 11,
            12, 13, 14,  12, 14, 15,
            16, 17, 18,  16, 18, 19,
            20, 21, 22,  20, 22, 23,
        ];

        var cubeVertexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(cubeVertices.Length * sizeof(float)),
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
        });
        cubeVertexBuffer.UploadData<float>(cubeVertices);

        var cubeIndexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(cubeIndices.Length * sizeof(uint)),
            Usage = BufferUsage.Index | BufferUsage.CopyDst,
        });
        cubeIndexBuffer.UploadData<uint>(cubeIndices);

        var cubeMesh = new Mesh
        {
            VertexBuffer = cubeVertexBuffer,
            IndexBuffer = cubeIndexBuffer,
            IndexCount = (uint)cubeIndices.Length,
            Layouts =
            [
                new VertexBufferLayout
                {
                    ArrayStride = 12 * sizeof(float), // pos(3) + normal(3) + uv(2) + tangent(4)
                    Attributes =
                    [
                        new VertexAttributeDescriptor
                        {
                            ShaderLocation = 0,
                            Format = VertexFormat.Float32x3,
                            Offset = 0,
                        },
                        new VertexAttributeDescriptor
                        {
                            ShaderLocation = 1,
                            Format = VertexFormat.Float32x3,
                            Offset = (nuint)(3 * sizeof(float)),
                        },
                        new VertexAttributeDescriptor
                        {
                            ShaderLocation = 2,
                            Format = VertexFormat.Float32x2,
                            Offset = (nuint)(6 * sizeof(float)),
                        },
                        new VertexAttributeDescriptor
                        {
                            ShaderLocation = 3,
                            Format = VertexFormat.Float32x4,
                            Offset = (nuint)(8 * sizeof(float)),
                        },
                    ]
                }
            ]
        };

        return store.AddMesh(cubeMesh);
    }
}
