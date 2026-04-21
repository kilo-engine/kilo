using System.Numerics;
using System.Runtime.InteropServices;
using Kilo.ECS;
using Kilo.Rendering.Assets;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Kilo.Rendering.Tests;

public class TextureLoadingTests
{
    private static string CreateTestPng(int width, int height, Rgba32 color)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"kilo_test_{Guid.NewGuid():N}.png");
        using var image = new Image<Rgba32>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = color;
            }
        }
        image.Save(tempPath);
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
    public void MaterialManager_CreateMaterial_WithTexture_SetsUseTextureTrue()
    {
        var path = CreateTestPng(16, 16, new Rgba32(255, 128, 64, 255));
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

            var materialHandle = context.MaterialManager.CreateMaterial(context, store, scene,
                new MaterialDescriptor { AlbedoTexturePath = path });

            var material = store.Materials[materialHandle.Value];
            Assert.True(material.UseTexture);
            Assert.NotNull(material.AlbedoTexture);
            Assert.NotNull(material.AlbedoSampler);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void MaterialManager_CreateMaterial_WithoutTexture_SetsUseTextureFalse()
    {
        var driver = new MockRenderDriver();
        var (context, store) = SetupContext(driver);
        var scene = new GpuSceneData
        {
            CameraBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform }),
            ObjectDataBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 4096, Usage = RenderGraph.BufferUsage.Uniform }),
            LightBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform }),
        };

        var materialHandle = context.MaterialManager.CreateMaterial(context, store, scene,
            new MaterialDescriptor { BaseColor = new Vector4(1, 0, 0, 1) });

        var material = store.Materials[materialHandle.Value];
        Assert.False(material.UseTexture);
        Assert.Null(material.AlbedoTexture);
    }

    [Fact]
    public void MaterialManager_CreateMaterial_TextureCaching()
    {
        var path = CreateTestPng(16, 16, new Rgba32(255, 255, 255, 255));
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

            var handle1 = context.MaterialManager.CreateMaterial(context, store, scene,
                new MaterialDescriptor { AlbedoTexturePath = path, BaseColor = new Vector4(1, 0, 0, 1) });
            var handle2 = context.MaterialManager.CreateMaterial(context, store, scene,
                new MaterialDescriptor { AlbedoTexturePath = path, BaseColor = new Vector4(0, 1, 0, 1) });

            var mat1 = store.Materials[handle1.Value];
            var mat2 = store.Materials[handle2.Value];

            Assert.NotNull(mat1.AlbedoTexture);
            Assert.NotNull(mat2.AlbedoTexture);
            Assert.Same(mat1.AlbedoTexture, mat2.AlbedoTexture);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void PrepareGpuScene_PopulatesUseTextureFlag()
    {
        var path = CreateTestPng(16, 16, new Rgba32(255, 255, 255, 255));
        try
        {
            var world = new KiloWorld();
            world.AddResource(new RenderSettings { Width = 1280, Height = 720 });
            world.AddResource(new WindowSize { Width = 1280, Height = 720 });

            var driver = new MockRenderDriver();
            var (context, store) = SetupContext(driver);
            var scene = new GpuSceneData
            {
                CameraBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform }),
                ObjectDataBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 4096, Usage = RenderGraph.BufferUsage.Uniform }),
                LightBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform }),
            };

            world.AddResource(context);
            world.AddResource(store);
            world.AddResource(scene);

            var textureHandle = context.MaterialManager.CreateMaterial(context, store, scene,
                new MaterialDescriptor { AlbedoTexturePath = path });

            var entity = world.Entity();
            entity.Set(new LocalToWorld { Value = Matrix4x4.Identity });
            entity.Set(new MeshRenderer { MeshHandle = new MeshHandle(0), MaterialHandle = textureHandle });

            new CameraPrepareSystem().Update(world);
            new ObjectPrepareSystem().Update(world);
            new LightPrepareSystem().Update(world);

            Assert.Equal(1, scene.DrawCount);
            Assert.Equal(textureHandle, scene.GetDraw(0).MaterialHandle);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void TextureUpload_CorrectDimensions()
    {
        var path = CreateTestPng(32, 32, new Rgba32(255, 255, 255, 255));
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

            var materialHandle = context.MaterialManager.CreateMaterial(context, store, scene,
                new MaterialDescriptor { AlbedoTexturePath = path });

            var material = store.Materials[materialHandle.Value];
            Assert.NotNull(material.AlbedoTexture);

            // MockTexture stores Width and Height from the descriptor
            var mockTexture = material.AlbedoTexture as MockTexture;
            Assert.NotNull(mockTexture);
            Assert.Equal(32, mockTexture.Width);
            Assert.Equal(32, mockTexture.Height);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void GltfLoader_LoadGlbWithBaseColorTexture_SetsUseTextureTrue()
    {
        // Step 1: Create a test PNG texture
        var texturePath = CreateTestPng(16, 16, new Rgba32(255, 128, 64, 255));
        string glbPath = null!;
        try
        {
            // Step 2: Create GLB with embedded texture using SharpGLTF
            var materialBuilder = new MaterialBuilder("textured")
                .WithMetallicRoughnessShader()
                .WithChannelImage(KnownChannel.BaseColor, texturePath);

            var mesh = new MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexEmpty, VertexEmpty>("textured_mesh");
            var prim = mesh.UsePrimitive(materialBuilder);

            var n = Vector3.UnitZ;
            prim.AddTriangle(
                new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>().WithGeometry(new Vector3(-1, 1, 0), n),
                new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>().WithGeometry(new Vector3(-1, -1, 0), n),
                new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>().WithGeometry(new Vector3(1, 0, 0), n));

            var scene = new SceneBuilder();
            scene.AddRigidMesh(mesh, new NodeBuilder("root"));

            // By default, SharpGLTF embeds images in GLB format
            var model = scene.ToGltf2();
            glbPath = Path.Combine(Path.GetTempPath(), $"kilo_test_glb_{Guid.NewGuid():N}.glb");
            model.SaveGLB(glbPath);

            // Step 3: Load the GLB and verify UseTexture is true
            var driver = new MockRenderDriver();
            var (context, store) = SetupContext(driver);
            var sceneData = new GpuSceneData
            {
                CameraBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 256, Usage = RenderGraph.BufferUsage.Uniform }),
                ObjectDataBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 4096, Usage = RenderGraph.BufferUsage.Uniform }),
                LightBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 1024, Usage = RenderGraph.BufferUsage.Uniform }),
            };

            var result = GltfLoader.Load(glbPath, driver, context, store, sceneData);

            Assert.Single(result.Primitives);
            var meshHandle = result.Primitives[0].MeshHandle;
            var materialHandle = result.Primitives[0].MaterialHandle;

            Assert.NotNull(store.Meshes[meshHandle.Value]);
            Assert.NotNull(store.Materials[materialHandle.Value]);

            var material = store.Materials[materialHandle.Value];
            Assert.True(material.UseTexture);
            Assert.NotNull(material.AlbedoTexture);
            Assert.NotNull(material.AlbedoSampler);
        }
        finally
        {
            if (File.Exists(texturePath)) File.Delete(texturePath);
            if (glbPath != null && File.Exists(glbPath)) File.Delete(glbPath);
        }
    }
}
