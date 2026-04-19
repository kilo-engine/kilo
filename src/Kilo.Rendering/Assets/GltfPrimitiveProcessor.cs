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
    // Static vertex format: pos(3) + normal(3) + uv(2) + tangent(4) = 12 floats = 48 bytes
    private const int StaticFloatsPerVertex = 12;
    private const int StaticBytesPerVertex = 48;

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

        // Tangent data
        var tangentAccessor = primitive.GetVertexAccessor("TANGENT");
        var gltfTangents = tangentAccessor?.AsVector4Array();
        Vector4[]? computedTangents = null;
        if (gltfTangents == null)
        {
            var resolvedNormals = normals ?? (IReadOnlyList<Vector3>)generatedNormals!;
            var resolvedUvs = uvs ?? (IReadOnlyList<Vector2>)(new Vector2[vertexCount]);
            var indices = primitive.GetIndices()?.ToArray();
            computedTangents = GenerateTangents(positions, resolvedNormals, resolvedUvs, indices);
        }

        // Check for skinning attributes
        var jointsAccessor = primitive.GetVertexAccessor("JOINTS_0");
        var weightsAccessor = primitive.GetVertexAccessor("WEIGHTS_0");
        bool hasSkinning = jointsAccessor != null && weightsAccessor != null;

        // If the model is skinned but this primitive lacks skinning data,
        // we must still output skinned-format vertices (pad with dummy joints/weights)
        bool needsSkinningFormat = hasSkinning || result.IsSkinned;

        byte[] vertexData;

        if (hasSkinning)
        {
            // Skinned: pos(12) + normal(12) + uv(8) + tangent(16) + joints(16) + weights(16) = 80 bytes
            vertexData = new byte[vertexCount * SkinnedMesh.BytesPerVertex];
            var joints = jointsAccessor!.AsVector4Array();
            var weights = weightsAccessor!.AsVector4Array();

            for (int i = 0; i < vertexCount; i++)
            {
                int off = i * SkinnedMesh.BytesPerVertex;

                // Position (12)
                var p = positions[i];
                System.Buffer.BlockCopy(new[] { p.X, p.Y, p.Z }, 0, vertexData, off, 12);

                // Normal (12)
                WriteNormal(vertexData, off + 12, normals, generatedNormals, i);

                // UV (8)
                WriteUv(vertexData, off + 24, uvs, i);

                // Tangent (16) — vec4 (xyz = direction, w = handedness)
                WriteTangentVec4(vertexData, off + 32, gltfTangents, computedTangents, i);

                // Joints (16)
                if (i < joints.Count)
                {
                    var j = joints[i];
                    uint[] jointIndices = { (uint)j.X, (uint)j.Y, (uint)j.Z, (uint)j.W };
                    for (int k = 0; k < 4; k++)
                        BitConverter.GetBytes(jointIndices[k]).CopyTo(vertexData, off + 48 + k * 4);
                }

                // Weights (16)
                if (i < weights.Count)
                {
                    var w = weights[i];
                    System.Buffer.BlockCopy(new[] { w.X, w.Y, w.Z, w.W }, 0, vertexData, off + 64, 16);
                }
            }
        }
        else if (needsSkinningFormat)
        {
            // Static primitive in a skinned model: use 80-byte format with dummy joints/weights
            vertexData = new byte[vertexCount * SkinnedMesh.BytesPerVertex];

            for (int i = 0; i < vertexCount; i++)
            {
                int off = i * SkinnedMesh.BytesPerVertex;

                var p = positions[i];
                System.Buffer.BlockCopy(new[] { p.X, p.Y, p.Z }, 0, vertexData, off, 12);

                WriteNormal(vertexData, off + 12, normals, generatedNormals, i);
                WriteUv(vertexData, off + 24, uvs, i);
                WriteTangentVec4(vertexData, off + 32, gltfTangents, computedTangents, i);

                // Dummy joints (0,0,0,0) — already zero
                // Dummy weights (1,0,0,0)
                System.Buffer.BlockCopy(new[] { 1f, 0f, 0f, 0f }, 0, vertexData, off + 64, 16);
            }
        }
        else
        {
            // Static: pos(3) + normal(3) + uv(2) + tangent(4) = 12 floats = 48 bytes
            var vertices = new float[vertexCount * StaticFloatsPerVertex];

            for (int i = 0; i < vertexCount; i++)
            {
                int off = i * StaticFloatsPerVertex;
                var p = positions[i];
                vertices[off + 0] = p.X;
                vertices[off + 1] = p.Y;
                vertices[off + 2] = p.Z;

                WriteNormalFloats(vertices, off + 3, normals, generatedNormals, i);

                if (uvs != null && i < uvs.Count)
                {
                    vertices[off + 6] = uvs[i].X;
                    vertices[off + 7] = uvs[i].Y;
                }

                // Tangent vec4
                WriteTangentFloats(vertices, off + 8, gltfTangents, computedTangents, i);
            }

            vertexData = new byte[vertices.Length * sizeof(float)];
            System.Buffer.BlockCopy(vertices, 0, vertexData, 0, vertexData.Length);
        }

        // Index data
        var indexList = primitive.GetIndices();
        uint[] indexData;
        if (indexList != null)
        {
            indexData = indexList.ToArray();
        }
        else
        {
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
                ArrayStride = StaticBytesPerVertex,
                Attributes =
                [
                    new VertexAttributeDescriptor { ShaderLocation = 0, Format = VertexFormat.Float32x3, Offset = 0 },
                    new VertexAttributeDescriptor { ShaderLocation = 1, Format = VertexFormat.Float32x3, Offset = 12 },
                    new VertexAttributeDescriptor { ShaderLocation = 2, Format = VertexFormat.Float32x2, Offset = 24 },
                    new VertexAttributeDescriptor { ShaderLocation = 3, Format = VertexFormat.Float32x4, Offset = 32 },
                ]
            }]
        };

        int meshHandle = context.AddMesh(mesh);

        // Create material from GLTF material with PBR properties
        var matDescriptor = new MaterialDescriptor { BaseColor = Vector4.One };

        var gltfMaterial = primitive.Material;
        var baseColorChannel = gltfMaterial?.FindChannel("BaseColor");
        if (baseColorChannel.HasValue)
        {
            var color = baseColorChannel.Value.Parameter;
            string? albedoPath = null;

            var tex = baseColorChannel.Value.Texture;
            if (tex != null)
            {
                var img = tex.PrimaryImage;
                var content = img.Content;
                if (content.IsValid && !content.IsEmpty)
                {
                    var ext = content.FileExtension ?? ".png";
                    if (!ext.StartsWith('.')) ext = "." + ext;
                    var tempPath = Path.Combine(Path.GetTempPath(), $"gltf_albedo_{img.LogicalIndex}{ext}");
                    content.SaveToFile(tempPath);
                    albedoPath = tempPath;
                }
            }

            matDescriptor = new MaterialDescriptor
            {
                BaseColor = new Vector4(color.X, color.Y, color.Z, color.W),
                AlbedoTexturePath = albedoPath,
            };
        }

        // Extract PBR metallic-roughness
        var mrChannel = gltfMaterial?.FindChannel("MetallicRoughness");
        if (mrChannel.HasValue)
        {
            var param = mrChannel.Value.Parameter;
            matDescriptor = new MaterialDescriptor
            {
                BaseColor = matDescriptor.BaseColor,
                AlbedoTexturePath = matDescriptor.AlbedoTexturePath,
                Metallic = param.X,    // glTF: X = metallic factor
                Roughness = param.Y,   // glTF: Y = roughness factor
            };
        }

        int materialHandle = context.MaterialManager.CreateMaterial(context, scene, matDescriptor);

        return (meshHandle, materialHandle);
    }

    // ---- Vertex data helpers ----

    private static void WriteNormal(byte[] data, int offset,
        IReadOnlyList<Vector3>? normals, Vector3[]? generated, int index)
    {
        Vector3 n;
        if (normals != null && index < normals.Count) n = normals[index];
        else if (generated != null) n = generated[index];
        else return;
        System.Buffer.BlockCopy(new[] { n.X, n.Y, n.Z }, 0, data, offset, 12);
    }

    private static void WriteNormalFloats(float[] data, int offset,
        IReadOnlyList<Vector3>? normals, Vector3[]? generated, int index)
    {
        Vector3 n;
        if (normals != null && index < normals.Count) n = normals[index];
        else if (generated != null) n = generated[index];
        else return;
        data[offset + 0] = n.X;
        data[offset + 1] = n.Y;
        data[offset + 2] = n.Z;
    }

    private static void WriteUv(byte[] data, int offset, IReadOnlyList<Vector2>? uvs, int index)
    {
        if (uvs != null && index < uvs.Count)
        {
            var uv = uvs[index];
            System.Buffer.BlockCopy(new[] { uv.X, uv.Y }, 0, data, offset, 8);
        }
    }

    private static void WriteTangentVec4(byte[] data, int offset,
        IReadOnlyList<Vector4>? gltfTangents, Vector4[]? computed, int index)
    {
        Vector4 t;
        if (gltfTangents != null && index < gltfTangents.Count)
            t = gltfTangents[index]; // xyz = tangent dir, w = handedness
        else if (computed != null)
            t = computed[index];
        else
            t = new Vector4(1, 0, 0, 1); // default tangent
        System.Buffer.BlockCopy(new[] { t.X, t.Y, t.Z, t.W }, 0, data, offset, 16);
    }

    private static void WriteTangentFloats(float[] data, int offset,
        IReadOnlyList<Vector4>? gltfTangents, Vector4[]? computed, int index)
    {
        Vector4 t;
        if (gltfTangents != null && index < gltfTangents.Count)
            t = gltfTangents[index];
        else if (computed != null)
            t = computed[index];
        else
            t = new Vector4(1, 0, 0, 1);
        data[offset + 0] = t.X;
        data[offset + 1] = t.Y;
        data[offset + 2] = t.Z;
        data[offset + 3] = t.W;
    }

    // ---- Tangent generation ----

    /// <summary>
    /// Generates tangents using the Lengyel algorithm when glTF data lacks TANGENT attribute.
    /// </summary>
    public static Vector4[] GenerateTangents(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<Vector3> normals,
        IReadOnlyList<Vector2> uvs,
        uint[]? indices)
    {
        int vertexCount = positions.Count;
        var tangentAccum = new Vector3[vertexCount];
        var bitangentAccum = new Vector3[vertexCount];

        void processTriangle(int i0, int i1, int i2)
        {
            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];

            var uv0 = i0 < uvs.Count ? uvs[i0] : Vector2.Zero;
            var uv1 = i1 < uvs.Count ? uvs[i1] : Vector2.Zero;
            var uv2 = i2 < uvs.Count ? uvs[i2] : Vector2.Zero;

            var dp1 = p1 - p0;
            var dp2 = p2 - p0;
            var duv1 = uv1 - uv0;
            var duv2 = uv2 - uv0;

            float det = duv1.X * duv2.Y - duv2.X * duv1.Y;
            if (Math.Abs(det) < 1e-6f) return;

            float r = 1.0f / det;
            var tangent = new Vector3(
                (dp1.X * duv2.Y - dp2.X * duv1.Y) * r,
                (dp1.Y * duv2.Y - dp2.Y * duv1.Y) * r,
                (dp1.Z * duv2.Y - dp2.Z * duv1.Y) * r);
            var bitangent = new Vector3(
                (dp2.X * duv1.X - dp1.X * duv2.X) * r,
                (dp2.Y * duv1.X - dp1.Y * duv2.X) * r,
                (dp2.Z * duv1.X - dp1.Z * duv2.X) * r);

            tangentAccum[i0] += tangent;
            tangentAccum[i1] += tangent;
            tangentAccum[i2] += tangent;
            bitangentAccum[i0] += bitangent;
            bitangentAccum[i1] += bitangent;
            bitangentAccum[i2] += bitangent;
        }

        if (indices != null)
        {
            for (int i = 0; i + 2 < indices.Length; i += 3)
                processTriangle((int)indices[i], (int)indices[i + 1], (int)indices[i + 2]);
        }
        else
        {
            for (int i = 0; i + 2 < vertexCount; i += 3)
                processTriangle(i, i + 1, i + 2);
        }

        var tangents = new Vector4[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            var n = i < normals.Count ? normals[i] : Vector3.UnitZ;
            var t = tangentAccum[i];
            // Orthogonalize tangent against normal
            t -= n * Vector3.Dot(n, t);
            float len = t.Length();
            if (len > 1e-6f)
            {
                t /= len;
                // Handedness from cross product sign
                float handedness = Vector3.Dot(Vector3.Cross(n, t), bitangentAccum[i]) < 0 ? -1.0f : 1.0f;
                tangents[i] = new Vector4(t, handedness);
            }
            else
            {
                tangents[i] = new Vector4(1, 0, 0, 1);
            }
        }

        return tangents;
    }

    /// <summary>
    /// Generates flat (per-face) normals for primitives that lack a NORMAL attribute.
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
