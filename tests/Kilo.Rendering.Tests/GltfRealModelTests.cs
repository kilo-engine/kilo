using System.Numerics;
using Kilo.Rendering.Assets;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Resources;
using Xunit;

namespace Kilo.Rendering.Tests;

/// <summary>
/// Tests GLTF loading with real model files from the bevy asset collection.
/// Covers: multi-mesh, textured, skinned/animated, complex models.
/// </summary>
public class GltfRealModelTests
{
    private static readonly string ModelsRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs-3rd", "bevy-main", "assets", "models"));

    private static RenderContext SetupContext(MockRenderDriver driver)
    {
        var context = new RenderContext { Driver = driver, ShaderCache = new ShaderCache(), PipelineCache = new PipelineCache() };
        context.Meshes.Add(new Mesh
        {
            VertexBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 256, Usage = BufferUsage.Vertex }),
            IndexBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 64, Usage = BufferUsage.Index }),
            IndexCount = 36,
            Layouts =
            [
                new VertexBufferLayout
                {
                    ArrayStride = 8 * sizeof(float),
                    Attributes =
                    [
                        new VertexAttributeDescriptor { ShaderLocation = 0, Format = VertexFormat.Float32x3, Offset = 0 },
                        new VertexAttributeDescriptor { ShaderLocation = 1, Format = VertexFormat.Float32x3, Offset = (nuint)(3 * sizeof(float)) },
                        new VertexAttributeDescriptor { ShaderLocation = 2, Format = VertexFormat.Float32x2, Offset = (nuint)(6 * sizeof(float)) },
                    ]
                }
            ]
        });
        return context;
    }

    private static (RenderContext context, GpuSceneData scene) CreateTestEnv()
    {
        var driver = new MockRenderDriver();
        var context = SetupContext(driver);
        var scene = new GpuSceneData
        {
            CameraBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 256, Usage = BufferUsage.Uniform }),
            ObjectDataBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 4096, Usage = BufferUsage.Uniform }),
            LightBuffer = driver.CreateBuffer(new BufferDescriptor { Size = 1024, Usage = BufferUsage.Uniform }),
        };
        return (context, scene);
    }

    private static string Resolve(string relativePath)
    {
        var path = Path.Combine(ModelsRoot, relativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Test model not found: {path}");
        return path;
    }

    // ── Simple mesh (no texture, no animation) ──────────────────────

    [Fact]
    public void CornellBox_Glb_LoadsSuccessfully()
    {
        var (context, scene) = CreateTestEnv();
        var result = GltfLoader.Load(Resolve("CornellBox/CornellBox.glb"), context.Driver, context, scene);

        Assert.True(result.Primitives.Count > 0, "Should have at least one primitive");
        Assert.False(result.IsSkinned, "CornellBox is not skinned");
        Assert.Empty(result.Animations);

        foreach (var (meshHandle, matHandle) in result.Primitives)
        {
            Assert.True(meshHandle > 0, "Mesh handle should be > 0 (index 0 is default cube)");
            Assert.InRange(matHandle, 0, context.Materials.Count - 1);
        }
    }

    // ── Simple gltf with external data ──────────────────────────────

    [Fact]
    public void Cube_Gltf_LoadsSuccessfully()
    {
        var (context, scene) = CreateTestEnv();
        var result = GltfLoader.Load(Resolve("cube/cube.gltf"), context.Driver, context, scene);

        Assert.True(result.Primitives.Count > 0);
        Assert.False(result.IsSkinned);
    }

    // ── Multi-mesh model ────────────────────────────────────────────

    [Fact]
    public void Cubes_Glb_MultiplePrimitives()
    {
        var (context, scene) = CreateTestEnv();
        var result = GltfLoader.Load(Resolve("cubes/Cubes.glb"), context.Driver, context, scene);

        Assert.True(result.Primitives.Count >= 1, "Cubes should have primitives");
    }

    // ── Textured model (multi-material with textures) ───────────────

    [Fact]
    public void FlightHelmet_Gltf_LoadsWithTextures()
    {
        var (context, scene) = CreateTestEnv();
        var result = GltfLoader.Load(Resolve("FlightHelmet/FlightHelmet.gltf"), context.Driver, context, scene);

        Assert.True(result.Primitives.Count >= 4, $"FlightHelmet should have >= 4 primitives (got {result.Primitives.Count})");

        // Check that materials have textures
        int texturedCount = 0;
        foreach (var (_, matHandle) in result.Primitives)
        {
            var mat = context.Materials[matHandle];
            if (mat.UseTexture) texturedCount++;
        }
        Assert.True(texturedCount > 0, $"At least one material should have textures (textured: {texturedCount})");
    }

    // ── Animated/skinned model ──────────────────────────────────────

    [Fact]
    public void Fox_Glb_LoadsWithSkeletonAndAnimations()
    {
        var (context, scene) = CreateTestEnv();
        var result = GltfLoader.Load(Resolve("animated/Fox.glb"), context.Driver, context, scene);

        Assert.True(result.Primitives.Count > 0, "Fox should have primitives");
        Assert.True(result.IsSkinned, "Fox should be detected as skinned");
        Assert.NotNull(result.Skeleton);
        Assert.True(result.Skeleton.Joints.Length > 0, "Fox should have joints");
        Assert.True(result.Animations.Count > 0, "Fox should have animations");

        // Verify animation has channels and keyframes
        foreach (var clip in result.Animations)
        {
            Assert.False(string.IsNullOrEmpty(clip.Name), $"Clip should have a name");
            Assert.True(clip.Duration > 0, $"Clip '{clip.Name}' should have positive duration");
            Assert.True(clip.Channels.Count > 0, $"Clip '{clip.Name}' should have channels");

            foreach (var channel in clip.Channels)
            {
                Assert.True(channel.Keyframes.Count > 0, "Each channel should have keyframes");
                Assert.True(channel.JointIndex >= 0, "JointIndex should be valid");
            }
        }
    }

    // ── Model with base color textures ──────────────────────────────

    [Fact]
    public void GolfBall_Glb_LoadsWithMaterial()
    {
        var (context, scene) = CreateTestEnv();
        var result = GltfLoader.Load(Resolve("GolfBall/GolfBall.glb"), context.Driver, context, scene);

        Assert.True(result.Primitives.Count > 0);
        foreach (var (meshHandle, matHandle) in result.Primitives)
        {
            Assert.InRange(meshHandle, 1, context.Meshes.Count - 1);
            Assert.InRange(matHandle, 0, context.Materials.Count - 1);
        }
    }

    // ── Vertex data integrity ───────────────────────────────────────

    [Fact]
    public void CornellBox_VertexBuffersHaveData()
    {
        var (context, scene) = CreateTestEnv();
        var result = GltfLoader.Load(Resolve("CornellBox/CornellBox.glb"), context.Driver, context, scene);

        foreach (var (meshHandle, _) in result.Primitives)
        {
            var mesh = context.Meshes[meshHandle];
            Assert.NotNull(mesh.VertexBuffer);
            Assert.NotNull(mesh.IndexBuffer);
            Assert.True(mesh.IndexCount > 0, "Should have indices");
            Assert.True(mesh.VertexBuffer.Size > 0, "Vertex buffer should have data");
            Assert.True(mesh.IndexBuffer.Size > 0, "Index buffer should have data");
        }
    }

    // ── Skinned mesh vertex format ──────────────────────────────────

    [Fact]
    public void Fox_SkinnedMeshHasCorrectVertexLayout()
    {
        var (context, scene) = CreateTestEnv();
        var result = GltfLoader.Load(Resolve("animated/Fox.glb"), context.Driver, context, scene);

        // At least one primitive should have skinned vertex layout
        bool foundSkinnedLayout = false;
        foreach (var (meshHandle, _) in result.Primitives)
        {
            var mesh = context.Meshes[meshHandle];
            var layout = mesh.Layouts[0];
            if (layout.ArrayStride == SkinnedMesh.BytesPerVertex)
            {
                foundSkinnedLayout = true;
                // Should have 5 attributes: pos, normal, uv, joints, weights
                Assert.Equal(5, layout.Attributes.Length);
            }
        }
        Assert.True(foundSkinnedLayout, "Fox should have at least one primitive with skinned vertex layout");
    }

    // ── Skeleton hierarchy ──────────────────────────────────────────

    [Fact]
    public void Fox_SkeletonHasValidHierarchy()
    {
        var (context, scene) = CreateTestEnv();
        var result = GltfLoader.Load(Resolve("animated/Fox.glb"), context.Driver, context, scene);

        Assert.NotNull(result.Skeleton);
        var joints = result.Skeleton.Joints;

        // At least one joint should be root (ParentIndex = -1)
        int rootCount = joints.Count(j => j.ParentIndex < 0);
        Assert.True(rootCount >= 1, $"Should have at least one root joint (found {rootCount})");

        // All non-root joints should have valid parent indices
        foreach (var joint in joints)
        {
            if (joint.ParentIndex >= 0)
            {
                Assert.InRange(joint.ParentIndex, 0, joints.Length - 1);
            }
            Assert.False(string.IsNullOrEmpty(joint.Name), "Each joint should have a name");
        }
    }

    // ── Invalid path ────────────────────────────────────────────────

    [Fact]
    public void InvalidPath_ThrowsException()
    {
        var (context, scene) = CreateTestEnv();
        Assert.ThrowsAny<Exception>(() =>
            GltfLoader.Load("does_not_exist.glb", context.Driver, context, scene));
    }
}
