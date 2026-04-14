using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Resources;
using Xunit;

namespace Kilo.Rendering.Tests;

public class MaterialInstanceTests
{
    [Fact]
    public void MaterialInstance_RetrievesParentPipeline()
    {
        var driver = new MockRenderDriver();
        var pipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = driver.CreateShaderModule("", "vs"),
            FragmentShader = driver.CreateShaderModule("", "fs"),
        });

        var material = new Material
        {
            Pipeline = pipeline,
            BindingSets = [driver.CreateBindingSet(new BindingSetDescriptor { Layout = new BindingSetLayout { Entries = [] } })],
        };

        var instance = new MaterialInstance(material);

        Assert.Same(pipeline, instance.Pipeline);
    }

    [Fact]
    public void MaterialInstance_GetBindingSet_FallsBackToParent()
    {
        var driver = new MockRenderDriver();
        var bindingSet = driver.CreateBindingSet(new BindingSetDescriptor { Layout = new BindingSetLayout { Entries = [] } });
        var material = new Material
        {
            Pipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
            {
                VertexShader = driver.CreateShaderModule("", "vs"),
                FragmentShader = driver.CreateShaderModule("", "fs"),
            }),
            BindingSets = [bindingSet],
        };

        var instance = new MaterialInstance(material);

        Assert.Same(bindingSet, instance.GetBindingSet(0));
    }
}
