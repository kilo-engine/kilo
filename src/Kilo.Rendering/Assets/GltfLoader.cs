using System.Numerics;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Resources;
using System.Linq;

namespace Kilo.Rendering.Assets;

/// <summary>
/// Recursively collects all nodes in the scene graph.
/// </summary>
internal static class GltfNodeCollector
{
    public static List<SharpGLTF.Schema2.Node> CollectAll(IEnumerable<SharpGLTF.Schema2.Node> roots)
    {
        var result = new List<SharpGLTF.Schema2.Node>();
        CollectRecursive(roots, result);
        return result;
    }

    private static void CollectRecursive(IEnumerable<SharpGLTF.Schema2.Node> nodes, List<SharpGLTF.Schema2.Node> result)
    {
        foreach (var node in nodes)
        {
            result.Add(node);
            CollectRecursive(node.VisualChildren, result);
        }
    }
}

/// <summary>
/// Result of loading a GLTF model: a list of (MeshHandle, MaterialHandle) pairs,
/// one per primitive in the model.
/// </summary>
public sealed class GltfModel
{
    public List<(int MeshHandle, int MaterialHandle)> Primitives { get; } = [];
    public SkeletonData? Skeleton { get; set; }
    public List<AnimationClip> Animations { get; set; } = [];
    public int[]? JointEntityIds { get; set; }
    public bool IsSkinned { get; set; }
}

/// <summary>
/// Loads glTF/glb models and creates engine Mesh + Material resources.
/// </summary>
public static class GltfLoader
{
    /// <summary>
    /// Load a glTF or glb file and create GPU resources.
    /// Returns one entry per mesh primitive in the file.
    /// </summary>
    public static GltfModel Load(
        string path,
        IRenderDriver driver,
        RenderContext context,
        GpuSceneData scene)
    {
        var model = SharpGLTF.Schema2.ModelRoot.Load(path);
        var result = new GltfModel();

        // Collect all nodes recursively (VisualChildren only returns direct children)
        var allNodes = GltfNodeCollector.CollectAll(model.DefaultScene.VisualChildren);

        // Find the first skin in the model (skins are node-level, not primitive-level)
        SharpGLTF.Schema2.Skin? firstSkin = null;

        foreach (var node in allNodes)
        {
            if (node.Mesh == null) continue;
            if (node.Skin != null)
            {
                firstSkin = node.Skin;
                break;
            }
        }

        // Load skeleton from the first skin found
        if (firstSkin != null)
        {
            result.Skeleton = LoadSkeleton(firstSkin, model);
            result.JointEntityIds = new int[result.Skeleton.JointCount];
        }

        // Load all animations
        result.Animations = LoadAnimations(model, result.Skeleton);

        foreach (var node in allNodes)
        {
            if (node.Mesh == null) continue;

            foreach (var primitive in node.Mesh.Primitives)
            {
                var (meshHandle, materialHandle, isSkinned) = LoadPrimitive(primitive, driver, context, scene);
                result.Primitives.Add((meshHandle, materialHandle));
                if (isSkinned)
                {
                    result.IsSkinned = true;
                }
            }
        }

        return result;
    }

