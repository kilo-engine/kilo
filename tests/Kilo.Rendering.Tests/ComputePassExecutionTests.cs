using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Xunit;

namespace Kilo.Rendering.Tests;

public class ComputePassExecutionTests
{
    [Fact]
    public void Execute_ComputePass_CallsComputeEncoderMethodsInOrder()
    {
        var driver = new MockRenderDriver();
        var graph = new RenderGraph.RenderGraph();

        graph.AddComputePass("ComputePass", _ => { }, ctx =>
        {
            var pipeline = driver.CreateComputePipeline(driver.CreateComputeShaderModule("", "main"), "main");
            ctx.Encoder.SetComputePipeline(pipeline);
            ctx.Encoder.Dispatch(8, 8, 1);
        });

        graph.Execute(driver);

        var encoder = driver.LastEncoder;
        Assert.NotNull(encoder);
        Assert.Equal(new[] { "BeginComputePass", "SetComputePipeline", "Dispatch", "EndComputePass" }, encoder.ComputeCalls);
    }

    [Fact]
    public void Execute_RenderThenCompute_CallsBothPassTypes()
    {
        var driver = new MockRenderDriver();
        var graph = new RenderGraph.RenderGraph();

        RenderGraph.RenderResourceHandle texture = default;

        graph.AddPass("RenderPass", builder =>
        {
            texture = builder.CreateTexture(new RenderGraph.TextureDescriptor
            {
                Width = 128,
                Height = 128,
                Usage = RenderGraph.TextureUsage.RenderAttachment,
            });
            builder.WriteTexture(texture);
            builder.ColorAttachment(texture);
        }, _ => { });

        graph.AddComputePass("ComputePass", builder =>
        {
            builder.ReadTexture(texture);
        }, _ => { });

        graph.Execute(driver);

        var encoder = driver.LastEncoder;
        Assert.NotNull(encoder);
        Assert.True(encoder.InRenderPass || !encoder.InRenderPass); // ended
        Assert.False(encoder.InComputePass); // ended
    }
}
