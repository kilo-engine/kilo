using System.Numerics;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Scene;

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
    public List<(MeshHandle MeshHandle, MaterialHandle MaterialHandle)> Primitives { get; } = [];
    public SkeletonData? Skeleton { get; set; }
    public List<AnimationClip> Animations { get; set; } = [];
    public int[]? JointEntityIds { get; set; }
    public bool IsSkinned { get; set; }

    /// <summary>Axis-aligned bounding box of the model in local space.</summary>
    public Vector3 BBoxMin = new(float.MaxValue);
    public Vector3 BBoxMax = new(float.MinValue);
}

/// <summary>
/// Loads glTF/glb models and creates engine Mesh + Material resources.
/// Orchestrates primitive processing, skeleton loading, and animation loading.
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
        RenderResourceStore store,
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
            result.Skeleton = GltfSkeletonLoader.LoadSkeleton(firstSkin, model);
            result.JointEntityIds = new int[result.Skeleton.JointCount];
        }

        // Load all animations
        result.Animations = GltfAnimationLoader.LoadAnimations(model, result.Skeleton);

        // First pass: detect if any primitive has skinning
        bool anySkinned = false;
        foreach (var node in allNodes)
        {
            if (node.Mesh == null) continue;
            foreach (var primitive in node.Mesh.Primitives)
            {
                var ja = primitive.GetVertexAccessor("JOINTS_0");
                var wa = primitive.GetVertexAccessor("WEIGHTS_0");
                if (ja != null && wa != null)
                {
                    anySkinned = true;
                    break;
                }
            }
            if (anySkinned) break;
        }
        result.IsSkinned = anySkinned;

        foreach (var node in allNodes)
        {
            if (node.Mesh == null) continue;

            foreach (var primitive in node.Mesh.Primitives)
            {
                var (meshHandle, materialHandle) = GltfPrimitiveProcessor.LoadPrimitive(primitive, driver, context, store, scene, result);
                result.Primitives.Add((meshHandle, materialHandle));
            }
        }

        return result;
    }
}
