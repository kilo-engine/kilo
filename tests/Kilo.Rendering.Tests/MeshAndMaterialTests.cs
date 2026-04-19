using Kilo.Rendering.Driver;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;
using Xunit;

namespace Kilo.Rendering.Tests;

public class MeshAndMaterialTests
{
    [Fact]
    public void Mesh_HasExpectedProperties()
    {
        var driver = new MockRenderDriver();
        var vertexBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 128, Usage = RenderGraph.BufferUsage.Vertex });
        var indexBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor { Size = 64, Usage = RenderGraph.BufferUsage.Index });

        var mesh = new Mesh
        {
            VertexBuffer = vertexBuffer,
            IndexBuffer = indexBuffer,
            IndexCount = 36,
            Layouts =
            [
                new VertexBufferLayout
                {
                    ArrayStride = 24,
                    Attributes =
                    [
                        new VertexAttributeDescriptor { ShaderLocation = 0, Format = VertexFormat.Float32x3, Offset = 0 },
                        new VertexAttributeDescriptor { ShaderLocation = 1, Format = VertexFormat.Float32x3, Offset = 12 },
                    ]
                }
            ]
        };

        Assert.Equal(vertexBuffer, mesh.VertexBuffer);
        Assert.Equal(indexBuffer, mesh.IndexBuffer);
        Assert.Equal(36u, mesh.IndexCount);
        Assert.Single(mesh.Layouts);
        Assert.Equal(2, mesh.Layouts[0].Attributes.Length);
    }

    [Fact]
    public void Material_HasExpectedProperties()
    {
        var driver = new MockRenderDriver();
        var pipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = driver.CreateShaderModule("", "vs"),
            FragmentShader = driver.CreateShaderModule("", "fs"),
        });

        var bindingSet = driver.CreateBindingSet(new BindingSetDescriptor
        {
            Layout = new BindingSetLayout { Entries = [] }
        });

        var material = new Material
        {
            Pipeline = pipeline,
            BindingSets = [bindingSet],
        };

        Assert.Equal(pipeline, material.Pipeline);
        Assert.Single(material.BindingSets);
        Assert.Equal(bindingSet, material.BindingSets[0]);
    }
}
