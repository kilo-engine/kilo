using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// System that renders mesh entities through the RenderGraph.
/// Draws skybox first (at far plane), then opaque objects, then transparent objects.
/// </summary>
public sealed class RenderSystem
{
    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var scene = world.GetResource<GpuSceneData>();
        var ws = world.GetResource<WindowSize>();
        var skybox = context.Skybox;

        var graph = context.RenderGraph;

        graph.AddPass("Forward", setup: pass =>
        {
            var depth = pass.CreateTexture(new TextureDescriptor
            {
                Width = ws.Width,
                Height = ws.Height,
                Format = DriverPixelFormat.Depth24Plus,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(depth);
            pass.DepthStencilAttachment(depth, DriverLoadAction.Clear, DriverStoreAction.Store, clearDepth: 1.0f);

            var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor
            {
                Width = ws.Width,
                Height = ws.Height,
                Format = driver.SwapchainFormat,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(backbuffer);
            pass.ColorAttachment(backbuffer, DriverLoadAction.Clear, DriverStoreAction.Store,
                clearColor: new Vector4(0.1f, 0.1f, 0.12f, 1f));

            var cameraBufferHandle = pass.ImportBuffer("CameraBuffer", new BufferDescriptor
            {
                Size = scene.CameraBuffer.Size,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            });
            var objectBufferHandle = pass.ImportBuffer("ObjectDataBuffer", new BufferDescriptor
            {
                Size = scene.ObjectDataBuffer.Size,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            });
            var lightBufferHandle = pass.ImportBuffer("LightBuffer", new BufferDescriptor
            {
                Size = scene.LightBuffer.Size,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            });

            pass.ReadBuffer(cameraBufferHandle);
            pass.ReadBuffer(objectBufferHandle);
            pass.ReadBuffer(lightBufferHandle);
        }, execute: ctx =>
        {
            var encoder = ctx.Encoder;
            encoder.SetViewport(0, 0, ws.Width, ws.Height);

            // 1) Skybox — depth LessEqual, depth write off, renders at far plane
            if (skybox?.Pipeline != null)
            {
                var camData = new CameraData[1];
                camData[0] = scene.PendingCamera;
                skybox.CameraBuffer.UploadData<CameraData>(camData);

                encoder.SetPipeline(skybox.Pipeline);
                encoder.SetVertexBuffer(0, skybox.VertexBuffer);
                encoder.SetIndexBuffer(skybox.IndexBuffer);
                encoder.SetBindingSet(0, skybox.CameraBinding);
                encoder.SetBindingSet(1, skybox.TextureBinding);
                encoder.DrawIndexed(36);
            }

            // 2) Opaque draws — depth write enabled (pipeline controlled)
            for (int i = 0; i < scene.OpaqueCount; i++)
            {
                scene.EmitDraw(encoder, context, i);
            }

            // 3) Transparent draws — depth write off, alpha blend, sorted back-to-front
            for (int i = scene.OpaqueCount; i < scene.DrawCount; i++)
            {
                scene.EmitDraw(encoder, context, i);
            }
        });
    }
}
