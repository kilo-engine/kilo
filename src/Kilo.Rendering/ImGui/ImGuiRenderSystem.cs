using Kilo.ECS;
using Kilo.Rendering.Driver;

namespace Kilo.Rendering;

/// <summary>
/// ECS system that renders ImGui on top of the scene.
/// Inserts an "ImGui" render pass into the render graph between
/// CameraRenderLoopSystem and EndFrameSystem.
/// </summary>
public sealed class ImGuiRenderSystem
{
    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var controller = world.GetResource<ImGuiController>();
        var driver = context.Driver;
        var graph = context.RenderGraph;
        var ws = world.GetResource<WindowSize>();

        if (driver == null || ws.Width <= 0 || ws.Height <= 0) return;

        // Upload ImGui draw data to GPU buffers
        controller.UpdateBuffers();

        graph.AddPass("ImGui", setup: pass =>
        {
            var backbuffer = pass.ImportTexture("Backbuffer", new RenderGraph.TextureDescriptor
            {
                Width = ws.Width,
                Height = ws.Height,
                Format = driver.SwapchainFormat,
                Usage = RenderGraph.TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(backbuffer);
            pass.ColorAttachment(backbuffer, DriverLoadAction.Load, DriverStoreAction.Store);
        }, execute: ctx =>
        {
            controller.Render(ctx.Encoder);
        });
    }
}
