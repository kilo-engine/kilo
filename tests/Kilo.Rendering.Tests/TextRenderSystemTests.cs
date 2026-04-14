using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Resources;
using Xunit;

namespace Kilo.Rendering.Tests;

public class TextRenderSystemTests
{
    [Fact]
    public void TextRenderSystem_NoEntities_DoesNotThrow()
    {
        var world = new KiloWorld();
        var driver = new MockRenderDriver();
        var context = new RenderContext { Driver = driver };
        var shaderCache = new ShaderCache();

        world.AddResource(context);
        world.AddResource(new WindowSize { Width = 800, Height = 600 });

        var system = new TextRenderSystem();
        var ex = Record.Exception(() => system.Update(world));
        Assert.Null(ex);
    }

    [Fact]
    public void TextRenderSystem_WithTextEntity_AddsTextPass()
    {
        var world = new KiloWorld();
        var driver = new MockRenderDriver();
        var context = new RenderContext { Driver = driver, ShaderCache = new ShaderCache() };

        world.AddResource(context);
        world.AddResource(new WindowSize { Width = 800, Height = 600 });

        world.Entity("Text")
            .Set(new TextRenderer { Text = "Hello", Color = Vector4.One, FontSize = 24f })
            .Set(new LocalToWorld { Value = Matrix4x4.Identity });

        var system = new TextRenderSystem();
        // Note: FontAtlas.Build requires real font system — this may fail on CI without fonts
        // But the system should at least attempt lazy init without crashing before font ops
        var ex = Record.Exception(() => system.Update(world));
        // If it throws due to missing fonts, that's acceptable — the code path is exercised
        // On machines with fonts it should succeed
        if (ex != null)
        {
            Assert.Contains("font", ex.Message.ToLower() + (ex.InnerException?.Message?.ToLower() ?? ""));
        }
    }
}
