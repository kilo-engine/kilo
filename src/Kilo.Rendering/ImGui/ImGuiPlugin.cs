using Kilo.ECS;

namespace Kilo.Rendering;

/// <summary>
/// Plugin that registers ImGui controller and render system.
/// Add after RenderingPlugin. The controller must be initialized separately
/// after the driver is created (typically in window.Load).
/// </summary>
public sealed class ImGuiPlugin : IKiloPlugin
{
    public void Build(KiloApp app)
    {
        app.AddResource(new ImGuiController());
        // ImGuiRenderSystem must be added AFTER CameraRenderLoopSystem and BEFORE EndFrameSystem.
        // Add it manually at the correct position: app.AddSystem(KiloStage.Last, new ImGuiRenderSystem().Update);
    }
}
