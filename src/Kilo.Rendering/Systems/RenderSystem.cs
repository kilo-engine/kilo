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
    /// <summary>
    /// Backward-compatible entry point. Creates a default screen camera context.
    /// </summary>
    public void Update(KiloWorld world)
    {
        var ws = world.GetResource<WindowSize>();
        var scene = world.GetResource<GpuSceneData>();
        var ctx = new CameraRenderContext(new ActiveCameraEntry
        {
            CameraData = scene.PendingCamera,
            Target = CameraTarget.Screen,
            ClearSettings = CameraClearSettings.Skybox,
            CameraType = CameraType.Scene,
            RenderWidth = ws.Width,
            RenderHeight = ws.Height,
            PostProcessEnabled = true,
        });
        AddForwardPass(ctx, world);
    }

    /// <summary>
    /// Adds the forward rendering pass for a specific camera context.
    /// Called by CameraRenderLoopSystem for each active camera.
    /// </summary>
    public void AddForwardPass(CameraRenderContext ctx, KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var scene = world.GetResource<GpuSceneData>();
        var store = world.GetResource<RenderResourceStore>();
        var skybox = world.GetResource<SkyboxState>();
        var graph = context.RenderGraph;
        var pp = world.GetResource<PostProcessState>();

        // Ensure per-camera resources (SceneColor texture + camera buffer)
        var camTex = pp.GetCameraTextures(ctx.Prefix);
        camTex.EnsureSceneColor(driver, ctx.Width, ctx.Height);
        camTex.EnsureCameraBuffer(driver);
        graph.RegisterExternalTexture(ctx.SceneColorName, camTex.SceneColorTexture!);

        graph.AddPass($"{ctx.Prefix}Forward", setup: pass =>
        {
            var depth = pass.CreateTexture(new TextureDescriptor
            {
                Width = ctx.Width,
                Height = ctx.Height,
                Format = DriverPixelFormat.Depth24Plus,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(depth);
            pass.DepthStencilAttachment(depth, DriverLoadAction.Clear, DriverStoreAction.Store, clearDepth: 1.0f);

            var sceneColor = pass.ImportTexture(ctx.SceneColorName, new TextureDescriptor
            {
                Width = ctx.Width,
                Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
            });
            pass.WriteTexture(sceneColor);

            var clearSettings = ctx.Camera.ClearSettings;
            var loadAction = clearSettings.Mode == CameraClearMode.DontClear ? DriverLoadAction.Load : DriverLoadAction.Clear;
            var clearColor = clearSettings.Mode == CameraClearMode.Color ? clearSettings.Color : new Vector4(0.1f, 0.1f, 0.12f, 1f);
            pass.ColorAttachment(sceneColor, loadAction, DriverStoreAction.Store, clearColor: clearColor);

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
        }, execute: exeCtx =>
        {
            var encoder = exeCtx.Encoder;
            encoder.SetViewport(0, 0, (uint)ctx.Width, (uint)ctx.Height);

            // Upload camera data to per-camera buffer (not the shared scene.CameraBuffer)
            // to avoid overwrite when multiple cameras use the same command encoder.
            var camData = new CameraData[1];
            camData[0] = ctx.Camera.CameraData;
            camData[0].LightCount = scene.LightCount;
            camTex.CameraBuffer!.UploadData<CameraData>(camData);

            // Set the per-camera buffer override for EmitDraw
            scene.CurrentCameraBuffer = camTex.CameraBuffer;

            // 1) Skybox — only when clear mode is Skybox
            if (skybox.Pipeline != null && ctx.Camera.ClearSettings.Mode == CameraClearMode.Skybox)
            {
                skybox.CameraBuffer.UploadData<CameraData>(camData);

                encoder.SetPipeline(skybox.Pipeline);
                encoder.SetVertexBuffer(0, skybox.VertexBuffer);
                encoder.SetIndexBuffer(skybox.IndexBuffer);
                encoder.SetBindingSet(0, skybox.CameraBinding);
                encoder.SetBindingSet(1, skybox.TextureBinding);
                encoder.DrawIndexed(36);
            }

            // 2) Opaque draws
            for (int i = 0; i < scene.OpaqueCount; i++)
                DrawEmitter.EmitDraw(encoder, scene, store, i);

            // 3) Transparent draws
            for (int i = scene.OpaqueCount; i < scene.DrawCount; i++)
                DrawEmitter.EmitDraw(encoder, scene, store, i);
        });
    }
}
