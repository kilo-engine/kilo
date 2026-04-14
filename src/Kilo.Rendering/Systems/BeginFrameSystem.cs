using Kilo.ECS;

namespace Kilo.Rendering;

public sealed class BeginFrameSystem
{
    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        context.Driver.BeginFrame();
        context.RenderGraph.BeginFrame();
        context.WindowResized = false;
    }
}
