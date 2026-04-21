using System.Numerics;
using Kilo.Rendering.Assets;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using Xunit;

namespace Kilo.Rendering.Tests;

public class GltfLoaderTests
{
    private static string CreateTestGlb(int triangleCount)
    {
        var material = new MaterialBuilder()
            .WithMetallicRoughnessShader();

        var mesh = new MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexEmpty, VertexEmpty>("test");
        var prim = mesh.UsePrimitive(material);

        for (int t = 0; t < triangleCount; t++)
        {
            float x = t * 2f;
            var v0 = new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>()
                .WithGeometry(new Vector3(x, 1, 0), Vector3.UnitZ);
            var v1 = new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>()
                .WithGeometry(new Vector3(x, -1, 0), Vector3.UnitZ);
            var v2 = new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>()
                .WithGeometry(new Vector3(x + 2, 0, 0), Vector3.UnitZ);
            prim.AddTriangle(v0, v1, v2);
        }

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, new NodeBuilder("root"));

        var model = scene.ToGltf2();
        var tempPath = Path.Combine(Path.GetTempPath(), $"kilo_test_{Guid.NewGuid():N}.glb");
        model.SaveGLB(tempPath);
        return tempPath;
    }

    private static string CreateTestGlbWithMultiplePrimitives()
    {
        var mat1 = new MaterialBuilder("red")
            .WithMetallicRoughnessShader()
            .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, new Vector4(1, 0, 0, 1));
        var mat2 = new MaterialBuilder("blue")
            .WithMetallicRoughnessShader()
            .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, new Vector4(0, 0, 1, 1));

        var mesh = new MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexEmpty, VertexEmpty>("multi");
        var prim1 = mesh.UsePrimitive(mat1);
        var prim2 = mesh.UsePrimitive(mat2);

        var n = Vector3.UnitZ;
        prim1.AddTriangle(
            new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>().WithGeometry(new Vector3(-2, 1, 0), n),
            new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>().WithGeometry(new Vector3(-2, -1, 0), n),
            new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>().WithGeometry(new Vector3(0, 0, 0), n));

        prim2.AddTriangle(
            new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>().WithGeometry(new Vector3(0, 1, 0), n),
            new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>().WithGeometry(new Vector3(0, -1, 0), n),
            new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>().WithGeometry(new Vector3(2, 0, 0), n));

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, new NodeBuilder("root"));
        var model = scene.ToGltf2();

        var tempPath = Path.Combine(Path.GetTempPath(), $"kilo_test_multi_{Guid.NewGuid():N}.glb");
        model.SaveGLB(tempPath);
        return tempPath;
    }

    private static (RenderContext context, RenderResourceStore store) SetupContext(MockRenderDriver driver)
    {
        var context = new RenderContext { Driver = driver, ShaderCache = new ShaderCache(), PipelineCache = new PipelineCache() };
        var store = new RenderResourceStore();

        // Default cube mesh (required by MaterialManager for pipeline layout)
        store.AddMesh(new Mesh
        {
            VertexBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Vertex }),
            IndexBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 64, Usage = RenderGraph.BufferUsage.Index }),
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

        return (context, store);
    }

    [Fact]
    public void Load_SingleTriangle_CreatesMeshAndMaterial()
    {
        var path = CreateTestGlb(1);
        try
        {
            var driver = new MockRenderDriver();
            var (context, store) = SetupContext(driver);
            var scene = new GpuSceneData
            {
                CameraBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform }),
                ObjectDataBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 4096, Usage = RenderGraph.BufferUsage.Uniform }),
                LightBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform }),
            };

            var result = GltfLoader.Load(path, driver, context, store, scene);

            Assert.Single(result.Primitives);
            Assert.Equal(2, store.Meshes.Count); // cube + gltf
            Assert.Equal(1, store.Materials.Count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_MultiplePrimitives_CreatesMultipleEntries()
    {
        var path = CreateTestGlbWithMultiplePrimitives();
        try
        {
            var driver = new MockRenderDriver();
            var (context, store) = SetupContext(driver);
            var scene = new GpuSceneData
            {
                CameraBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform }),
                ObjectDataBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 4096, Usage = RenderGraph.BufferUsage.Uniform }),
                LightBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform }),
            };

            var result = GltfLoader.Load(path, driver, context, store, scene);

            Assert.Equal(2, result.Primitives.Count);
            Assert.Equal(1 + 2, store.Meshes.Count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_InvalidPath_Throws()
    {
        var driver = new MockRenderDriver();
        var context = new RenderContext { Driver = driver };
        var store = new RenderResourceStore();
        var scene = new GpuSceneData();

        Assert.ThrowsAny<Exception>(() =>
            GltfLoader.Load("nonexistent_file.glb", driver, context, store, scene));
    }
}
