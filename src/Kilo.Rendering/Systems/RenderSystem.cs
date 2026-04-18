using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// System that renders mesh entities through the RenderGraph.
/// </summary>
public sealed class RenderSystem
{
    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var scene = world.GetResource<GpuSceneData>();
        var ws = world.GetResource<WindowSize>();

        var graph = context.RenderGraph;

        graph.AddPass("ForwardOpaque", setup: pass =>
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
            pass.ColorAttachment(backbuffer, DriverLoadAction.Clear, DriverStoreAction.Store, clearColor: new Vector4(0.1f, 0.1f, 0.12f, 1f));

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

            for (int i = 0; i < scene.DrawCount; i++)
            {
                var draw = scene.DrawData[i];
                if (draw.MeshHandle < 0 || draw.MeshHandle >= context.Meshes.Count) continue;
                if (draw.MaterialId < 0 || draw.MaterialId >= context.Materials.Count) continue;

                var mesh = context.Meshes[draw.MeshHandle];
                var material = context.Materials[draw.MaterialId];

                encoder.SetPipeline(material.Pipeline);
                encoder.SetVertexBuffer(0, mesh.VertexBuffer);
                encoder.SetIndexBuffer(mesh.IndexBuffer);
                encoder.SetBindingSet(0, material.BindingSets[0]);
                encoder.SetBindingSet(1, material.BindingSets[1], (uint)(i * 256));
                encoder.SetBindingSet(2, material.BindingSets[2]);
                if (material.BindingSets.Length > 3)
                    encoder.SetBindingSet(3, material.BindingSets[3]);
                if (material.BindingSets.Length > 4)
                    encoder.SetBindingSet(4, material.BindingSets[4]);
                encoder.DrawIndexed((int)mesh.IndexCount);
            }
        });
    }
}
