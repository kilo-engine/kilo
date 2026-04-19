using System.Numerics;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering.Assets;

/// <summary>
/// Processes GLTF mesh primitives: builds vertex/index buffers and creates materials.
/// </summary>
internal static class GltfPrimitiveProcessor
{
    public static (int meshHandle, int materialHandle) LoadPrimitive(
        SharpGLTF.Schema2.MeshPrimitive primitive,
        IRenderDriver driver,
        RenderContext context,
        GpuSceneData scene,
        GltfModel result)
    {
        // Vertex data
        var posAccessor = primitive.GetVertexAccessor("POSITION");
        var positions = posAccessor.AsVector3Array();
        int vertexCount = positions.Count;

        // Update bounding box
        for (int i = 0; i < vertexCount; i++)
        {
            var p = positions[i];
            result.BBoxMin = Vector3.Min(result.BBoxMin, p);
            result.BBoxMax = Vector3.Max(result.BBoxMax, p);
        }

        var normAccessor = primitive.GetVertexAccessor("NORMAL");
        var normals = normAccessor?.AsVector3Array();

        // Generate flat normals if the primitive has no NORMAL attribute
        Vector3[]? generatedNormals = null;
        if (normals == null)
        {
            generatedNormals = GenerateFlatNormals(positions, primitive.GetIndices());
        }

        var uvAccessor = primitive.GetVertexAccessor("TEXCOORD_0");
        var uvs = uvAccessor?.AsVector2Array();

        // Check for skinning attributes
        var jointsAccessor = primitive.GetVertexAccessor("JOINTS_0");
        var weightsAccessor = primitive.GetVertexAccessor("WEIGHTS_0");
        bool hasSkinning = jointsAccessor != null && weightsAccessor != null;

        // If the model is skinned but this primitive lacks skinning data,
        // we must still output 64-byte vertices (pad with dummy joints/weights)
        bool needsSkinningFormat = hasSkinning || result.IsSkinned;

        byte[] vertexData;

        if (hasSkinning)
        {
            // Skinned vertex format: pos(12) + normal(12) + uv(8) + joints(16) + weights(16) = 64 bytes
            vertexData = new byte[vertexCount * SkinnedMesh.BytesPerVertex];
            var joints = jointsAccessor!.AsVector4Array();
            var weights = weightsAccessor!.AsVector4Array();

            for (int i = 0; i < vertexCount; i++)
            {
                int off = i * SkinnedMesh.BytesPerVertex;

                // Position (3 floats, 12 bytes)
                var p = positions[i];
                System.Buffer.BlockCopy(new[] { p.X, p.Y, p.Z }, 0, vertexData, off, 12);

                // Normal (3 floats, 12 bytes)
                if (normals != null && i < normals.Count)
                {
                    var n = normals[i];
                    System.Buffer.BlockCopy(new[] { n.X, n.Y, n.Z }, 0, vertexData, off + 12, 12);
                }
                else if (generatedNormals != null)
                {
                    var n = generatedNormals[i];
                    System.Buffer.BlockCopy(new[] { n.X, n.Y, n.Z }, 0, vertexData, off + 12, 12);
                }

                // UV (2 floats, 8 bytes) — glTF UV origin is top-left, same as WebGPU
                if (uvs != null && i < uvs.Count)
                {
                    var uv = uvs[i];
                    System.Buffer.BlockCopy(new[] { uv.X, uv.Y }, 0, vertexData, off + 24, 8);
                }

                // Joints (4 uints, 16 bytes)
                if (i < joints.Count)
                {
                    var j = joints[i];
                    uint[] jointIndices = { (uint)j.X, (uint)j.Y, (uint)j.Z, (uint)j.W };
                    for (int k = 0; k < 4; k++)
                        BitConverter.GetBytes(jointIndices[k]).CopyTo(vertexData, off + 32 + k * 4);
                }

                // Weights (4 floats, 16 bytes)
                if (i < weights.Count)
                {
                    var w = weights[i];
                    System.Buffer.BlockCopy(new[] { w.X, w.Y, w.Z, w.W }, 0, vertexData, off + 48, 16);
                }
            }

            Console.WriteLine($"[GltfPrimitiveProcessor] Skinned primitive: {vertexCount} verts, joints accessor count={joints.Count}");
        }
        else if (needsSkinningFormat)
        {
            // Static primitive in a skinned model: use 64-byte format with dummy joints/weights
            vertexData = new byte[vertexCount * SkinnedMesh.BytesPerVertex];

            for (int i = 0; i < vertexCount; i++)
            {
                int off = i * SkinnedMesh.BytesPerVertex;

                // Position (3 floats, 12 bytes)
                var p = positions[i];
                System.Buffer.BlockCopy(new[] { p.X, p.Y, p.Z }, 0, vertexData, off, 12);

                // Normal (3 floats, 12 bytes)
                if (normals != null && i < normals.Count)
                {
                    var n = normals[i];
                    System.Buffer.BlockCopy(new[] { n.X, n.Y, n.Z }, 0, vertexData, off + 12, 12);
                }
                else if (generatedNormals != null)
                {
                    var n = generatedNormals[i];
                    System.Buffer.BlockCopy(new[] { n.X, n.Y, n.Z }, 0, vertexData, off + 12, 12);
                }

                // UV (2 floats, 8 bytes) — glTF UV origin is top-left, same as WebGPU
                if (uvs != null && i < uvs.Count)
                {
                    var uv = uvs[i];
                    System.Buffer.BlockCopy(new[] { uv.X, uv.Y }, 0, vertexData, off + 24, 8);
                }

                // Dummy joints (4 uints = 0,0,0,0) — bytes 32-47 already zero
                // Dummy weights (1,0,0,0) — full weight to joint 0
                System.Buffer.BlockCopy(new[] { 1f, 0f, 0f, 0f }, 0, vertexData, off + 48, 16);
            }

            Console.WriteLine($"[GltfPrimitiveProcessor] Static-in-skinned primitive: {vertexCount} verts (padded to 64-byte)");
        }
        else
        {
            // Static vertex format: pos(3) + normal(3) + uv(2) = 8 floats = 32 bytes
            int floatsPerVertex = 8;
            var vertices = new float[vertexCount * floatsPerVertex];

            for (int i = 0; i < vertexCount; i++)
            {
                int off = i * floatsPerVertex;
                var p = positions[i];
                vertices[off + 0] = p.X;
                vertices[off + 1] = p.Y;
                vertices[off + 2] = p.Z;

                if (normals != null && i < normals.Count)
                {
                    var n = normals[i];
                    vertices[off + 3] = n.X;
                    vertices[off + 4] = n.Y;
                    vertices[off + 5] = n.Z;
                }
                else if (generatedNormals != null)
                {
                    var n = generatedNormals[i];
                    vertices[off + 3] = n.X;
                    vertices[off + 4] = n.Y;
                    vertices[off + 5] = n.Z;
                }

                if (uvs != null && i < uvs.Count)
                {
                    var uv = uvs[i];
                    vertices[off + 6] = uv.X;
                    vertices[off + 7] = uv.Y;
                }
            }

            // Convert to byte array
            vertexData = new byte[vertices.Length * sizeof(float)];
            System.Buffer.BlockCopy(vertices, 0, vertexData, 0, vertexData.Length);
        }

        // Index data — some models use non-indexed drawing (e.g. Fox.glb)
        var indexList = primitive.GetIndices();
        uint[] indexData;
        if (indexList != null)
        {
            indexData = indexList.ToArray();
        }
        else
        {
            // Generate sequential indices for non-indexed primitives
            indexData = new uint[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                indexData[i] = (uint)i;
        }

        // Create GPU buffers
        var vb = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)vertexData.Length,
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
        });
        vb.UploadData<byte>(vertexData);

        var ib = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(indexData.Length * sizeof(uint)),
            Usage = BufferUsage.Index | BufferUsage.CopyDst,
        });
        ib.UploadData<uint>(indexData);

        var mesh = new Mesh
        {
            VertexBuffer = vb,
            IndexBuffer = ib,
            IndexCount = (uint)indexData.Length,
            Layouts = [needsSkinningFormat ? SkinnedMesh.Layout : new VertexBufferLayout
            {
                ArrayStride = 32,
                Attributes =
                [
                    new VertexAttributeDescriptor { ShaderLocation = 0, Format = VertexFormat.Float32x3, Offset = 0 },
                    new VertexAttributeDescriptor { ShaderLocation = 1, Format = VertexFormat.Float32x3, Offset = 12 },
                    new VertexAttributeDescriptor { ShaderLocation = 2, Format = VertexFormat.Float32x2, Offset = 24 },
                ]
            }]
        };

        int meshHandle = context.AddMesh(mesh);

        // Create material from GLTF material
        var matDescriptor = new MaterialDescriptor { BaseColor = Vector4.One };

        var gltfMaterial = primitive.Material;
        var baseColorChannel = gltfMaterial?.FindChannel("BaseColor");
        if (baseColorChannel.HasValue)
        {
            // Apply base color factor
            var color = baseColorChannel.Value.Parameter;
            matDescriptor = new MaterialDescriptor { BaseColor = new Vector4(color.X, color.Y, color.Z, color.W) };

            // Extract texture if present
            var tex = baseColorChannel.Value.Texture;
            if (tex != null)
            {
                var img = tex.PrimaryImage;
                var content = img.Content;
                if (content.IsValid && !content.IsEmpty)
                {
                    // Fix: ensure the extension matches the content format
                    var ext = content.FileExtension ?? ".png";
                    // Ensure extension starts with dot
                    if (!ext.StartsWith('.')) ext = "." + ext;
                    var tempPath = Path.Combine(Path.GetTempPath(), $"gltf_albedo_{img.LogicalIndex}{ext}");
                    content.SaveToFile(tempPath);
                    matDescriptor = new MaterialDescriptor
                    {
                        BaseColor = new Vector4(color.X, color.Y, color.Z, color.W),
                        AlbedoTexturePath = tempPath,
                    };
                }
            }
        }

        int materialHandle = context.MaterialManager.CreateMaterial(context, scene, matDescriptor);

        return (meshHandle, materialHandle);
    }

    /// <summary>
    /// Generates flat (per-face) normals for primitives that lack a NORMAL attribute.
    /// Works with both indexed and non-indexed triangle lists.
    /// </summary>
    public static Vector3[] GenerateFlatNormals(
        IReadOnlyList<Vector3> positions,
        IEnumerable<uint>? indices)
    {
        var normals = new Vector3[positions.Count];
        var normalAccum = new Vector3[positions.Count];

        if (indices != null)
        {
            var indexList = indices.ToList();
            for (int i = 0; i + 2 < indexList.Count; i += 3)
            {
                var v0 = positions[(int)indexList[i]];
                var v1 = positions[(int)indexList[i + 1]];
                var v2 = positions[(int)indexList[i + 2]];
                var faceNormal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
                normalAccum[(int)indexList[i]] += faceNormal;
                normalAccum[(int)indexList[i + 1]] += faceNormal;
                normalAccum[(int)indexList[i + 2]] += faceNormal;
            }
        }
        else
        {
            // Non-indexed: every 3 consecutive vertices form a triangle
            for (int i = 0; i + 2 < positions.Count; i += 3)
            {
                var v0 = positions[i];
                var v1 = positions[i + 1];
                var v2 = positions[i + 2];
                var faceNormal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
                normalAccum[i] += faceNormal;
                normalAccum[i + 1] += faceNormal;
                normalAccum[i + 2] += faceNormal;
            }
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = normalAccum[i].Length() > 0
                ? Vector3.Normalize(normalAccum[i])
                : Vector3.UnitZ;
        }

        return normals;
    }
}
