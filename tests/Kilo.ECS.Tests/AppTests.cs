using Kilo.ECS;
using Xunit;

namespace Kilo.ECS.Tests;

public sealed class AppTests : IDisposable
{
    private readonly KiloApp _app = new();

    public void Dispose()
    {
        _app.World.Dispose();
    }

    [Fact]
    public void App_Run_DoesNotThrow()
    {
        _app.Run();
    }

    [Fact]
    public void App_AddSystem_Executes()
    {
        var executed = false;
        _app.AddSystem(KiloStage.Update, world =>
        {
            executed = true;
        });

        _app.Run();
        Assert.True(executed);
    }

    [Fact]
    public void App_SystemsExecuteInOrder()
    {
        var order = new List<string>();

        _app.AddSystem(KiloStage.First, _ => order.Add("first"));
        _app.AddSystem(KiloStage.Update, _ => order.Add("update"));
        _app.AddSystem(KiloStage.Last, _ => order.Add("last"));

        _app.Run();

        Assert.Equal(new[] { "first", "update", "last" }, order);
    }

    [Fact]
    public void App_Startup_RunsOnce()
    {
        var count = 0;
        _app.AddSystem(KiloStage.Startup, _ => count++);

        _app.Run(); // First run: startup + update
        _app.Run(); // Second run: only update

        Assert.Equal(1, count);
    }

    [Fact]
    public void App_AddPlugin_Registers()
    {
        var pluginRan = false;
        var action = new Action(() => pluginRan = true);

        _app.AddPlugin(new TestPlugin(action));
        _app.Run();

        Assert.True(pluginRan);
    }

    [Fact]
    public void App_AddResource_Accessible()
    {
        _app.AddResource(new TimeResource { Delta = 0.016f });

        var accessed = false;
        _app.AddSystem(KiloStage.Update, world =>
        {
            var time = world.GetResource<TimeResource>();
            Assert.Equal(0.016f, time.Delta);
            accessed = true;
        });

        _app.Run();
        Assert.True(accessed);
    }

    [Fact]
    public void App_CustomStage()
    {
        var order = new List<string>();
        var physics = KiloStage.Custom("Physics");

        _app.AddStage(physics)
            .After(KiloStage.Update)
            .Before(KiloStage.PostUpdate)
            .Build();

        _app.AddSystem(KiloStage.Update, _ => order.Add("update"));
        _app.AddSystem(physics, _ => order.Add("physics"));
        _app.AddSystem(KiloStage.PostUpdate, _ => order.Add("post"));

        _app.Run();

        Assert.Equal(new[] { "update", "physics", "post" }, order);
    }

    class TestPlugin : IKiloPlugin
    {
        private readonly Action _onBuild;

        public TestPlugin(Action onBuild) => _onBuild = onBuild;

        public void Build(KiloApp app)
        {
            app.AddSystem(KiloStage.Update, _ => _onBuild());
        }
    }

    class TimeResource
    {
        public float Delta;
    }
}
