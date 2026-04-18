using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Driver.WebGPUImpl;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Scene;
using Silk.NET.Windowing;

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
        app.AddResource(new WindowSize { Width = _settings.Width, Height = _settings.Height });
        app.AddResource(new GpuSceneData());

        app.AddSystem(KiloStage.PostUpdate, new LocalToWorldSystem().Update);
        app.AddSystem(KiloStage.First, new CameraSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new FrustumCullingSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new PrepareGpuSceneSystem().Update);

        app.AddSystem(KiloStage.Last, new BeginFrameSystem().Update);
        app.AddSystem(KiloStage.Last, new ShadowMapSystem().Update);
        app.AddSystem(KiloStage.Last, new RenderSystem().Update);
        app.AddSystem(KiloStage.Last, new SpriteRenderSystem().Update);
        app.AddSystem(KiloStage.Last, new TextRenderSystem().Update);
        app.AddSystem(KiloStage.Last, new EndFrameSystem().Update);
        app.AddSystem(KiloStage.Last, new WindowResizeSystem().Update);
    }

    public void Run(KiloApp app)
    {
        Console.WriteLine("[Kilo] Creating window...");
        var window = WindowHelper.CreateWindow(_settings);
        var context = app.World.GetResource<RenderContext>();
        var scene = app.World.GetResource<GpuSceneData>();

        window.Load += () =>
        {
            Console.WriteLine("[Kilo] Window loaded, initializing WebGPU...");
            var driver = WebGPUDriverFactory.Create(window, _settings);
            context.Driver = driver;
            SceneInitializer.Initialize(context, scene, driver);
            InputWiring.WireInputEvents(window, app.World);
            Console.WriteLine("[Kilo] WebGPU initialized successfully.");
        };

        window.Render += _ =>
        {
            app.Update();
        };

        window.Resize += size =>
        {
            var ws = app.World.GetResource<WindowSize>();
            ws.Width = size.X;
            ws.Height = size.Y;
            context.WindowResized = true;
        };

        window.Closing += () =>
        {
            context.RenderGraph.Dispose();
            context.Driver?.Dispose();
        };

        window.Run();
        window.Dispose();
    }
}
