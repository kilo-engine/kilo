using System.Numerics;
using System.Runtime.InteropServices;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;
using Kilo.Rendering.Shaders;

namespace Kilo.Rendering;

/// <summary>
/// System that renders TextRenderer entities via the shared RenderGraph.
/// </summary>
public sealed class TextRenderSystem
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
        AddTextPass(ctx, world);
    }

    private IRenderPipeline? _textPipeline;
    private IBuffer? _textUniformBuffer;
    private IBuffer? _textQuadVB;
    private IBuffer? _textQuadIB;
    private IBindingSet? _textBindingSet;
    private FontAtlas? _fontAtlas;

    [StructLayout(LayoutKind.Sequential)]
    private struct TextUniforms
    {
        public Matrix4x4 Projection;
        private Vector4 _pad0;
        private Vector4 _pad1;
        private Vector4 _pad2;
        private Vector4 _pad3;
        private Vector4 _pad4;
        private Vector4 _pad5;
        private Vector4 _pad6;
        private Vector4 _pad7;
        private Vector4 _pad8;
        private Vector4 _pad9;
    }

    public void AddTextPass(CameraRenderContext ctx, KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;

        // Lazy init
        if (_fontAtlas == null)
        {
            _fontAtlas = FontAtlas.Build(driver, 96f);
        }

        if (_textPipeline == null)
        {
            var vs = context.ShaderCache.GetOrCreateShader(driver, TextShaders.WGSL, "vs_main");
            var fs = context.ShaderCache.GetOrCreateShader(driver, TextShaders.WGSL, "fs_main");

            _textPipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
            {
                VertexShader = vs,
                FragmentShader = fs,
                Topology = DriverPrimitiveTopology.TriangleList,
                VertexBuffers =
                [
                    new VertexBufferLayout
                    {
                        ArrayStride = 4 * sizeof(float),
                        Attributes =
                        [
                            new VertexAttributeDescriptor { ShaderLocation = 0, Format = VertexFormat.Float32x2, Offset = 0 },
                            new VertexAttributeDescriptor { ShaderLocation = 1, Format = VertexFormat.Float32x2, Offset = (nuint)(2 * sizeof(float)) },
                        ]
                    }
                ],
                ColorTargets =
                [
                    new ColorTargetDescriptor
                    {
                        Format = DriverPixelFormat.RGBA16Float,
                        Blend = new BlendStateDescriptor
                        {
                            Color = new BlendComponentDescriptor
                            {
                                SrcFactor = DriverBlendFactor.SrcAlpha,
                                DstFactor = DriverBlendFactor.OneMinusSrcAlpha,
                            },
                            Alpha = new BlendComponentDescriptor
                            {
                                SrcFactor = DriverBlendFactor.One,
                                DstFactor = DriverBlendFactor.OneMinusSrcAlpha,
                            }
                        }
                    }
                ],
                DepthStencil = null,
            });

            _textUniformBuffer = driver.CreateBuffer(new BufferDescriptor
            {
                Size = 256,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            });

            var fontSampler = driver.CreateSampler(new SamplerDescriptor
            {
                MinFilter = FilterMode.Linear,
                MagFilter = FilterMode.Linear,
            });

            _textBindingSet = driver.CreateBindingSetForPipeline(_textPipeline, 0,
                [new UniformBufferBinding { Buffer = _textUniformBuffer, Binding = 0 }],
                [new TextureBinding { Binding = 1, TextureView = _fontAtlas.TextureView }],
                [new SamplerBinding { Binding = 2, Sampler = fontSampler }]);

            float[] quadVerts = [0, 0, 0, 0, 1, 0, 1, 0, 0, 1, 0, 1, 1, 1, 1, 1];
            uint[] quadIdx = [0u, 1, 2, 2, 1, 3];
            _textQuadVB = driver.CreateBuffer(new BufferDescriptor { Size = (nuint)(quadVerts.Length * 4), Usage = BufferUsage.Vertex | BufferUsage.CopyDst });
            _textQuadVB.UploadData<float>(quadVerts);
            _textQuadIB = driver.CreateBuffer(new BufferDescriptor { Size = (nuint)(quadIdx.Length * 4), Usage = BufferUsage.Index | BufferUsage.CopyDst });
            _textQuadIB.UploadData<uint>(quadIdx);
        }

        // Collect text entities
        var textQuery = world.QueryBuilder()
            .With<TextRenderer>()
            .With<LocalToWorld>()
            .Build();

        var texts = new List<(string Text, Vector4 Color, Matrix4x4 World)>();
        var textIter = textQuery.Iter();
        while (textIter.Next())
        {
            var renderers = textIter.Data<TextRenderer>(textIter.GetColumnIndexOf<TextRenderer>());
            var transforms = textIter.Data<LocalToWorld>(textIter.GetColumnIndexOf<LocalToWorld>());
            for (int i = 0; i < textIter.Count; i++)
            {
                if (!string.IsNullOrEmpty(renderers[i].Text))
                    texts.Add((renderers[i].Text, renderers[i].Color, transforms[i].Value));
            }
        }

        if (texts.Count == 0) return;

        // Orthographic projection
        float aspect = (float)ctx.Width / ctx.Height;
        float halfH = 5f;
        float halfW = halfH * aspect;
        var projection = Matrix4x4.CreateOrthographicOffCenter(-halfW, halfW, -halfH, halfH, -1f, 1f);

        // Upload projection
        var uniformData = new TextUniforms[1];
        uniformData[0].Projection = projection;
        _textUniformBuffer!.UploadData<TextUniforms>(uniformData.AsSpan());

        // Build vertex data
        float scale = 1f / _fontAtlas.FontSize;
        var vertices = new List<float>();
        var indices = new List<uint>();

        foreach (var (text, color, worldMat) in texts)
        {
            float cursorX = 0;
            var origin = Vector3.Transform(Vector3.Zero, worldMat);

            foreach (var ch in text)
            {
                if (!_fontAtlas!.Glyphs.TryGetValue(ch, out var glyph))
                    continue;

                float x0 = origin.X + cursorX + glyph.Offset.X * scale;
                float y0 = origin.Y - glyph.Size.Y * scale + glyph.Offset.Y * scale;
                float x1 = x0 + glyph.Size.X * scale;
                float y1 = y0 + glyph.Size.Y * scale;

                uint baseIdx = (uint)(vertices.Count / 4);
                vertices.AddRange([x0, y0, glyph.UVMin.X, glyph.UVMax.Y]);
                vertices.AddRange([x1, y0, glyph.UVMax.X, glyph.UVMax.Y]);
                vertices.AddRange([x0, y1, glyph.UVMin.X, glyph.UVMin.Y]);
                vertices.AddRange([x1, y1, glyph.UVMax.X, glyph.UVMin.Y]);
                indices.AddRange([baseIdx, baseIdx + 1, baseIdx + 2, baseIdx + 2, baseIdx + 1, baseIdx + 3]);

                cursorX += glyph.Advance * scale;
            }
        }

        if (vertices.Count == 0) return;

        var dynamicVB = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(vertices.Count * sizeof(float)),
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
        });
        dynamicVB.UploadData<float>(vertices.ToArray());

        var dynamicIB = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(indices.Count * sizeof(uint)),
            Usage = BufferUsage.Index | BufferUsage.CopyDst,
        });
        dynamicIB.UploadData<uint>(indices.ToArray());

        var graph = context.RenderGraph;
        graph.AddPass($"{ctx.Prefix}Text", setup: pass =>
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
        }, execute: exeCtx =>
        {
            var encoder = exeCtx.Encoder;
            encoder.SetPipeline(_textPipeline!);
            encoder.SetBindingSet(0, _textBindingSet!);
            encoder.SetVertexBuffer(0, dynamicVB);
            encoder.SetIndexBuffer(dynamicIB);
            encoder.DrawIndexed(indices.Count);
        });
    }
}
