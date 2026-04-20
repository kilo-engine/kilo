using System.Numerics;
using System.Runtime.InteropServices;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// GPU sprite instance data. Padded to 256 bytes for WebGPU dynamic uniform alignment.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SpriteInstanceData
{
    public Matrix4x4 Model;
    public Matrix4x4 Projection;
    public Vector4 Color;

    private Vector4 _pad0;
    private Vector4 _pad1;
    private Vector4 _pad2;
    private Vector4 _pad3;
    private Vector4 _pad4;
    private Vector4 _pad5;
    private Vector4 _pad6;

    public static int Size => 256;
}

public sealed class SpriteRenderSystem
{
    /// <summary>Backward-compatible entry point with default screen context.</summary>
    public void Update(KiloWorld world)
    {
        var ws = world.GetResource<WindowSize>();
        var ctx = new CameraRenderContext(new ActiveCameraEntry
        {
            Target = CameraTarget.Screen,
            CameraType = CameraType.Scene,
            RenderWidth = ws.Width,
            RenderHeight = ws.Height,
        });
        AddSpritePass(ctx, world);
    }

    public void AddSpritePass(CameraRenderContext ctx, KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var graph = context.RenderGraph;

        // Orthographic projection
        float aspect = (float)ctx.Width / ctx.Height;
        const float HalfHeight = 5f;
        float halfW = HalfHeight * aspect;
        var projection = Matrix4x4.CreateOrthographicOffCenter(-halfW, halfW, -HalfHeight, HalfHeight, -1f, 1f);

        // Collect sprites
        var query = world.QueryBuilder()
            .With<LocalToWorld>()
            .With<Sprite>()
            .Build();

        var sprites = new List<(Matrix4x4 Model, Vector4 Color)>();

        var iter = query.Iter();
        while (iter.Next())
        {
            var transforms = iter.Data<LocalToWorld>(iter.GetColumnIndexOf<LocalToWorld>());
            var spriteData = iter.Data<Sprite>(iter.GetColumnIndexOf<Sprite>());

            for (int i = 0; i < iter.Count; i++)
            {
                var model = transforms[i].Value * Matrix4x4.CreateScale(spriteData[i].Size.X, spriteData[i].Size.Y, 1f);
                sprites.Add((model, spriteData[i].Tint));
            }
        }

        int totalDrawn = sprites.Count;
        const int UniformAlign = 256;
        int maxSprites = context.Sprite.UniformBuffer is not null ? (int)(context.Sprite.UniformBuffer.Size / (nuint)UniformAlign) : 0;
        int drawCount = Math.Min(sprites.Count, maxSprites);

        // Bulk upload all sprite instance data before drawing
        if (drawCount > 0 && context.Sprite.UniformBuffer is not null)
        {
            var instanceData = new SpriteInstanceData[drawCount];
            for (int i = 0; i < drawCount; i++)
            {
                instanceData[i].Model = sprites[i].Model;
                instanceData[i].Projection = projection;
                instanceData[i].Color = sprites[i].Color;
            }
            context.Sprite.UniformBuffer.UploadData<SpriteInstanceData>(instanceData.AsSpan());
        }

        graph.AddPass($"{ctx.Prefix}Sprite", setup: pass =>
        {
            var sceneColor = pass.ImportTexture(ctx.SceneColorName, new TextureDescriptor
            {
                Width = ctx.Width,
                Height = ctx.Height,
                Format = DriverPixelFormat.RGBA16Float,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
            });
            pass.WriteTexture(sceneColor);
            pass.ColorAttachment(sceneColor, DriverLoadAction.Load, DriverStoreAction.Store);

            if (context.Sprite.UniformBuffer is not null)
            {
                var uniformBufferHandle = pass.ImportBuffer("SpriteUniformBuffer", new BufferDescriptor
                {
                    Size = context.Sprite.UniformBuffer.Size,
                    Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
                });
                pass.ReadBuffer(uniformBufferHandle);
            }
        }, execute: exeCtx =>
        {
            var encoder = exeCtx.Encoder;
            encoder.SetPipeline(context.Sprite.Pipeline!);
            encoder.SetVertexBuffer(0, context.Sprite.QuadVertexBuffer!);
            encoder.SetIndexBuffer(context.Sprite.QuadIndexBuffer!);

            for (int i = 0; i < drawCount; i++)
            {
                uint offset = (uint)(i * UniformAlign);
                encoder.SetBindingSet(0, context.Sprite.BindingSet!, offset);
                encoder.DrawIndexed(6);
            }
        });
    }
}
