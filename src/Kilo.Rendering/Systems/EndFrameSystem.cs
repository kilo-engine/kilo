using Kilo.ECS;

namespace Kilo.Rendering;

public sealed class EndFrameSystem
{
    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        context.RenderGraph.Execute(context.Driver);
        context.Driver.Present();
    }
}
