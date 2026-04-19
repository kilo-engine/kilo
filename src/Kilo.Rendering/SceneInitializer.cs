using Kilo.Rendering.Driver;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// Facade for the default GPU initialization sequence.
/// </summary>
public static class SceneInitializer
{
    /// <summary>
    /// Runs the full default initialization: GPU buffers → default cube → default material → sprite pipeline.
    /// </summary>
    public static void Initialize(RenderContext context, GpuSceneData scene, IRenderDriver driver)
    {
        SceneBuffers.Create(scene, driver);
        context.RenderGraph.RegisterExternalTexture("ShadowDepth", scene.ShadowDepthTexture!);
        BuiltinMeshes.CreateDefaultCube(context, driver);
        BuiltinMaterials.CreateDefaultMaterial(context, scene, driver);
        SpriteResources.Create(context, driver);
    }
}
