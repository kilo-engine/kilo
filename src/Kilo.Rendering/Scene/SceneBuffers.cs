using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;

namespace Kilo.Rendering.Scene;

internal static class SceneBuffers
{
    public static void Create(GpuSceneData scene, IRenderDriver driver)
    {
        const int ObjectBufferSize = 64 * 1024; // 64KB
        const int LightBufferSize = 4 * 1024;   // 4KB

        scene.CameraBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)CameraData.Size,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });

        scene.ObjectDataBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)ObjectBufferSize,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });

        scene.LightBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)LightBufferSize,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });
    }
}
