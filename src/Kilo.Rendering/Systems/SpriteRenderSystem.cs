using System.Numerics;
using System.Runtime.InteropServices;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;

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
    private static int _logFrame;

    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var windowSize = world.GetResource<WindowSize>();

        _logFrame++;
        bool shouldLog = _logFrame <= 3;

        // Orthographic projection
        float aspect = (float)windowSize.Width / windowSize.Height;
        float halfH = 5f;
        float halfW = halfH * aspect;
        var projection = Matrix4x4.CreateOrthographicOffCenter(-halfW, halfW, -halfH, halfH, -1f, 1f);

        if (shouldLog) Console.WriteLine($"[SpriteRender] Frame {_logFrame}: aspect={aspect:F2}");

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
        int maxSprites = context.UniformBuffer is not null ? (int)(context.UniformBuffer.Size / (nuint)UniformAlign) : 0;
        int drawCount = Math.Min(sprites.Count, maxSprites);

        // Bulk upload all sprite instance data before drawing
        if (drawCount > 0 && context.UniformBuffer is not null)
        {
            var instanceData = new SpriteInstanceData[drawCount];
            for (int i = 0; i < drawCount; i++)
            {
                instanceData[i].Model = sprites[i].Model;
                instanceData[i].Projection = projection;
                instanceData[i].Color = sprites[i].Color;
            }
            context.UniformBuffer.UploadData<SpriteInstanceData>(instanceData.AsSpan());
        }

        // Add sprite pass to the shared RenderGraph
        var graph = context.RenderGraph;

        graph.AddPass("SpritePass", setup: pass =>
        {
            var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor
            {
                Width = windowSize.Width,
                Height = windowSize.Height,
                Format = driver.SwapchainFormat,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(backbuffer);
            pass.ColorAttachment(backbuffer, DriverLoadAction.Load, DriverStoreAction.Store);

            if (context.UniformBuffer is not null)
            {
                var uniformBufferHandle = pass.ImportBuffer("SpriteUniformBuffer", new BufferDescriptor
                {
                    Size = context.UniformBuffer.Size,
                    Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
                });
                pass.ReadBuffer(uniformBufferHandle);
            }
        }, execute: ctx =>
        {
            var encoder = ctx.Encoder;
            encoder.SetPipeline(context.SpritePipeline!);
            encoder.SetVertexBuffer(0, context.QuadVertexBuffer!);
            encoder.SetIndexBuffer(context.QuadIndexBuffer!);

            for (int i = 0; i < drawCount; i++)
            {
                uint offset = (uint)(i * UniformAlign);
                encoder.SetBindingSet(0, context.BindingSet!, offset);
                encoder.DrawIndexed(6);

                if (shouldLog && i < 3)
                    Console.WriteLine($"[SpriteRender]   Sprite {i}: offset={offset}");
            }
        });

        if (shouldLog) Console.WriteLine($"[SpriteRender] Total drawn: {totalDrawn}");
    }
}
