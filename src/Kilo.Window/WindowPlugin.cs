using Kilo.ECS;

namespace Kilo.Window;

public sealed class WindowPlugin : IKiloPlugin
{
    public void Build(KiloApp app)
    {
        app.AddResource(new InputState());
        app.AddResource(new InputSettings());
        app.AddSystem(KiloStage.First, new InputPollSystem().Update);
    }
}