    private static (int meshHandle, int materialHandle, bool isSkinned) LoadPrimitive(
        SharpGLTF.Schema2.MeshPrimitive primitive,
        IRenderDriver driver,
        RenderContext context,
        GpuSceneData scene)
    {
        // Vertex data
        var posAccessor = primitive.GetVertexAccessor("POSITION");
        var positions = posAccessor.AsVector3Array();
        int vertexCount = positions.Count;

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

        byte[] vertexData;

        if (hasSkinning)
        {
            // Skinned vertex format: pos(12) + normal(12) + uv(8) + joints(16) + weights(16) = 64 bytes
            vertexData = new byte[vertexCount * SkinnedMesh.BytesPerVertex];
            var joints = jointsAccessor.AsVector4Array();
            var weights = weightsAccessor.AsVector4Array();

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

                // UV (2 floats, 8 bytes)
                if (uvs != null && i < uvs.Count)
                {
                    var uv = uvs[i];
                    System.Buffer.BlockCopy(new[] { uv.X, 1.0f - uv.Y }, 0, vertexData, off + 24, 8);
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
                    vertices[off + 7] = 1.0f - uv.Y; // Flip Y for WebGPU
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
            Layouts = [hasSkinning ? SkinnedMesh.Layout : new VertexBufferLayout
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

        context.Meshes.Add(mesh);
        int meshHandle = context.Meshes.Count - 1;

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

        return (meshHandle, materialHandle, hasSkinning);
    }

    /// <summary>
    /// Loads skeleton data from a GLTF skin.
    /// </summary>
    private static SkeletonData LoadSkeleton(
        SharpGLTF.Schema2.Skin skin,
        SharpGLTF.Schema2.ModelRoot model)
    {
        var joints = skin.Joints;
        var jointCount = joints.Count;

        var skeletonData = new SkeletonData
        {
            Joints = new JointInfo[jointCount]
        };

        // Build a mapping from node index to joint index
        var nodeToJointIndex = new Dictionary<int, int>();
        for (int i = 0; i < jointCount; i++)
        {
            nodeToJointIndex[joints[i].LogicalIndex] = i;
        }

        // Get inverse bind matrices
        Matrix4x4[]? inverseBindMatrices = null;
        if (skin.InverseBindMatrices != null)
        {
            inverseBindMatrices = skin.InverseBindMatrices.ToArray();
        }

        // Populate joint info
        for (int i = 0; i < jointCount; i++)
        {
            var jointNode = joints[i];
            var jointInfo = new JointInfo
            {
                Name = jointNode.Name ?? $"Joint_{i}",
                InverseBindMatrix = inverseBindMatrices != null && i < inverseBindMatrices.Length
                    ? inverseBindMatrices[i]
                    : Matrix4x4.Identity
            };

            // Find parent joint
            var parent = jointNode.VisualParent;
            while (parent != null)
            {
                if (nodeToJointIndex.TryGetValue(parent.LogicalIndex, out int parentJointIndex))
                {
                    jointInfo.ParentIndex = parentJointIndex;
                    break;
                }
                parent = parent.VisualParent;
            }

            skeletonData.Joints[i] = jointInfo;
        }

        return skeletonData;
    }

    /// <summary>
    /// Loads all animations from a GLTF model.
    /// </summary>
    private static List<AnimationClip> LoadAnimations(
        SharpGLTF.Schema2.ModelRoot model,
        SkeletonData? skeleton)
    {
        var animations = new List<AnimationClip>();

        // Build mapping from node index to joint index
        var nodeToJointIndex = new Dictionary<int, int>();
        if (skeleton != null)
        {
            // Create node name to joint index mapping since we need to match animations to joints
            for (int i = 0; i < skeleton.Joints.Length; i++)
            {
                var jointName = skeleton.Joints[i].Name;
                if (!string.IsNullOrEmpty(jointName))
                {
                    // Try to find the node with this name in the model
                    // Note: SharpGLTF may not have FindNode, so we'll search manually
                    foreach (var node in model.DefaultScene.VisualChildren)
                    {
                        if (FindNodeByName(node, jointName, out var foundNode))
                        {
                            nodeToJointIndex[foundNode.LogicalIndex] = i;
                            break;
                        }
                    }
                }
            }
        }

        foreach (var gltfAnim in model.LogicalAnimations)
        {
            var clip = new AnimationClip
            {
                Name = gltfAnim.Name ?? $"Animation_{animations.Count}",
                Channels = []
            };

            // Group channels by target node
            var channelsByNode = gltfAnim.Channels
                .Where(c => c.TargetNode != null)
                .GroupBy(c => c.TargetNode);

            foreach (var nodeGroup in channelsByNode)
            {
                var node = nodeGroup.Key;

                // Find which joint this node corresponds to
                int jointIndex = -1;
                if (nodeToJointIndex.TryGetValue(node.LogicalIndex, out int foundJointIndex))
                {
                    jointIndex = foundJointIndex;
                }
                else if (skeleton != null)
                {
                    for (int i = 0; i < skeleton.Joints.Length; i++)
                    {
                        if (skeleton.Joints[i].Name == node.Name)
                        {
                            jointIndex = i;
                            break;
                        }
                    }
                }

                if (jointIndex < 0) continue;

                // Collect keyframes from all property channels for this node
                var allTimes = new HashSet<float>();
                var translationKeys = new List<(float Time, Vector3 Value)>();
                var rotationKeys = new List<(float Time, Quaternion Value)>();
                var scaleKeys = new List<(float Time, Vector3 Value)>();

                foreach (var channel in nodeGroup)
                {
                    switch (channel.TargetNodePath)
                    {
                        case SharpGLTF.Schema2.PropertyPath.translation:
                            var tSampler = channel.GetTranslationSampler();
                            if (tSampler != null)
                            {
                                foreach (var (k, v) in tSampler.GetLinearKeys())
                                {
                                    allTimes.Add(k);
                                    translationKeys.Add((k, v));
                                }
                            }
                            break;
                        case SharpGLTF.Schema2.PropertyPath.rotation:
                            var rSampler = channel.GetRotationSampler();
                            if (rSampler != null)
                            {
                                foreach (var (k, v) in rSampler.GetLinearKeys())
                                {
                                    allTimes.Add(k);
                                    rotationKeys.Add((k, v));
                                }
                            }
                            break;
                        case SharpGLTF.Schema2.PropertyPath.scale:
                            var sSampler = channel.GetScaleSampler();
                            if (sSampler != null)
                            {
                                foreach (var (k, v) in sSampler.GetLinearKeys())
                                {
                                    allTimes.Add(k);
                                    scaleKeys.Add((k, v));
                                }
                            }
                            break;
                    }
                }

                if (allTimes.Count == 0) continue;

                var animChannel = new AnimationChannel
                {
                    JointIndex = jointIndex,
                    Keyframes = []
                };

                foreach (float t in allTimes.OrderBy(x => x))
                {
                    var kf = new AnimationKeyframe
                    {
                        Time = t,
                        Position = InterpolateLinear(translationKeys, t, Vector3.Zero),
                        Rotation = InterpolateRotation(rotationKeys, t, Quaternion.Identity),
                        Scale = InterpolateLinear(scaleKeys, t, Vector3.One)
                    };
                    animChannel.Keyframes.Add(kf);
                }

                clip.Channels.Add(animChannel);
            }

            // Calculate clip duration
            clip.Duration = clip.Channels.SelectMany(ch => ch.Keyframes).Select(kf => kf.Time).DefaultIfEmpty(0).Max();

            if (clip.Channels.Count > 0)
            {
                animations.Add(clip);
            }
        }

        return animations;
    }

    /// <summary>
    /// Recursively finds a node by name in the scene hierarchy.
    /// </summary>
    private static bool FindNodeByName(SharpGLTF.Schema2.Node node, string name, out SharpGLTF.Schema2.Node? found)
    {
        if (node.Name == name)
        {
            found = node;
            return true;
        }

        foreach (var child in node.VisualChildren)
        {
            if (FindNodeByName(child, name, out found))
                return true;
        }

        found = null;
        return false;
    }

    private static Vector3 InterpolateLinear(List<(float Time, Vector3 Value)> keys, float t, Vector3 fallback)
    {
        if (keys.Count == 0) return fallback;
        if (keys.Count == 1) return keys[0].Value;

        // Find surrounding keyframes
        int idx = 0;
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i].Time <= t) idx = i;
            else break;
        }

        if (idx >= keys.Count - 1) return keys[^1].Value;
        if (keys[idx].Time == t) return keys[idx].Value;

        float dt = keys[idx + 1].Time - keys[idx].Time;
        float alpha = dt > 0 ? (t - keys[idx].Time) / dt : 0f;
        return Vector3.Lerp(keys[idx].Value, keys[idx + 1].Value, alpha);
    }

    private static Quaternion InterpolateRotation(List<(float Time, Quaternion Value)> keys, float t, Quaternion fallback)
    {
        if (keys.Count == 0) return fallback;
        if (keys.Count == 1) return keys[0].Value;

        int idx = 0;
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i].Time <= t) idx = i;
            else break;
        }

        if (idx >= keys.Count - 1) return keys[^1].Value;
        if (keys[idx].Time == t) return keys[idx].Value;

        float dt = keys[idx + 1].Time - keys[idx].Time;
        float alpha = dt > 0 ? (t - keys[idx].Time) / dt : 0f;
        return Quaternion.Slerp(keys[idx].Value, keys[idx + 1].Value, alpha);
    }

    /// <summary>
    /// Generates flat (per-face) normals for primitives that lack a NORMAL attribute.
    /// Works with both indexed and non-indexed triangle lists.
    /// </summary>
    private static Vector3[] GenerateFlatNormals(
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
