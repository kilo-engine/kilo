using Kilo.ECS;

namespace Kilo.Rendering;

public sealed class EndFrameSystem
{
    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var graph = context.RenderGraph;
        var screenshot = world.GetResource<ScreenshotState>();

        // Add screenshot copy pass as the very last pass (after sprites/text)
        if (screenshot.Requested)
        {
            var ws = world.GetResource<WindowSize>();
            var width = ws.Width;
            var height = ws.Height;
            // BGRA format: 4 bytes per pixel
            var alignedBytesPerRow = (uint)(((width * 4) + 255) & ~255);
            var requiredSize = (nuint)(alignedBytesPerRow * height);

            var screenshotBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor
            {
                Size = requiredSize,
                Usage = RenderGraph.BufferUsage.CopyDst | RenderGraph.BufferUsage.MapRead,
            });

            graph.AddPass("ScreenshotCopy", setup: pass =>
            {
                var backbuffer = pass.ImportTexture("Backbuffer", new RenderGraph.TextureDescriptor
                {
                    Width = width,
                    Height = height,
                    Format = driver.SwapchainFormat,
                    Usage = RenderGraph.TextureUsage.RenderAttachment | RenderGraph.TextureUsage.CopySrc,
                });
                pass.ReadTexture(backbuffer);
            }, execute: ctx =>
            {
                var backbufferTexture = ctx.GetTexture("Backbuffer");
                ctx.Encoder.CopyTextureToBuffer(backbufferTexture, new Driver.TextureCopyRegion
                {
                    Width = backbufferTexture.Width,
                    Height = backbufferTexture.Height,
                }, screenshotBuffer, 0);
            });

            screenshot.Requested = false;
            screenshot.HasPending = true;
            screenshot.Buffer = screenshotBuffer;
            screenshot.AlignedBytesPerRow = alignedBytesPerRow;
            screenshot.Width = width;
            screenshot.Height = height;
        }

        graph.Execute(driver);
        driver.Present();
    }
}
