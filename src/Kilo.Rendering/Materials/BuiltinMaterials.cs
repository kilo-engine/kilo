using System.Numerics;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering.Materials;

internal static class BuiltinMaterials
{
    /// <summary>
    /// Creates the default BasicLit material via MaterialManager.
    /// All binding set details are centralized in MaterialManager — no duplication here.
    /// </summary>
    public static void CreateDefaultMaterial(RenderContext context, GpuSceneData scene, IRenderDriver driver)
    {
        context.MaterialManager.CreateMaterial(context, scene, new MaterialDescriptor
        {
            BaseColor = Vector4.One,
            Metallic = 0.0f,
            Roughness = 0.5f,
        });
    }
}
