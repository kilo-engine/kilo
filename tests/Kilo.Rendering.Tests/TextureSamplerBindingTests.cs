using Kilo.Rendering.Driver;
using Xunit;

namespace Kilo.Rendering.Tests;

public class TextureSamplerBindingTests
{
    [Fact]
    public void CreateTextureView_AndSampler_Succeeds()
    {
        var driver = new MockRenderDriver();
        var texture = driver.CreateTexture(new RenderGraph.TextureDescriptor { Width = 256, Height = 256 });
        var view = driver.CreateTextureView(texture, new TextureViewDescriptor { Format = DriverPixelFormat.RGBA8Unorm });
        var sampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            MipFilter = FilterMode.Linear,
        });

        Assert.NotNull(texture);
        Assert.NotNull(view);
        Assert.NotNull(sampler);
    }

    [Fact]
    public void CreateBindingSet_WithTextureAndSampler_Succeeds()
    {
        var driver = new MockRenderDriver();
        var texture = driver.CreateTexture(new RenderGraph.TextureDescriptor { Width = 256, Height = 256 });
        var view = driver.CreateTextureView(texture, new TextureViewDescriptor { Format = DriverPixelFormat.RGBA8Unorm });
        var sampler = driver.CreateSampler(new SamplerDescriptor());

        var layout = new BindingSetLayout
        {
            Entries =
            [
                new BindingLayoutEntry { Binding = 0, Type = BindingType.Texture, Visibility = ShaderVisibility.Fragment },
                new BindingLayoutEntry { Binding = 1, Type = BindingType.Sampler, Visibility = ShaderVisibility.Fragment },
            ]
        };

        var descriptor = new BindingSetDescriptor
        {
            Layout = layout,
            Textures = [new TextureBinding { TextureView = view, Binding = 0 }],
            Samplers = [new SamplerBinding { Sampler = sampler, Binding = 1 }],
        };

        var bindingSet = driver.CreateBindingSet(descriptor);

        Assert.NotNull(bindingSet);
        Assert.Equal(1, driver.CreateBindingSetCallCount);
    }
}
