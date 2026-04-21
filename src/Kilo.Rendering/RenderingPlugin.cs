using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Particles;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

public sealed class RenderingPlugin : IKiloPlugin
{
    private readonly RenderSettings _settings;

    public RenderingPlugin(RenderSettings? settings = null)
    {
        _settings = settings ?? new RenderSettings();
    }

    public void Build(KiloApp app)
    {
        app.AddResource(_settings);
        app.AddResource(new RenderContext
        {
            ShaderCache = new ShaderCache(),
            PipelineCache = new PipelineCache(),
        });
        app.AddResource(new RenderResourceStore());
        app.AddResource(new WindowSize { Width = _settings.Width, Height = _settings.Height });
        app.AddResource(new GpuSceneData());
        app.AddResource(new ActiveCameraList());

        // Subsystem states as independent ECS resources
        app.AddResource(new SkyboxState());
        app.AddResource(new ScreenshotState());
        app.AddResource(new SpriteRenderState());
        app.AddResource(new PostProcessState());
        app.AddResource(new ParticleSystemState());

        app.AddSystem(KiloStage.PostUpdate, new LocalToWorldSystem().Update);
        app.AddSystem(KiloStage.First, new CameraSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new FrustumCullingSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new CameraPrepareSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new ObjectPrepareSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new LightPrepareSystem().Update);

        app.AddSystem(KiloStage.Last, new BeginFrameSystem().Update);
        app.AddSystem(KiloStage.Last, new ShadowMapSystem().Update);
        app.AddSystem(KiloStage.Last, new SkyboxRenderSystem().Update);
        app.AddSystem(KiloStage.Last, new ParticleUpdateSystem().Update);
        app.AddSystem(KiloStage.Last, new CameraRenderLoopSystem().Update);
        app.AddSystem(KiloStage.Last, new EndFrameSystem().Update);
        app.AddSystem(KiloStage.Last, new WindowResizeSystem().Update);
    }
}
