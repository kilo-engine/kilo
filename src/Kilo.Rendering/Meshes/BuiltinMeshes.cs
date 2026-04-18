using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;

namespace Kilo.Rendering.Meshes;

internal static class BuiltinMeshes
{
    /// <summary>
    /// Creates the default unit cube mesh (pos3 + normal3 + uv2 = 8 floats per vertex).
    /// </summary>
    public static void CreateDefaultCube(RenderContext context, IRenderDriver driver)
    {
        float[] cubeVertices =
        [
            // Front face
            -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 1.0f,
            // Back face
            -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 0.0f,
            -0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 1.0f,
             0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,
            // Top face
            -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 1.0f,
            // Bottom face
            -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 0.0f,
             0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,
            // Right face
             0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
             0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
             0.5f, -0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
            // Left face
            -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
            -0.5f, -0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
            -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
            -0.5f,  0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
        ];

        uint[] cubeIndices =
        [
            // Front
            0, 1, 2,  0, 2, 3,
            // Back
            4, 5, 6,  4, 6, 7,
            // Top
            8, 9, 10,  8, 10, 11,
            // Bottom
            12, 13, 14,  12, 14, 15,
            // Right
            16, 17, 18,  16, 18, 19,
            // Left
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
                    ArrayStride = 8 * sizeof(float), // pos(3) + normal(3) + uv(2)
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
                        }
                    ]
                }
            ]
        };

        context.AddMesh(cubeMesh);
    }
}
